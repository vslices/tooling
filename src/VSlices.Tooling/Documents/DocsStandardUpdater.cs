namespace VSlices.Tooling;

internal static class DocsStandardUpdater
{
    public const string OfficialSource = ProjectConfiguration.OfficialDocsStandardSource;
    public const string OfficialRef = ProjectConfiguration.OfficialDocsStandardRef;

    public static async Task<int> Update(
        VSlicesProjectContext project,
        string source,
        string? reference,
        CancellationToken cancellationToken)
    {
        TerminalOutput.Detail("Docs Standard source", source);
        if (!string.IsNullOrWhiteSpace(reference))
            TerminalOutput.Detail("Docs Standard ref", reference);
        TerminalOutput.Detail(
            "Destination",
            Path.GetRelativePath(project.ProjectRoot, Path.Combine(project.VslicesRoot, "docs-standard")));
        TerminalOutput.BlankLine();

        var stagingRoot = Path.Combine(
            Path.GetTempPath(),
            "vslices-docs-standard-update-" + Guid.NewGuid().ToString("N"));
        var prepared = Path.Combine(
            project.VslicesRoot,
            ".docs-standard-update-" + Guid.NewGuid().ToString("N"));

        try
        {
            DocsStandardMaterializationResult materialized = null!;
            var docsStandardSource = new DocsStandardSource(source, reference);
            if (DocsStandardSourceMaterializer.IsRemoteSource(source))
            {
                await TerminalOutput.ProgressAsync(
                    "Downloading Docs Standard...",
                    async () => materialized = await DocsStandardSourceMaterializer.Materialize(
                        docsStandardSource,
                        stagingRoot,
                        cancellationToken));
            }
            else
            {
                materialized = await DocsStandardSourceMaterializer.Materialize(
                    docsStandardSource,
                    stagingRoot,
                    cancellationToken);
            }

            if (!materialized.IsSuccess)
            {
                TerminalOutput.Error($"{materialized.DiagnosticCode}: {materialized.Message}");
                return 1;
            }

            TerminalOutput.Success("✓ Docs Standard materialized");

            var preparedResult = DocsStandardSnapshotInstaller.Prepare(
                materialized.Root!,
                prepared);
            if (!preparedResult.IsSuccess)
            {
                TerminalOutput.Error(preparedResult.Error!);
                return 1;
            }

            TerminalOutput.Success("✓ Docs Standard validated");

            var firstInstall = !File.Exists(
                Path.Combine(
                    project.VslicesRoot,
                    "docs-standard",
                    "manifest.yaml"));

            var configuration = firstInstall
                ? DocumentPolicyBootstrap.ConfigureFirstInstall(
                    project.Configuration,
                    project.ProjectRoot)
                : project.Configuration;

            DocsStandardSnapshotInstaller.Replace(project.VslicesRoot, prepared);

            configuration = configuration with
            {
                DocsStandardSource = source,
                DocsStandardRef = reference
            };
            await ProjectConfiguration.WriteAsync(
                project.ProjectRoot,
                configuration,
                cancellationToken);

            TerminalOutput.Success("✓ Docs Standard updated");
            TerminalOutput.Success("✓ Docs Standard provenance recorded");
            return 0;
        }
        catch (Exception ex)
        {
            TerminalOutput.Error($"UPD034: Could not update Docs Standard: {ex.Message}");
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
