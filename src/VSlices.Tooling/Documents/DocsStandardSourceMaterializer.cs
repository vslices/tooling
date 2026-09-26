using System.IO.Compression;

namespace VSlices.Tooling;

internal sealed record DocsStandardSource(string Location, string? Reference = null);

internal sealed record DocsStandardMaterializationResult(
    string? Root,
    string? DiagnosticCode,
    string? Message,
    bool IsRemote)
{
    public bool IsSuccess => Root is not null && DiagnosticCode is null;

    public static DocsStandardMaterializationResult Success(string root, bool isRemote) =>
        new(root, null, null, isRemote);

    public static DocsStandardMaterializationResult Failure(
        string code,
        string message,
        bool isRemote = false) =>
        new(null, code, message, isRemote);
}

internal static class DocsStandardSourceMaterializer
{
    public static async Task<DocsStandardMaterializationResult> Materialize(
        DocsStandardSource source,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        if (!IsRemoteSource(source.Location))
        {
            var local = Path.GetFullPath(source.Location, Environment.CurrentDirectory);
            if (!Directory.Exists(local))
            {
                return DocsStandardMaterializationResult.Failure(
                    "DSM001",
                    $"Docs Standard source '{source.Location}' is neither an existing directory nor an HTTP(S) URL.");
            }

            if (!string.IsNullOrWhiteSpace(source.Reference))
            {
                return DocsStandardMaterializationResult.Failure(
                    "DSM002",
                    "A Docs Standard ref applies only to supported GitHub repository sources, not local directories.");
            }

            return DocsStandardMaterializationResult.Success(local, isRemote: false);
        }

        if (!Uri.TryCreate(source.Location, UriKind.Absolute, out var uri))
        {
            return DocsStandardMaterializationResult.Failure(
                "DSM001",
                $"Docs Standard source '{source.Location}' is not a valid HTTP(S) URL.",
                isRemote: true);
        }

        try
        {
            if (IsGitHubRepositoryUri(uri))
            {
                if (string.IsNullOrWhiteSpace(source.Reference))
                {
                    return DocsStandardMaterializationResult.Failure(
                        "DSM003",
                        "A GitHub Docs Standard source requires a ref in its origin or recorded provenance so the snapshot is reproducible.",
                        isRemote: true);
                }

                using var http = new HttpClient();
                foreach (var candidate in GitHubArchiveCandidates(uri, source.Reference))
                {
                    using var response = await http.GetAsync(
                        candidate,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    return ExtractArchive(stream, stagingRoot, candidate);
                }

                return DocsStandardMaterializationResult.Failure(
                    "DSM004",
                    $"Could not resolve GitHub Docs Standard ref '{source.Reference}' as a branch, tag, or commit.",
                    isRemote: true);
            }

            if (!string.IsNullOrWhiteSpace(source.Reference))
            {
                return DocsStandardMaterializationResult.Failure(
                    "DSM005",
                    "Docs Standard refs are currently supported only for GitHub repository sources.",
                    isRemote: true);
            }

            using var directHttp = new HttpClient();
            await using var directStream = await directHttp.GetStreamAsync(uri, cancellationToken);
            return ExtractArchive(directStream, stagingRoot, source.Location);
        }
        catch (Exception ex)
        {
            return DocsStandardMaterializationResult.Failure(
                "DSM006",
                $"Could not materialize Docs Standard source: {ex.Message}",
                isRemote: true);
        }
    }

    public static bool IsRemoteSource(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https";

    internal static IReadOnlyList<string> GitHubArchiveCandidates(Uri repository, string reference)
    {
        var baseUri = repository.GetLeftPart(UriPartial.Authority) + repository.AbsolutePath.TrimEnd('/');
        if (baseUri.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            baseUri = baseUri[..^4];

        var escapedRef = string.Join(
            "/",
            reference.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

        return
        [
            $"{baseUri}/archive/refs/heads/{escapedRef}.zip",
            $"{baseUri}/archive/refs/tags/{escapedRef}.zip",
            $"{baseUri}/archive/{Uri.EscapeDataString(reference)}.zip"
        ];
    }

    private static bool IsGitHubRepositoryUri(Uri uri)
    {
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 && !segments[1].EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static DocsStandardMaterializationResult ExtractArchive(
        Stream stream,
        string stagingRoot,
        string sourceDescription)
    {
        Directory.CreateDirectory(stagingRoot);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        archive.ExtractToDirectory(stagingRoot);

        var manifests = Directory
            .EnumerateFiles(stagingRoot, "manifest.yaml", SearchOption.AllDirectories)
            .Take(2)
            .ToArray();

        if (manifests.Length != 1)
        {
            return DocsStandardMaterializationResult.Failure(
                "DSM007",
                $"Docs Standard archive '{sourceDescription}' must contain exactly one manifest.yaml.",
                isRemote: true);
        }

        return DocsStandardMaterializationResult.Success(
            Path.GetDirectoryName(manifests[0])!,
            isRemote: true);
    }
}
