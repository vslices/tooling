namespace VSlices.Tooling.Tests;

public sealed class NewDocumentCommandTests
{
    [Fact]
    public async Task New_document_materializes_only_the_root_question_and_placeholder()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        var result = await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context");

        Assert.Equal(0, result.ExitCode);
        var path = Path.Combine(project.Root, "tooling-context.md");
        Assert.True(File.Exists(path));
        Assert.Equal(
            "# ¿Dónde existe?\n\n<!-- vslices:placeholder document=context question=context -->\n",
            File.ReadAllText(path).Replace("\r\n", "\n"));
        Assert.DoesNotContain("¿Qué estamos asumiendo como cierto?", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_document_takes_the_root_question_from_the_installed_standard()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root, rootQuestion: "¿Dónde existe realmente?");

        var result = await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context");

        Assert.Equal(0, result.ExitCode);
        var source = File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"));
        Assert.Contains("# ¿Dónde existe realmente?", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_document_keeps_an_existing_md_extension()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        var result = await project.Run(
            project.Root,
            "new", "document", "tooling-context.md", "--kind", "context");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md.md")));
    }

    [Fact]
    public async Task New_document_rejects_unknown_kinds_without_creating_a_file()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        var result = await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "unknown");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("NEW103", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_requires_an_installed_docs_standard_snapshot()
    {
        using var project = new ToolingTestProject();

        var result = await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("NEW104", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "tooling-context.md")));
    }

    [Fact]
    public async Task New_document_does_not_overwrite_existing_markdown()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        var path = Path.Combine(project.Root, "tooling-context.md");
        File.WriteAllText(path, "sentinel");

        var result = await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("sentinel", File.ReadAllText(path));
    }

    private static void WriteDocsStandard(
        string projectRoot,
        string rootQuestion = "¿Dónde existe?")
    {
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
