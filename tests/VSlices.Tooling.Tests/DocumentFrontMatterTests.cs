namespace VSlices.Tooling.Tests;

public sealed class DocumentFrontMatterTests
{
    [Fact]
    public async Task Front_matter_type_is_artifact_identity_even_when_visible_root_wording_changes()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root, rootQuestion: "¿En qué contexto existe?");
        File.WriteAllText(Path.Combine(project.Root, "context.md"), """
            ---
            artifact:
              kind: document
              type: context
            ---

            # Texto visible histórico
            """);
        var discovery = await project.Run(project.Root, "discovery", "document", "context");
        Assert.Equal(0, discovery.ExitCode);
        Assert.Contains("type: context", discovery.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[1] ¿En qué contexto existe?", discovery.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("[1] Texto visible histórico", discovery.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Markerless_root_with_significant_body_content_is_answered()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        var path = Path.Combine(project.Root, "context.md");
        File.WriteAllText(path, """
            ---
            artifact:
              kind: document
              type: context
            ---

            # ¿Dónde existe?

            Esto es contenido significativo y por tanto la respuesta de la raíz.
            """);
        var result = await project.Run(project.Root, "discovery", "document", "context");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[1] ¿Dónde existe?", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: answered", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[2] ¿Qué estamos asumiendo como cierto?", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: available", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("vslices:question", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Markerless_unanswered_root_must_match_configured_heading_geometry()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        File.WriteAllText(Path.Combine(project.Root, "context.md"), """
            ---
            artifact:
              kind: document
              type: context
            ---

            ## ¿Dónde existe?
            """);
        var result = await project.Run(project.Root, "discovery", "document", "context");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCART026", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Front_matter_and_legacy_marker_cannot_disagree_about_document_type()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        var path = Path.Combine(project.Root, "context.md");
        File.WriteAllText(path, """
            ---
            artifact:
              kind: document
              type: context
            ---

            # ¿Dónde existe?

            <!-- vslices:placeholder document=structure question=context -->
            """);
        var before = File.ReadAllText(path);
        var result = await project.Run(project.Root, "discovery", "document", "context");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCART002", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task Promoted_front_matter_fields_are_readable_without_becoming_document_answers()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        File.WriteAllText(Path.Combine(project.Root, "context.md"), """
            ---
            artifact:
              kind: document
              type: context
              scope: project
              target: Tooling
            metadata:
              status: draft
              relates: []
            tooling:
              version: historical
              schema:
                version: 0.1.0
              template:
                name: markdown.question-tree
                version: 0.1.0
            ---

            # ¿Dónde existe?
            """);
        var result = await project.Run(project.Root, "discovery", "document", "context");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("scope: project", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("target: Tooling", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: unanswered", result.StandardOutput, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("artifact:\n  kind: document\n  type: context\n  invented: hidden\n")]
    [InlineData("artifact:\n  kind: document\n  type: context\nmetadata:\n  relates: broken\n")]
    [InlineData("artifact:\n  kind: document\n  type: context\nmetadata:\n  relates:\n    - not-a-relation\n")]
    [InlineData("artifact:\n  kind: document\n  type: context\ntooling:\n  schema:\n    version: 99.0.0\n")]
    public async Task Unknown_or_malformed_promoted_metadata_still_fails_closed(string header)
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        var path = Path.Combine(project.Root, "context.md");
        File.WriteAllText(path, "---\n" + header + "---\n\n# ¿Dónde existe?\n");
        var before = File.ReadAllBytes(path);
        var result = await project.Run(project.Root, "discovery", "document", "context");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("RELART", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    private static void WriteDocsStandard(string projectRoot, string rootQuestion = "¿Dónde existe?")
    {
        ToolingTestProject.WriteDocumentAuthoringSupport(projectRoot);
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
}
