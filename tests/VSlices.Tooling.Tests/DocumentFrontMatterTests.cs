namespace VSlices.Tooling.Tests;

public sealed class DocumentFrontMatterTests
{
    [Fact]
    public async Task Front_matter_type_is_artifact_identity_even_when_visible_root_wording_changes()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root, rootQuestion: "¿En qué contexto existe?");

        var path = Path.Combine(project.Root, "context.md");
        File.WriteAllText(
            path,
            """
            ---
            artifact:
              kind: document
              type: context
            ---

            # Texto visible histórico
            """);

        var discovery = await project.Run(
            project.Root,
            "discovery", "document", "context");

        Assert.Equal(0, discovery.ExitCode);
        Assert.Contains("type: context", discovery.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[1] ¿En qué contexto existe?", discovery.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("[1] Texto visible histórico", discovery.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Markerless_unanswered_root_rejects_significant_body_content()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        var path = Path.Combine(project.Root, "context.md");
        File.WriteAllText(
            path,
            """
            ---
            artifact:
              kind: document
              type: context
            ---

            # ¿Dónde existe?

            Esto ya es contenido significativo, pero todavía no está materializado como una respuesta.
            """);

        var result = await project.Run(
            project.Root,
            "discovery", "document", "context");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCART025", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Markerless_unanswered_root_must_match_configured_heading_geometry()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        var path = Path.Combine(project.Root, "context.md");
        File.WriteAllText(
            path,
            """
            ---
            artifact:
              kind: document
              type: context
            ---

            ## ¿Dónde existe?
            """);

        var result = await project.Run(
            project.Root,
            "discovery", "document", "context");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCART026", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Front_matter_and_legacy_marker_cannot_disagree_about_document_type()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        var path = Path.Combine(project.Root, "context.md");
        File.WriteAllText(
            path,
            """
            ---
            artifact:
              kind: document
              type: context
            ---

            # ¿Dónde existe?

            <!-- vslices:placeholder document=structure question=context -->
            """);

        var before = File.ReadAllText(path);
        var result = await project.Run(
            project.Root,
            "discovery", "document", "context");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCART002", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task Unsupported_front_matter_fields_fail_closed_until_their_semantics_are_promoted()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        var path = Path.Combine(project.Root, "context.md");
        File.WriteAllText(
            path,
            """
            ---
            artifact:
              kind: document
              type: context
              scope: project
            ---

            # ¿Dónde existe?

            <!-- vslices:placeholder question=context -->
            """);

        var result = await project.Run(
            project.Root,
            "discovery", "document", "context");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCART021", result.StandardError, StringComparison.Ordinal);
    }

    private static void WriteDocsStandard(
        string projectRoot,
        string rootQuestion = "¿Dónde existe?")
    {
        ToolingTestProject.WriteDocumentAuthoringSupport(projectRoot);

        var standardRoot = Path.Combine(projectRoot, ".vslices", "docs-standard");
        var documentsRoot = Path.Combine(standardRoot, "documents");
        Directory.CreateDirectory(documentsRoot);

        File.WriteAllText(
            Path.Combine(standardRoot, "manifest.yaml"),
            """
            kind: vslices-docs-standard
            version: 0.1
            documents:
              - documents/context-document.yml
            """);

        File.WriteAllText(
            Path.Combine(documentsRoot, "context-document.yml"),
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
            """);
    }
}
