using System.IO.Compression;

namespace VSlices.Tooling;

internal static class RulesetUpdater
{
    public static async Task<int> Update(
        ProjectConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var projectRoot = FindProjectRoot(Environment.CurrentDirectory);
        if (projectRoot is null)
        {
            TerminalOutput.Error("UPD010: Could not locate a VSlices project configuration.");
            return 1;
        }

        var source = configuration.RulesetSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            TerminalOutput.Error("UPD011: The project does not declare ruleset.source in .vslices/config.yaml.");
            return 1;
        }

        var target = CommandInfrastructure.NormalizeTarget(configuration.DefaultTarget ?? "csharp");
        var reference = configuration.RulesetRef;
        var resolvedSource = ResolveSource(source, reference);

        TerminalOutput.Detail("Ruleset source", source);
        if (!string.IsNullOrWhiteSpace(reference))
            TerminalOutput.Detail("Ruleset ref", reference);
        TerminalOutput.Detail("Target", CommandInfrastructure.DisplayTarget(target));
        TerminalOutput.BlankLine();

        var stagingRoot = Path.Combine(
            Path.GetTempPath(),
            "vslices-ruleset-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingRoot);

        try
        {
            string? materialized = null;
            if (IsRemoteSource(resolvedSource))
            {
                await TerminalOutput.ProgressAsync(
                    "Downloading ruleset...",
                    async () => materialized = await MaterializeSource(
                        resolvedSource,
                        stagingRoot,
                        cancellationToken));
            }
            else
            {
                materialized = await MaterializeSource(
                    resolvedSource,
                    stagingRoot,
                    cancellationToken);
            }

            if (materialized is null)
                return 1;

            if (!File.Exists(Path.Combine(materialized, "manifest.yaml")))
            {
                TerminalOutput.Error(
                    $"UPD012: Ruleset source '{resolvedSource}' does not contain manifest.yaml.");
                return 1;
            }

            var sourceTarget = Path.Combine(materialized, target);
            if (!Directory.Exists(sourceTarget))
            {
                TerminalOutput.Error(
                    $"UPD013: Ruleset source does not contain target '{target}'.");
                return 1;
            }

            var vslicesRoot = Path.Combine(projectRoot, ".vslices");
            var rulesetTarget = Path.Combine(vslicesRoot, "ruleset");
            var prepared = Path.Combine(vslicesRoot, ".ruleset-update-" + Guid.NewGuid().ToString("N"));
            var backup = Path.Combine(vslicesRoot, ".ruleset-backup-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(prepared);
                CopyRootFiles(materialized, prepared);
                CopyDirectory(sourceTarget, Path.Combine(prepared, target));

                if (Directory.Exists(rulesetTarget))
                    Directory.Move(rulesetTarget, backup);

                try
                {
                    Directory.Move(prepared, rulesetTarget);
                }
                catch
                {
                    if (Directory.Exists(backup) && !Directory.Exists(rulesetTarget))
                        Directory.Move(backup, rulesetTarget);
                    throw;
                }

                if (Directory.Exists(backup))
                    Directory.Delete(backup, recursive: true);

                TerminalOutput.Success("✓ Ruleset updated");
                return 0;
            }
            finally
            {
                if (Directory.Exists(prepared))
                    Directory.Delete(prepared, recursive: true);
                if (Directory.Exists(backup) && Directory.Exists(rulesetTarget))
                    Directory.Delete(backup, recursive: true);
            }
        }
        catch (Exception ex)
        {
            TerminalOutput.Error($"UPD014: Could not update ruleset: {ex.Message}");
            return 1;
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    private static string ResolveSource(string source, string? reference)
    {
        if (!IsRemoteSource(source))
        {
            var local = Path.GetFullPath(source, Environment.CurrentDirectory);
            if (Directory.Exists(local))
                return source;
        }

        if (source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(reference))
            return source;

        return source.TrimEnd('/') + "/archive/refs/heads/" + reference + ".zip";
    }

    private static string? FindProjectRoot(string start)
    {
        var current = new DirectoryInfo(Path.GetFullPath(start));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".vslices", "config.yaml")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private static async Task<string?> MaterializeSource(
        string source,
        string staging,
        CancellationToken cancellationToken)
    {
        if (!IsRemoteSource(source))
        {
            var local = Path.GetFullPath(source, Environment.CurrentDirectory);
            if (Directory.Exists(local))
                return local;
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            TerminalOutput.Error(
                $"UPD015: Ruleset source '{source}' is neither an existing directory nor an HTTP(S) URL.");
            return null;
        }

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
            TerminalOutput.Error(
                "UPD016: Downloaded ruleset archive must contain exactly one manifest.yaml.");
            return null;
        }

        return Path.GetDirectoryName(manifests[0]);
    }

    private static bool IsRemoteSource(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https";

    private static void CopyRootFiles(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
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
