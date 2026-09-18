namespace VSlices.Tooling.Tests;

public sealed class ProjectInitializationLifecycleTests
{
    [Fact]
    public async Task Init_without_origins_creates_only_the_minimum_project_surface()
    {
        using var project = new ToolingTestProject();

        var result = await project.Run(project.Root, "init");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.VslicesRoot, "config.yaml")));
        Assert.True(File.Exists(Path.Combine(project.VslicesRoot, ".ignore")));
        Assert.False(Directory.Exists(project.RulesetRoot));
        Assert.False(Directory.Exists(Path.Combine(project.VslicesRoot, "docs-standard")));

        var configuration = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(configuration);
        Assert.Null(configuration!.RulesetSource);
        Assert.Null(configuration.RulesetRef);
        Assert.Null(configuration.DocsStandardSource);
        Assert.Null(configuration.DocsStandardRef);

        var text = File.ReadAllText(Path.Combine(project.VslicesRoot, "config.yaml"));
        Assert.DoesNotContain("ruleset:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("docs-standard:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_with_explicit_origins_delegates_to_the_same_component_update_lifecycles()
    {
        using var project = new ToolingTestProject();

        var rulesetOrigin = Path.Combine(project.Root, "ruleset-origin");
        ToolingTestProject.WriteValidRuleset(rulesetOrigin, "ruleset.marker");

        var docsOrigin = Path.Combine(project.Root, "docs-origin");
        WriteDocsStandard(docsOrigin, "¿Dónde existe?");

        var result = await project.Run(
            project.Root,
            "init",
            "--ruleset-origin", rulesetOrigin,
            "--docs-standard-origin", docsOrigin);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.RulesetRoot, "ruleset.marker")));
        Assert.True(File.Exists(Path.Combine(
            project.VslicesRoot,
            "docs-standard",
            "documents",
            "context-document.yml")));

        var configuration = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(configuration);
        Assert.Equal(rulesetOrigin, configuration!.RulesetSource);
        Assert.Null(configuration.RulesetRef);
        Assert.Equal(docsOrigin, configuration.DocsStandardSource);
        Assert.Null(configuration.DocsStandardRef);
    }

    [Fact]
    public async Task Historical_init_from_remains_a_ruleset_origin_compatibility_bridge()
    {
        using var project = new ToolingTestProject();

        var rulesetOrigin = Path.Combine(project.Root, "ruleset-origin");
        ToolingTestProject.WriteValidRuleset(rulesetOrigin, "ruleset.marker");

        var result = await project.Run(
            project.Root,
            "init",
            "--from", rulesetOrigin);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.RulesetRoot, "ruleset.marker")));

        var configuration = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(configuration);
        Assert.Equal(rulesetOrigin, configuration!.RulesetSource);
    }

    [Fact]
    public async Task Init_rejects_two_ruleset_origin_surfaces_for_the_same_operation()
    {
        using var project = new ToolingTestProject();

        var result = await project.Run(
            project.Root,
            "init",
            "--ruleset-origin", "vslices/ruleset:main",
            "--from", "https://github.com/vslices/ruleset");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("CLI023", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.VslicesRoot, "config.yaml")));
    }

    [Fact]
    public void GitHub_shorthand_origin_separates_repository_and_ref()
    {
        var result = ProjectOriginResolver.Parse(
            "vslices/docs-standard:feat/document-authoring-preview",
            ProjectOrigin.OfficialDocsStandard,
            "TEST001");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "https://github.com/vslices/docs-standard",
            result.Origin!.Source);
        Assert.Equal(
            "feat/document-authoring-preview",
            result.Origin.Reference);
    }

    [Fact]
    public void Relative_two_segment_path_is_not_reinterpreted_as_a_GitHub_repository()
    {
        var result = ProjectOriginResolver.Parse(
            "fixtures/ruleset",
            ProjectOrigin.OfficialRuleset,
            "TEST001");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("fixtures/ruleset", result.Origin!.Source);
        Assert.Null(result.Origin.Reference);
    }

    [Fact]
    public void Official_GitHub_URL_without_explicit_ref_uses_the_official_ref()
    {
        var result = ProjectOriginResolver.Parse(
            "https://github.com/vslices/ruleset",
            ProjectOrigin.OfficialRuleset,
            "TEST001");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            ProjectConfiguration.OfficialRulesetSource,
            result.Origin!.Source);
        Assert.Equal(
            ProjectConfiguration.OfficialRulesetRef,
            result.Origin.Reference);
    }

    private static void WriteDocsStandard(string root, string rootQuestion)
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

        File.WriteAllText(
            Path.Combine(documents, "context-document.yml"),
            $$"""
            kind: vslices-document-definition
            version: 0.1

            document:
              type: context
              scopes:
                - project
              question:
                id: context
                text: {{rootQuestion}}
            """);
    }
}
