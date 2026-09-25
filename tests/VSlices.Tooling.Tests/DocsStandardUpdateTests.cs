namespace VSlices.Tooling.Tests;

public sealed class DocsStandardUpdateTests
{
    [Fact]
    public async Task Project_configuration_round_trips_docs_standard_source_and_ref()
    {
        using var project = new ToolingTestProject();
        var configuration = ProjectConfiguration.Default() with
        {
            DocsStandardSource = "https://github.com/vslices/docs-standard",
            DocsStandardRef = "feat/document-authoring-preview"
        };

        await ProjectConfiguration.WriteAsync(
            project.Root,
            configuration,
            CancellationToken.None);

        var loaded = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(loaded);
        Assert.Equal(
            "https://github.com/vslices/docs-standard",
            loaded!.DocsStandardSource);
        Assert.Equal(
            "feat/document-authoring-preview",
            loaded.DocsStandardRef);
    }

    [Fact]
    public async Task Valid_local_source_replaces_snapshot_and_installs_only_manifest_reachable_files()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();

        var source = Path.Combine(project.Root, "source-docs-standard");
        WriteDocsStandard(source, "¿Dónde existe ahora?");
        File.WriteAllText(Path.Combine(source, "unreachable.txt"), "not part of the standard");

        var installed = Path.Combine(project.VslicesRoot, "docs-standard");
        WriteDocsStandard(installed, "¿Dónde existía?");
        File.WriteAllText(Path.Combine(installed, "old.marker"), "old");

        var result = await project.Run(
            project.Root,
            "update", "docs-standard", "--origin", source);

        Assert.Equal(0, result.ExitCode);
        var installedDefinition = File.ReadAllText(
            Path.Combine(installed, "documents", "context-document.yml"));
        Assert.Contains("¿Dónde existe ahora?", installedDefinition, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(installed, "old.marker")));
        Assert.False(File.Exists(Path.Combine(installed, "unreachable.txt")));
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(project.VslicesRoot),
            path => Path.GetFileName(path).StartsWith(".docs-standard-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Successful_update_records_provenance_and_plain_update_reuses_it()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();

        var source = Path.Combine(project.Root, "source-docs-standard");
        WriteDocsStandard(source, "¿Dónde existe inicialmente?");

        var first = await project.Run(
            project.Root,
            "update", "docs-standard", "--origin", source);

        Assert.Equal(0, first.ExitCode);
        var configured = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(configured);
        Assert.Equal(source, configured!.DocsStandardSource);
        Assert.Null(configured.DocsStandardRef);

        WriteDocsStandard(source, "¿Dónde existe después?");
        var second = await project.Run(
            project.Root,
            "update", "docs-standard");

        Assert.Equal(0, second.ExitCode);
        Assert.Contains(
            "Docs Standard source",
            second.StandardOutput,
            StringComparison.Ordinal);
        Assert.Contains(source, second.StandardOutput, StringComparison.Ordinal);

        var installedDefinition = File.ReadAllText(
            Path.Combine(
                project.VslicesRoot,
                "docs-standard",
                "documents",
                "context-document.yml"));
        Assert.Contains("¿Dónde existe después?", installedDefinition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failed_update_preserves_last_known_docs_standard_provenance()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();

        var validSource = Path.Combine(project.Root, "valid-docs-standard");
        WriteDocsStandard(validSource, "¿Dónde existe?");
        Assert.Equal(
            0,
            (await project.Run(
                project.Root,
                "update", "docs-standard", "--origin", validSource)).ExitCode);

        var invalidSource = Path.Combine(project.Root, "invalid-docs-standard");
        Directory.CreateDirectory(invalidSource);
        File.WriteAllText(
            Path.Combine(invalidSource, "manifest.yaml"),
            """
            kind: vslices-docs-standard
            version: 0.1
            documents:
              - documents/missing.yml
            """);

        var failed = await project.Run(
            project.Root,
            "update", "docs-standard", "--origin", invalidSource);

        Assert.NotEqual(0, failed.ExitCode);
        var configured = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(configured);
        Assert.Equal(validSource, configured!.DocsStandardSource);
        Assert.Null(configured.DocsStandardRef);
    }

    [Fact]
    public async Task Invalid_candidate_never_replaces_current_snapshot()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();

        var source = Path.Combine(project.Root, "invalid-docs-standard");
        Directory.CreateDirectory(source);
        File.WriteAllText(
            Path.Combine(source, "manifest.yaml"),
            """
            kind: vslices-docs-standard
            version: 0.1
            documents:
              - documents/missing.yml
            """);

        var installed = Path.Combine(project.VslicesRoot, "docs-standard");
        WriteDocsStandard(installed, "¿Dónde existía?");

        var result = await project.Run(
            project.Root,
            "update", "docs-standard", "--origin", source);

        Assert.NotEqual(0, result.ExitCode);
        var installedDefinition = File.ReadAllText(
            Path.Combine(installed, "documents", "context-document.yml"));
        Assert.Contains("¿Dónde existía?", installedDefinition, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(project.VslicesRoot),
            path => Path.GetFileName(path).StartsWith(".docs-standard-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_question_cardinality_never_replaces_current_snapshot()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();

        var source = Path.Combine(project.Root, "invalid-cardinality-docs-standard");
        WriteDocsStandard(source, "¿Dónde existe ahora?", childCardinality: "several");

        var installed = Path.Combine(project.VslicesRoot, "docs-standard");
        WriteDocsStandard(installed, "¿Dónde existía?");

        var result = await project.Run(
            project.Root,
            "update", "docs-standard", "--origin", source);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCS027", result.StandardError, StringComparison.Ordinal);

        var installedDefinition = File.ReadAllText(
            Path.Combine(installed, "documents", "context-document.yml"));
        Assert.Contains("¿Dónde existía?", installedDefinition, StringComparison.Ordinal);
        Assert.DoesNotContain("cardinality: several", installedDefinition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_docs_standard_requires_a_vslices_project()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "source-docs-standard");
        WriteDocsStandard(source, "¿Dónde existe ahora?");

        var result = await project.Run(
            project.Root,
            "update", "docs-standard", "--origin", source);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("UPD030", result.StandardError, StringComparison.Ordinal);
    }

    private static void WriteDocsStandard(
        string root,
        string rootQuestion,
        string? childCardinality = null)
    {
        var documents = Path.Combine(root, "documents");
        Directory.CreateDirectory(documents);

        File.WriteAllText(
            Path.Combine(root, "manifest.yaml"),
            """
            kind: vslices-docs-standard
            version: 0.1
            documents:
              - documents/context-document.yml
            """);

        var cardinality = string.IsNullOrWhiteSpace(childCardinality)
            ? string.Empty
            : $"        cardinality: {childCardinality}{Environment.NewLine}";

        File.WriteAllText(
            Path.Combine(documents, "context-document.yml"),
            $$"""
            kind: vslices-document-definition
            version: 0.1

            document:
              type: context
              scopes:
                - concept
                - project
              question:
                id: context
                text: {{rootQuestion}}
                children:
                  - id: assumptions
                    text: ¿Qué estamos asumiendo como cierto?
            {{cardinality}}
            """);
    }
}
