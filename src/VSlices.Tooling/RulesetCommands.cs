using System.IO.Compression;

namespace VSlices.Tooling;

internal static class RulesetCommands
{
    /// <summary>Initializes a project-local .vslices ruleset from a local directory or ZIP URL.</summary>
    /// <param name="from">Source ruleset directory or ZIP URL. If omitted, VSLICES_RULESET_SOURCE is used.</param>
    /// <param name="force">Replace an existing project-local ruleset.</param>
    public static async Task<int> Init(
        string? from = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var source = string.IsNullOrWhiteSpace(from)
            ? Environment.GetEnvironmentVariable("VSLICES_RULESET_SOURCE")
            : from;

        if (string.IsNullOrWhiteSpace(source))
        {
            Console.Error.WriteLine(
                "CLI010: No ruleset source was provided. Use --from <directory-or-zip-url> or set VSLICES_RULESET_SOURCE.");
            return 2;
        }

        var target = Path.Combine(Environment.CurrentDirectory, ".vslices", "ruleset");
        if (Directory.Exists(target) && !force)
        {
            Console.Error.WriteLine(
                $"CLI011: Ruleset already exists at '{target}'. Use --force to replace it.");
            return 1;
        }

        var staging = Path.Combine(Path.GetTempPath(), "vslices-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var sourceRoot = await MaterializeSource(source, staging, cancellationToken);
            if (sourceRoot is null)
                return 1;

            if (!File.Exists(Path.Combine(sourceRoot, "manifest.yaml")))
            {
                Console.Error.WriteLine(
                    $"CLI012: Ruleset source '{source}' does not contain manifest.yaml.");
                return 1;
            }

            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            CopyDirectory(sourceRoot, target);

            Console.WriteLine($"Initialized VSlices ruleset at '{target}'.");
            return 0;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static async Task<string?> MaterializeSource(
        string source,
        string staging,
        CancellationToken cancellationToken)
    {
        var local = Path.GetFullPath(source, Environment.CurrentDirectory);
        if (Directory.Exists(local))
            return local;

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            Console.Error.WriteLine(
                $"CLI013: Ruleset source '{source}' is neither an existing directory nor an HTTP(S) URL.");
            return null;
        }

        try
        {
            using var http = new HttpClient();
            await using var stream = await http.GetStreamAsync(uri, cancellationToken);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            archive.ExtractToDirectory(staging);

            var manifests = Directory
                .EnumerateFiles(staging, "manifest.yaml", SearchOption.AllDirectories)
                .Take(2)
                .ToArray();

            if (manifests.Length != 1)
            {
                Console.Error.WriteLine(
                    "CLI014: Downloaded ruleset archive must contain exactly one manifest.yaml.");
                return null;
            }

            return Path.GetDirectoryName(manifests[0]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CLI015: Could not download ruleset source: {ex.Message}");
            return null;
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);

        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
}
