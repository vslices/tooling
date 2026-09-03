using System.IO.Compression;

namespace VSlices.Tooling;

internal static class RulesetCommands
{
    private const string OfficialRulesetArchive =
        "https://github.com/vslices/ruleset/archive/refs/heads/main.zip";

    private const string DefaultIgnoreContent =
        "# Project-specific paths ignored by VSlices artifact discovery.\n" +
        "# Built-in exclusions: .git/, .vslices/, bin/, obj/.\n";

    /// <summary>Initializes a project-local .vslices ruleset from the official ruleset or a custom source.</summary>
    /// <param name="from">Custom ruleset directory or ZIP URL. If omitted, interactive terminals offer the official ruleset.</param>
    /// <param name="target">-t, Target rules to install. Current experimental target: C#.</param>
    /// <param name="force">Replace an existing project-local ruleset.</param>
    public static async Task<int> Init(
        string? from = null,
        string? target = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var explicitSource = !string.IsNullOrWhiteSpace(from) ||
                             !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VSLICES_RULESET_SOURCE"));

        var source = string.IsNullOrWhiteSpace(from)
            ? Environment.GetEnvironmentVariable("VSLICES_RULESET_SOURCE")
            : from;

        if (string.IsNullOrWhiteSpace(source))
        {
            source = Console.IsInputRedirected
                ? OfficialRulesetArchive
                : PromptRulesetSource();
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            Console.Error.WriteLine("CLI010: Ruleset initialization was cancelled.");
            return 2;
        }

        var selectedTarget = ResolveTarget(target);
        if (selectedTarget is null)
            return 2;

        var projectRoot = Environment.CurrentDirectory;
        var vslicesRoot = Path.Combine(projectRoot, ".vslices");
        var rulesetTarget = Path.Combine(vslicesRoot, "ruleset");
        if (Directory.Exists(rulesetTarget) && !force)
        {
            Console.Error.WriteLine(
                $"CLI011: Ruleset already exists at '{rulesetTarget}'. Use --force to replace it.");
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

            var sourceTarget = Path.Combine(sourceRoot, selectedTarget);
            if (!Directory.Exists(sourceTarget))
            {
                Console.Error.WriteLine(
                    $"CLI016: Ruleset source does not contain target '{selectedTarget}'.");
                return 1;
            }

            if (Directory.Exists(rulesetTarget))
                Directory.Delete(rulesetTarget, recursive: true);

            Directory.CreateDirectory(rulesetTarget);
            CopyRootFiles(sourceRoot, rulesetTarget);
            CopyDirectory(sourceTarget, Path.Combine(rulesetTarget, selectedTarget));

            var ignorePath = Path.Combine(vslicesRoot, ".ignore");
            if (!File.Exists(ignorePath))
                await File.WriteAllTextAsync(ignorePath, DefaultIgnoreContent, cancellationToken);

            var existingConfiguration = ProjectConfiguration.LoadFromProjectRoot(projectRoot);
            var official = source.Equals(OfficialRulesetArchive, StringComparison.OrdinalIgnoreCase);
            var configuration = new ProjectConfiguration(
                ProjectConfiguration.CurrentVersion,
                selectedTarget,
                official
                    ? ProjectConfiguration.OfficialRulesetSource
                    : explicitSource
                        ? source
                        : existingConfiguration?.RulesetSource ?? source,
                official
                    ? ProjectConfiguration.OfficialRulesetRef
                    : existingConfiguration?.RulesetRef,
                existingConfiguration?.UpdateSource ?? ProjectConfiguration.OfficialToolingSource,
                existingConfiguration?.UpdateChannel ?? ProjectConfiguration.DefaultUpdateChannel,
                existingConfiguration?.UpdatePullRequest);

            await ProjectConfiguration.WriteAsync(projectRoot, configuration, cancellationToken);

            Console.WriteLine(
                $"Initialized VSlices ruleset at '{rulesetTarget}' with target {CommandInfrastructure.DisplayTarget(selectedTarget)}.");
            return 0;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static string? PromptRulesetSource()
    {
        Console.WriteLine("Select a ruleset source:");
        Console.WriteLine("  1. VSlices official ruleset");
        Console.WriteLine("  2. Custom source");
        Console.Write("Choice [1]: ");

        var choice = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(choice) || choice == "1")
            return OfficialRulesetArchive;

        if (choice != "2")
        {
            Console.Error.WriteLine("CLI017: Invalid ruleset source selection.");
            return null;
        }

        Console.Write("Ruleset directory or ZIP URL: ");
        return Console.ReadLine()?.Trim();
    }

    private static string? ResolveTarget(string? target)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            var normalized = CommandInfrastructure.NormalizeTarget(target);
            if (normalized == "csharp")
                return normalized;

            Console.Error.WriteLine(
                $"CLI020: Target '{target}' is not supported. Current experimental target: C#." );
            return null;
        }

        if (Console.IsInputRedirected)
            return "csharp";

        Console.WriteLine("Select a target:");
        Console.WriteLine("  1. C#");
        Console.Write("Choice [1]: ");

        var choice = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(choice) || choice == "1")
            return "csharp";

        Console.Error.WriteLine("CLI018: Invalid target selection.");
        return null;
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
