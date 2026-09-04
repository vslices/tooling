namespace VSlices.Tooling;

internal static class RulesetCommands
{
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
        var environmentSource = Environment.GetEnvironmentVariable("VSLICES_RULESET_SOURCE");
        var explicitSource = !string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(environmentSource);
        var source = string.IsNullOrWhiteSpace(from) ? environmentSource : from;

        if (string.IsNullOrWhiteSpace(source))
        {
            source = Console.IsInputRedirected
                ? ProjectConfiguration.OfficialRulesetSource
                : PromptRulesetSource();
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            TerminalOutput.Error("CLI010: Ruleset initialization was cancelled.");
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
            TerminalOutput.Warning("! Project already contains a VSlices ruleset");
            TerminalOutput.Detail("Path", rulesetTarget);
            TerminalOutput.Muted("  Use --force to replace it.");
            TerminalOutput.BlankLine();
            TerminalOutput.Error(
                $"CLI011: Ruleset already exists at '{rulesetTarget}'. Use --force to replace it.");
            return 1;
        }

        var official = source.Equals(ProjectConfiguration.OfficialRulesetSource, StringComparison.OrdinalIgnoreCase);
        var reference = official ? ProjectConfiguration.OfficialRulesetRef : null;

        TerminalOutput.Detail("Target", CommandInfrastructure.DisplayTarget(selectedTarget));
        TerminalOutput.Detail("Ruleset", official ? "official" : DescribeSource(source));
        TerminalOutput.Detail("Destination", Path.GetRelativePath(projectRoot, rulesetTarget));
        if (force)
            TerminalOutput.Detail("Mode", "replace existing");
        TerminalOutput.BlankLine();

        var staging = Path.Combine(Path.GetTempPath(), "vslices-init-" + Guid.NewGuid().ToString("N"));
        var prepared = Path.Combine(vslicesRoot, ".ruleset-init-" + Guid.NewGuid().ToString("N"));

        try
        {
            RulesetMaterializationResult materialized = null!;
            var rulesetSource = new RulesetSource(source, reference);
            if (RulesetSourceMaterializer.IsRemoteSource(source))
            {
                await TerminalOutput.ProgressAsync(
                    "Downloading ruleset...",
                    async () => materialized = await RulesetSourceMaterializer.Materialize(
                        rulesetSource,
                        staging,
                        cancellationToken));
            }
            else
            {
                materialized = await RulesetSourceMaterializer.Materialize(
                    rulesetSource,
                    staging,
                    cancellationToken);
            }

            if (!materialized.IsSuccess)
            {
                TerminalOutput.Error($"{materialized.DiagnosticCode}: {materialized.Message}");
                return 1;
            }

            TerminalOutput.Success("✓ Ruleset materialized");

            var preparedResult = RulesetSnapshotInstaller.Prepare(
                materialized.Root!,
                selectedTarget,
                prepared);
            if (!preparedResult.IsSuccess)
            {
                CommandInfrastructure.WriteDiagnostics(preparedResult.Diagnostics);
                return 1;
            }

            TerminalOutput.Success("✓ Ruleset validated");
            RulesetSnapshotInstaller.Replace(vslicesRoot, prepared);
            TerminalOutput.Success($"✓ {CommandInfrastructure.DisplayTarget(selectedTarget)} target installed");

            var ignorePath = Path.Combine(vslicesRoot, ".ignore");
            if (!File.Exists(ignorePath))
                await File.WriteAllTextAsync(ignorePath, DefaultIgnoreContent, cancellationToken);

            var existingConfiguration = ProjectConfiguration.LoadFromProjectRoot(projectRoot);
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
                existingConfiguration?.UpdatePullRequest,
                existingConfiguration?.LineageBootstrapConvention ?? ProjectConfiguration.DefaultLineageBootstrapConvention);

            await ProjectConfiguration.WriteAsync(projectRoot, configuration, cancellationToken);
            TerminalOutput.Success("✓ Configuration written");
            TerminalOutput.BlankLine();
            TerminalOutput.Success("VSlices project initialized");
            return 0;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            if (Directory.Exists(prepared))
                Directory.Delete(prepared, recursive: true);
        }
    }

    private static string? PromptRulesetSource()
    {
        TerminalOutput.Heading("Select a ruleset source");
        Console.WriteLine("  1. VSlices official ruleset");
        Console.WriteLine("  2. Custom source");
        Console.Write("Choice [1]: ");

        var choice = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(choice) || choice == "1")
            return ProjectConfiguration.OfficialRulesetSource;

        if (choice != "2")
        {
            TerminalOutput.Error("CLI017: Invalid ruleset source selection.");
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

            TerminalOutput.Error(
                $"CLI020: Target '{target}' is not supported. Current experimental target: C#.");
            return null;
        }

        if (Console.IsInputRedirected)
            return "csharp";

        TerminalOutput.Heading("Select a target");
        Console.WriteLine("  1. C#");
        Console.Write("Choice [1]: ");

        var choice = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(choice) || choice == "1")
            return "csharp";

        TerminalOutput.Error("CLI018: Invalid target selection.");
        return null;
    }

    private static string DescribeSource(string source)
    {
        if (RulesetSourceMaterializer.IsRemoteSource(source))
            return "custom remote";

        var local = Path.GetFullPath(source, Environment.CurrentDirectory);
        return Directory.Exists(local) ? "local" : "custom";
    }
}
