namespace VSlices.Tooling;

internal static class RulesetCommands
{
    private const string DefaultIgnoreContent =
        "# Project-specific paths ignored by VSlices artifact discovery.\n" +
        "# Built-in exclusions: .git/, .vslices/, bin/, obj/.\n";

    /// <summary>Initializes the minimum project-local VSlices surface.</summary>
    /// <param name="rulesetOrigin">Optionally installs Ruleset through the same origin/update lifecycle as 'vslices update ruleset'.</param>
    /// <param name="docsStandardOrigin">Optionally installs Docs Standard through the same origin/update lifecycle as 'vslices update docs-standard'.</param>
    /// <param name="defaultOrigin">Installs any unspecified external knowledge source from its official VSlices origin.</param>
    /// <param name="from">Compatibility alias for --ruleset-origin.</param>
    /// <param name="target">-t, Default lowering target. Current experimental target: C#.</param>
    /// <param name="force">Reinitializes the minimum project surface while preserving existing project policy not explicitly replaced.</param>
    public static async Task<int> Init(
        string? rulesetOrigin = null,
        string? docsStandardOrigin = null,
        bool defaultOrigin = false,
        string? from = null,
        string? target = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(rulesetOrigin) &&
            !string.IsNullOrWhiteSpace(from))
        {
            TerminalOutput.Error(
                "CLI023: --ruleset-origin cannot be combined with the compatibility option --from.");
            return 2;
        }

        var environmentSource = Environment.GetEnvironmentVariable("VSLICES_RULESET_SOURCE");
        if (!string.IsNullOrWhiteSpace(environmentSource))
        {
            if (!string.IsNullOrWhiteSpace(rulesetOrigin) ||
                !string.IsNullOrWhiteSpace(from))
            {
                TerminalOutput.Error(
                    "CLI024: VSLICES_RULESET_SOURCE cannot be combined with --ruleset-origin or --from.");
                return 2;
            }

            rulesetOrigin = environmentSource;
        }
        else if (!string.IsNullOrWhiteSpace(from))
        {
            rulesetOrigin = from;
        }

        if (defaultOrigin)
        {
            rulesetOrigin ??= "vslices/ruleset:main";
            docsStandardOrigin ??= "vslices/docs-standard:main";
        }

        var projectRoot = Environment.CurrentDirectory;
        var existingConfiguration = ProjectConfiguration.LoadFromProjectRoot(projectRoot);
        if (existingConfiguration is not null && !force)
        {
            TerminalOutput.Warning("! Directory already contains a VSlices project");
            TerminalOutput.Detail(
                "Path",
                Path.GetRelativePath(
                    projectRoot,
                    Path.Combine(projectRoot, ".vslices", "config.yaml")));
            TerminalOutput.Muted("  Use --force to refresh the minimum project surface.");
            TerminalOutput.BlankLine();
            TerminalOutput.Error(
                "CLI011: VSlices project is already initialized. Use --force to reinitialize it.");
            return 1;
        }

        var selectedTarget = ResolveTarget(
            target,
            existingConfiguration?.DefaultTarget);
        if (selectedTarget is null)
            return 2;

        var configuration = existingConfiguration is null
            ? ProjectConfiguration.Default(selectedTarget)
            : existingConfiguration with
            {
                DefaultTarget = selectedTarget
            };

        await ProjectConfiguration.WriteAsync(
            projectRoot,
            configuration,
            cancellationToken);

        var vslicesRoot = Path.Combine(projectRoot, ".vslices");
        var ignorePath = Path.Combine(vslicesRoot, ".ignore");
        if (!File.Exists(ignorePath))
        {
            await File.WriteAllTextAsync(
                ignorePath,
                DefaultIgnoreContent,
                cancellationToken);
        }

        TerminalOutput.Detail("Target", CommandInfrastructure.DisplayTarget(selectedTarget));
        TerminalOutput.Detail("Configuration", Path.GetRelativePath(projectRoot, Path.Combine(vslicesRoot, "config.yaml")));
        TerminalOutput.Detail("Ignore policy", Path.GetRelativePath(projectRoot, ignorePath));
        if (force)
            TerminalOutput.Detail("Mode", "refresh minimum project surface");
        TerminalOutput.BlankLine();
        TerminalOutput.Success("✓ VSlices project initialized");

        if (!string.IsNullOrWhiteSpace(rulesetOrigin))
        {
            TerminalOutput.BlankLine();
            TerminalOutput.Info("→ Installing Ruleset from requested origin");
            var rulesetExit = await UpdateCommands.Ruleset(
                origin: rulesetOrigin,
                cancellationToken: cancellationToken);
            if (rulesetExit != 0)
                return rulesetExit;
        }

        if (!string.IsNullOrWhiteSpace(docsStandardOrigin))
        {
            TerminalOutput.BlankLine();
            TerminalOutput.Info("→ Installing Docs Standard from requested origin");
            var docsExit = await UpdateCommands.DocsStandard(
                origin: docsStandardOrigin,
                cancellationToken: cancellationToken);
            if (docsExit != 0)
                return docsExit;
        }

        return 0;
    }

    private static string? ResolveTarget(
        string? requestedTarget,
        string? existingTarget)
    {
        if (!string.IsNullOrWhiteSpace(requestedTarget))
        {
            var normalized = CommandInfrastructure.NormalizeTarget(requestedTarget);
            if (normalized == "csharp")
                return normalized;

            TerminalOutput.Error(
                $"CLI020: Target '{requestedTarget}' is not supported. Current experimental target: C#.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(existingTarget))
            return CommandInfrastructure.NormalizeTarget(existingTarget);

        return "csharp";
    }
}
