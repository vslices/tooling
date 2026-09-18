namespace VSlices.Tooling;

internal static class TemplateStandardUpdater
{
    public static async Task<int> Update(VSlicesProjectContext project, string source, string? reference, CancellationToken cancellationToken)
    {
        TerminalOutput.Detail("Template Standard source", source);
        if (!string.IsNullOrWhiteSpace(reference))
            TerminalOutput.Detail("Template Standard ref", reference);
        TerminalOutput.Detail("Destination", Path.GetRelativePath(project.ProjectRoot, Path.Combine(project.VslicesRoot, "template-standard")));
        TerminalOutput.BlankLine();

        var stagingRoot = Path.Combine(Path.GetTempPath(), "vslices-template-standard-update-" + Guid.NewGuid().ToString("N"));
        var prepared = Path.Combine(project.VslicesRoot, ".template-standard-update-" + Guid.NewGuid().ToString("N"));

        try
        {
            TemplateStandardMaterializationResult materialized = null!;
            var templateSource = new TemplateStandardSource(source, reference);
            if (TemplateStandardSourceMaterializer.IsRemoteSource(source))
            {
                await TerminalOutput.ProgressAsync(
                    "Downloading Template Standard...",
                    async () => materialized = await TemplateStandardSourceMaterializer.Materialize(templateSource, stagingRoot, cancellationToken));
            }
            else
            {
                materialized = await TemplateStandardSourceMaterializer.Materialize(templateSource, stagingRoot, cancellationToken);
            }

            if (!materialized.IsSuccess)
            {
                TerminalOutput.Error($"{materialized.DiagnosticCode}: {materialized.Message}");
                return 1;
            }

            TerminalOutput.Success("✓ Template Standard materialized");
            var preparedResult = TemplateStandardSnapshotInstaller.Prepare(materialized.Root!, prepared);
            if (!preparedResult.IsSuccess)
            {
                TerminalOutput.Error(preparedResult.Error!);
                return 1;
            }

            TerminalOutput.Success("✓ Template Standard validated");
            TemplateStandardSnapshotInstaller.Replace(project.VslicesRoot, prepared);

            var configuration = project.Configuration with
            {
                TemplateStandardSource = source,
                TemplateStandardRef = reference
            };
            await ProjectConfiguration.WriteAsync(project.ProjectRoot, configuration, cancellationToken);

            TerminalOutput.Success("✓ Template Standard updated");
            TerminalOutput.Success("✓ Template Standard provenance recorded");
            return 0;
        }
        catch (Exception ex)
        {
            TerminalOutput.Error($"UPD044: Could not update Template Standard: {ex.Message}");
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
