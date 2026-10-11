using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling.Tests;

public sealed class NewDocumentCommandTests
{
    [Fact]
    public async Task New_document_materializes_promoted_identity_and_markerless_root_question()
    {
        using var project = new ToolingTestProject();
        await WriteDocumentAuthoringEnvironment(project);

        var result = await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling", "--scope", "project");

        Assert.Equal(0, result.ExitCode);
        var path = Path.Combine(project.Root, "tooling-context.md");
        Assert.True(File.Exists(path));
        var source = File.ReadAllText(path);
        Assert.Equal("# ¿Dónde existe?\n", ArtifactTestMetadata.Body(source));
        var metadata = ArtifactTestMetadata.Read(source);
        Assert.Equal("document", ArtifactTestMetadata.Scalar(metadata, "artifact", "kind"));
        Assert.Equal("context", ArtifactTestMetadata.Scalar(metadata, "artifact", "type"));
        Assert.Equal("project", ArtifactTestMetadata.Scalar(metadata, "artifact", "scope"));
        Assert.Equal("Tooling", ArtifactTestMetadata.Scalar(metadata, "artifact", "target"));
        Assert.Equal("draft", ArtifactTestMetadata.Scalar(metadata, "metadata", "status"));
        Assert.Empty(Assert.IsType<YamlSequenceNode>(ArtifactTestMetadata.Node(metadata, "metadata", "relates")).Children);
        Assert.Equal("0.1.0", ArtifactTestMetadata.Scalar(metadata, "tooling", "schema", "version"));
        Assert.Equal(ProjectConfiguration.DefaultDocumentTemplate, ArtifactTestMetadata.Scalar(metadata, "tooling", "template", "name"));
        Assert.Equal("0.1.0", ArtifactTestMetadata.Scalar(metadata, "tooling", "template", "version"));
        Assert.DoesNotContain("¿Qué estamos asumiendo como cierto?", source, StringComparison.Ordinal);
        Assert.DoesNotContain("document:\n  question:", source.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("vslices:placeholder", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_document_takes_the_root_question_from_the_installed_docs_standard()
    {
        using var project = new ToolingTestProject();
        await WriteDocumentAuthoringEnvironment(project, rootQuestion: "¿Dónde existe realmente?");
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.Equal(0, result.ExitCode);
        var source = File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"));
        Assert.Contains("# ¿Dónde existe realmente?", source, StringComparison.Ordinal);
        Assert.Contains("type: context", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_document_takes_root_heading_level_from_the_configured_template()
    {
        using var project = new ToolingTestProject();
        await WriteDocumentAuthoringEnvironment(project,
            configuredTemplate: "markdown.question-tree-h2", installedTemplate: "markdown.question-tree-h2", rootLevel: 2);
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.Equal(0, result.ExitCode);
        var source = File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"));
        Assert.Contains("## ¿Dónde existe?", source, StringComparison.Ordinal);
        Assert.DoesNotContain("# ¿Dónde existe?\n", source.Replace("## ¿Dónde existe?\n", string.Empty), StringComparison.Ordinal);
        Assert.Contains("type: context", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_document_uses_configured_route_for_a_bare_name()
    {
        using var project = new ToolingTestProject();
        await WriteDocumentAuthoringEnvironment(project, documentRoute: "docs/knowledge");
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.Root, "docs", "knowledge", "tooling-context.md")));
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
        var discovery = await project.Run(project.Root, "discovery", "document", "tooling-context");
        Assert.Equal(0, discovery.ExitCode);
        Assert.Contains("type: context", discovery.StandardOutput, StringComparison.Ordinal);
        var update = await project.Run(project.Root, "update", "document", "tooling-context",
            "--question-id", "1", "--answer", "Routed answer");
        Assert.Equal(0, update.ExitCode);
        Assert.Contains("Routed answer", File.ReadAllText(Path.Combine(project.Root, "docs", "knowledge", "tooling-context.md")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_document_path_bypasses_configured_route()
    {
        using var project = new ToolingTestProject();
        await WriteDocumentAuthoringEnvironment(project, documentRoute: "docs/knowledge");
        var result = await project.Run(project.Root,
            "new", "document", "explicit/tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.Root, "explicit", "tooling-context.md")));
        Assert.False(File.Exists(Path.Combine(project.Root, "docs", "knowledge", "explicit", "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_keeps_an_existing_md_extension()
    {
        using var project = new ToolingTestProject();
        await WriteDocumentAuthoringEnvironment(project);
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context.md", "--kind", "context", "--target", "Tooling");
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md.md")));
    }

    [Fact]
    public async Task New_document_rejects_unknown_kinds_without_creating_a_file()
    {
        using var project = new ToolingTestProject();
        await WriteDocumentAuthoringEnvironment(project);
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "unknown", "--target", "Tooling");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("NEW103", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_requires_an_installed_docs_standard_snapshot()
    {
        using var project = new ToolingTestProject();
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("NEW104", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_requires_a_vslices_project_configuration()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        WriteTemplateStandard(project.Root, ProjectConfiguration.DefaultDocumentTemplate, rootLevel: 1);
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCMAT001", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_requires_an_installed_template_standard_snapshot()
    {
        using var project = new ToolingTestProject();
        await ProjectConfiguration.WriteAsync(project.Root, ProjectConfiguration.Default(), CancellationToken.None);
        WriteDocsStandard(project.Root);
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCMAT002", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("vslices update template-standard", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_rejects_a_configured_template_that_is_not_installed()
    {
        using var project = new ToolingTestProject();
        await ProjectConfiguration.WriteAsync(project.Root,
            ProjectConfiguration.Default() with { DocumentsTemplate = "markdown.not-installed" }, CancellationToken.None);
        WriteDocsStandard(project.Root);
        WriteTemplateStandard(project.Root, ProjectConfiguration.DefaultDocumentTemplate, rootLevel: 1);
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCMAT004", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("markdown.not-installed", result.StandardError, StringComparison.Ordinal);
        Assert.Contains(ProjectConfiguration.DefaultDocumentTemplate, result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_fails_closed_when_the_selected_template_has_no_executable_question_presentation()
    {
        using var project = new ToolingTestProject();
        await ProjectConfiguration.WriteAsync(project.Root,
            ProjectConfiguration.Default() with { DocumentsTemplate = ProjectConfiguration.DefaultDocumentTemplate }, CancellationToken.None);
        WriteDocsStandard(project.Root);
        WriteTemplateStandardMetadataOnly(project.Root, ProjectConfiguration.DefaultDocumentTemplate);
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("TMPL102", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_does_not_overwrite_existing_markdown()
    {
        using var project = new ToolingTestProject();
        await WriteDocumentAuthoringEnvironment(project);
        var path = Path.Combine(project.Root, "tooling-context.md");
        File.WriteAllText(path, "sentinel");
        var result = await project.Run(project.Root,
            "new", "document", "tooling-context", "--kind", "context", "--target", "Tooling");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("sentinel", File.ReadAllText(path));
    }

    private static async Task WriteDocumentAuthoringEnvironment(
        ToolingTestProject project, string rootQuestion = "¿Dónde existe?",
        string configuredTemplate = ProjectConfiguration.DefaultDocumentTemplate,
        string? installedTemplate = null, int rootLevel = 1, string? documentRoute = null)
    {
        await ProjectConfiguration.WriteAsync(project.Root, ProjectConfiguration.Default() with
        {
            DocumentsTemplate = configuredTemplate,
            DocumentsRoute = documentRoute
        }, CancellationToken.None);
        WriteDocsStandard(project.Root, rootQuestion);
        WriteTemplateStandard(project.Root, installedTemplate ?? configuredTemplate, rootLevel);
    }

    private static void WriteDocsStandard(string projectRoot, string rootQuestion = "¿Dónde existe?")
    {
        var standardRoot = Path.Combine(projectRoot, ".vslices", "docs-standard");
        var documentsRoot = Path.Combine(standardRoot, "documents");
        Directory.CreateDirectory(documentsRoot);
        File.WriteAllText(Path.Combine(standardRoot, "manifest.yaml"), """
            kind: vslices-docs-standard
            version: 0.1
            documents:
              - documents/context-document.yml
            """);
        File.WriteAllText(Path.Combine(documentsRoot, "context-document.yml"), $$"""
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
            """);
    }

    private static void WriteTemplateStandard(string projectRoot, string templateId, int rootLevel)
    {
        var standardRoot = Path.Combine(projectRoot, ".vslices", "template-standard");
        var templatesRoot = Path.Combine(standardRoot, "templates", "markdown");
        Directory.CreateDirectory(templatesRoot);
        File.WriteAllText(Path.Combine(standardRoot, "manifest.yaml"), """
            kind: vslices-template-standard
            version: 0.1
            templates:
              - templates/markdown/question-tree.yaml
            """);
        File.WriteAllText(Path.Combine(templatesRoot, "question-tree.yaml"), $$"""
            kind: vslices-materialization-template
            version: 0.1

            template:
              id: {{templateId}}
              artifact-kind: document
              media-type: text/markdown

            representation:
              question:
                presentation:
                  kind: heading
                  text:
                    source: question.text
                  level:
                    strategy: semantic-depth
                    root: {{rootLevel}}
                answer:
                  region:
                    starts: after-question-heading
                    ends: before-next-materialized-question-heading
                  empty: unanswered

            reconstruction:
              question-identity:
                root:
                  strategy: document-type-root-question
            """);
    }

    private static void WriteTemplateStandardMetadataOnly(string projectRoot, string templateId)
    {
        var standardRoot = Path.Combine(projectRoot, ".vslices", "template-standard");
        var templatesRoot = Path.Combine(standardRoot, "templates", "markdown");
        Directory.CreateDirectory(templatesRoot);
        File.WriteAllText(Path.Combine(standardRoot, "manifest.yaml"), """
            kind: vslices-template-standard
            version: 0.1
            templates:
              - templates/markdown/question-tree.yaml
            """);
        File.WriteAllText(Path.Combine(templatesRoot, "question-tree.yaml"), $$"""
            kind: vslices-materialization-template
            version: 0.1

            template:
              id: {{templateId}}
              artifact-kind: document
              media-type: text/markdown
            """);
    }
}
