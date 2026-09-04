using System.IO.Compression;

namespace VSlices.Tooling;

internal sealed record RulesetSource(string Location, string? Reference = null);

internal sealed record RulesetMaterializationResult(
    string? Root,
    string? DiagnosticCode,
    string? Message,
    bool IsRemote)
{
    public bool IsSuccess => Root is not null && DiagnosticCode is null;

    public static RulesetMaterializationResult Success(string root, bool isRemote) =>
        new(root, null, null, isRemote);

    public static RulesetMaterializationResult Failure(string code, string message, bool isRemote = false) =>
        new(null, code, message, isRemote);
}

internal static class RulesetSourceMaterializer
{
    public static async Task<RulesetMaterializationResult> Materialize(
        RulesetSource source,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        if (!IsRemoteSource(source.Location))
        {
            var local = Path.GetFullPath(source.Location, Environment.CurrentDirectory);
            if (!Directory.Exists(local))
            {
                return RulesetMaterializationResult.Failure(
                    "RSM001",
                    $"Ruleset source '{source.Location}' is neither an existing directory nor an HTTP(S) URL.");
            }

            if (!string.IsNullOrWhiteSpace(source.Reference))
            {
                return RulesetMaterializationResult.Failure(
                    "RSM002",
                    "ruleset.ref applies only to supported GitHub repository sources, not local directories.");
            }

            return RulesetMaterializationResult.Success(local, isRemote: false);
        }

        if (!Uri.TryCreate(source.Location, UriKind.Absolute, out var uri))
        {
            return RulesetMaterializationResult.Failure(
                "RSM001",
                $"Ruleset source '{source.Location}' is not a valid HTTP(S) URL.",
                isRemote: true);
        }

        try
        {
            if (IsGitHubRepositoryUri(uri))
            {
                if (string.IsNullOrWhiteSpace(source.Reference))
                {
                    return RulesetMaterializationResult.Failure(
                        "RSM003",
                        "A GitHub repository ruleset source requires ruleset.ref so the snapshot is reproducible.",
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

                return RulesetMaterializationResult.Failure(
                    "RSM004",
                    $"Could not resolve GitHub ruleset ref '{source.Reference}' as a branch, tag, or commit.",
                    isRemote: true);
            }

            if (!string.IsNullOrWhiteSpace(source.Reference))
            {
                return RulesetMaterializationResult.Failure(
                    "RSM005",
                    "ruleset.ref is currently supported only for GitHub repository sources.",
                    isRemote: true);
            }

            using var directHttp = new HttpClient();
            await using var directStream = await directHttp.GetStreamAsync(uri, cancellationToken);
            return ExtractArchive(directStream, stagingRoot, source.Location);
        }
        catch (Exception ex)
        {
            return RulesetMaterializationResult.Failure(
                "RSM006",
                $"Could not materialize ruleset source: {ex.Message}",
                isRemote: true);
        }
    }

    public static bool IsRemoteSource(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https";

    private static bool IsGitHubRepositoryUri(Uri uri)
    {
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 && !segments[1].EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GitHubArchiveCandidates(Uri repository, string reference)
    {
        var baseUri = repository.GetLeftPart(UriPartial.Authority) + repository.AbsolutePath.TrimEnd('/');
        if (baseUri.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            baseUri = baseUri[..^4];

        var escapedRef = string.Join(
            "/",
            reference.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

        yield return $"{baseUri}/archive/refs/heads/{escapedRef}.zip";
        yield return $"{baseUri}/archive/refs/tags/{escapedRef}.zip";
        yield return $"{baseUri}/archive/{Uri.EscapeDataString(reference)}.zip";
    }

    private static RulesetMaterializationResult ExtractArchive(
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
            return RulesetMaterializationResult.Failure(
                "RSM007",
                $"Ruleset archive '{sourceDescription}' must contain exactly one manifest.yaml.",
                isRemote: true);
        }

        return RulesetMaterializationResult.Success(
            Path.GetDirectoryName(manifests[0])!,
            isRemote: true);
    }
}
