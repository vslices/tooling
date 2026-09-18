namespace VSlices.Tooling.Tests;

public sealed class TemplateStandardUpdateTests
{
    [Fact]
    public async Task Project_configuration_round_trips_template_standard_source_and_ref()
    {
        using var project = new ToolingTestProject();
        var configuration = ProjectConfiguration.Default() with
        {
            TemplateStandardSource = "https://github.com/vslices/template-standard",
            TemplateStandardRef = "feat/initial-markdown-template"
        };

        await ProjectConfiguration.WriteAsync(project.Root, configuration, CancellationToken.None);

        var loaded = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(loaded);
        Assert.Equal("https://github.com/vslices/template-standard", loaded!.TemplateStandardSource);
        Assert.Equal("feat/initial-markdown-template", loaded.TemplateStandardRef);
    }

    [Fact]
    public async Task Successful_update_records_provenance_and_plain_update_reuses_it()
    {
        using var project = new ToolingTestProject();
        Assert.Equal(0, (await project.Run(project.Root, "init")).ExitCode);

        var source = Path.Combine(project.Root, "source-template-standard");
        WriteTemplateStandard(source, "markdown.question-tree");

        var first = await project.Run(
            project.Root,
            "update", "template-standard",
            "--origin", source);

        Assert.Equal(0, first.ExitCode);

        var installed = Path.Combine(
            project.VslicesRoot,
            "template-standard",
            "templates",
            "markdown",
            "question-tree.yaml");
        Assert.True(File.Exists(installed));
        Assert.Contains("id: markdown.question-tree", File.ReadAllText(installed), StringComparison.Ordinal);

        var configured = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(configured);
        Assert.Equal(source, configured!.TemplateStandardSource);
        Assert.Null(configured.TemplateStandardRef);

        WriteTemplateStandard(source, "markdown.question-tree-v2");

        var second = await project.Run(
            project.Root,
            "update", "template-standard");

        Assert.Equal(0, second.ExitCode);
        Assert.Contains("Template Standard source", second.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(source, second.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("id: markdown.question-tree-v2", File.ReadAllText(installed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Valid_source_installs_only_manifest_reachable_files()
    {
        using var project = new ToolingTestProject();
        Assert.Equal(0, (await project.Run(project.Root, "init")).ExitCode);

        var source = Path.Combine(project.Root, "source-template-standard");
        WriteTemplateStandard(source, "markdown.question-tree");
        File.WriteAllText(Path.Combine(source, "unreachable.txt"), "not part of the standard");

        var result = await project.Run(
            project.Root,
            "update", "template-standard",
            "--origin", source);

        Assert.Equal(0, result.ExitCode);

        var installed = Path.Combine(project.VslicesRoot, "template-standard");
        Assert.False(File.Exists(Path.Combine(installed, "unreachable.txt")));
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(project.VslicesRoot),
            path => Path.GetFileName(path).StartsWith(".template-standard-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Failed_update_preserves_last_known_snapshot_and_provenance()
    {
        using var project = new ToolingTestProject();
        Assert.Equal(0, (await project.Run(project.Root, "init")).ExitCode);

        var valid = Path.Combine(project.Root, "valid-template-standard");
        WriteTemplateStandard(valid, "markdown.question-tree");
        Assert.Equal(
            0,
            (await project.Run(
                project.Root,
                "update", "template-standard",
                "--origin", valid)).ExitCode);

        var invalid = Path.Combine(project.Root, "invalid-template-standard");
        Directory.CreateDirectory(invalid);
        File.WriteAllText(
            Path.Combine(invalid, "manifest.yaml"),
            """
            kind: vslices-template-standard
            version: 0.1
            templates:
              - templates/missing.yaml
            """);

        var failed = await project.Run(
            project.Root,
            "update", "template-standard",
            "--origin", invalid);

        Assert.NotEqual(0, failed.ExitCode);

        var configured = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(configured);
        Assert.Equal(valid, configured!.TemplateStandardSource);
        Assert.Null(configured.TemplateStandardRef);

        var installed = Path.Combine(
            project.VslicesRoot,
            "template-standard",
            "templates",
            "markdown",
            "question-tree.yaml");
        Assert.Contains("id: markdown.question-tree", File.ReadAllText(installed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Local_source_with_ref_fails_instead_of_silently_interpreting_it()
    {
        using var project = new ToolingTestProject();
        Assert.Equal(0, (await project.Run(project.Root, "init")).ExitCode);

        var source = Path.Combine(project.Root, "source-template-standard");
        WriteTemplateStandard(source, "markdown.question-tree");

        var result = await project.Run(
            project.Root,
            "update", "template-standard",
            "--from", source,
            "--ref", "main");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("TSM002", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Origin_cannot_be_combined_with_legacy_template_standard_source_options()
    {
        using var project = new ToolingTestProject();
        Assert.Equal(0, (await project.Run(project.Root, "init")).ExitCode);

        var result = await project.Run(
            project.Root,
            "update", "template-standard",
            "--origin", "vslices/template-standard:main",
            "--from", "https://github.com/vslices/template-standard");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("UPD045", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_template_standard_requires_a_vslices_project()
    {
        using var project = new ToolingTestProject();

        var source = Path.Combine(project.Root, "source-template-standard");
        WriteTemplateStandard(source, "markdown.question-tree");

        var result = await project.Run(
            project.Root,
            "update", "template-standard",
            "--origin", source);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("UPD040", result.StandardError, StringComparison.Ordinal);
    }

    private static void WriteTemplateStandard(string root, string templateId)
    {
        var templates = Path.Combine(root, "templates", "markdown");
        Directory.CreateDirectory(templates);

        File.WriteAllText(
            Path.Combine(root, "manifest.yaml"),
            """
            kind: vslices-template-standard
            version: 0.1
            templates:
              - templates/markdown/question-tree.yaml
            """);

        File.WriteAllText(
            Path.Combine(templates, "question-tree.yaml"),
            $$"""
            kind: vslices-materialization-template
            version: 0.1

            template:
              id: {{templateId}}
              artifact-kind: document
              media-type: text/markdown

            authority:
              semantics: docs-standard
              materialization: template-standard

            representation: {}
            reconstruction: {}
            constraints: []
            """);
    }
}
