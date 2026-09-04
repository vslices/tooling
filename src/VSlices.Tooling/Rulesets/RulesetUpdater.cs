namespace VSlices.Tooling;

internal static class RulesetUpdater
{
    public static async Task<int> Update(
        VSlicesProjectContext project,
        CancellationToken cancellationToken)
    {
        var configuration = project.Configuration;
        var source = configuration.RulesetSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            TerminalOutput.Error("UPD011: The project does not declare ruleset.source in .vslices/config.yaml.");
            return 1;
        }

        var target = CommandInfrastructure.NormalizeTarget(configuration.DefaultTarget ?? "csharp");
        var reference = configuration.RulesetRef;

        TerminalOutput.Detail("Ruleset source", source);
        if (!string.IsNullOrWhiteSpace(reference))
            TerminalOutput.Detail("Ruleset ref", reference);
        TerminalOutput.Detail("Target", CommandInfrastructure.DisplayTarget(target));
        TerminalOutput.BlankLine();

        var stagingRoot = Path.Combine(
            Path.GetTempPath(),
            "vslices-ruleset-update-" + Guid.NewGuid().ToString("N"));
        var prepared = Path.Combine(
            project.VslicesRoot,
            ".ruleset-update-" + Guid.NewGuid().ToString("N"));

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
                        stagingRoot,
                        cancellationToken));
            }
            else
            {
                materialized = await RulesetSourceMaterializer.Materialize(
                    rulesetSource,
                    stagingRoot,
                    cancellationToken);
            }

            if (!materialized.IsSuccess)
            {
                TerminalOutput.Error($"{materialized.DiagnosticCode}: {materialized.Message}");
                return 1;
            }

            var preparedResult = RulesetSnapshotInstaller.Prepare(
                materialized.Root!,
                target,
                prepared);
            if (!preparedResult.IsSuccess)
            {
                CommandInfrastructure.WriteDiagnostics(preparedResult.Diagnostics);
                return 1;
            }

            RulesetSnapshotInstaller.Replace(project.VslicesRoot, prepared);
            TerminalOutput.Success("✓ Ruleset updated");
            return 0;
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
            if (Directory.Exists(prepared))
                Directory.Delete(prepared, recursive: true);
        }
    }
}
