namespace VSlices.Tooling.Tests;

public sealed class DocumentDiscoveryTests
{
    [Fact]
    public async Task Discovery_initially_exposes_only_the_unanswered_root()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        var created = await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context");
        Assert.Equal(0, created.ExitCode);
        Assert.DoesNotContain(
            "vslices:placeholder",
            File.ReadAllText(Path.Combine(project.Root, "tooling-context.md")),
            StringComparison.Ordinal);

        var result = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[1] ¿Dónde existe?", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: unanswered", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "vslices update document tooling-context --question-id 1 --answer \"<answer>\"",
            result.StandardOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain("¿Qué estamos asumiendo como cierto?", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discovery_after_answering_root_exposes_root_and_immediate_child_with_update_compatible_ids()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);

        var sourceAfterRoot = File.ReadAllText(
            Path.Combine(project.Root, "tooling-context.md"));
        Assert.DoesNotContain(
            "vslices:question question=context",
            sourceAfterRoot,
            StringComparison.Ordinal);

        var discovered = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, discovered.ExitCode);
        Assert.Contains("[1] ¿Dónde existe?", discovered.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: answered", discovered.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[2] ¿Qué estamos asumiendo como cierto?", discovered.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: available", discovered.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "vslices update document tooling-context --question-id 2 --answer \"<answer>\"",
            discovered.StandardOutput,
            StringComparison.Ordinal);

        var updated = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Assumption answer");

        Assert.Equal(0, updated.ExitCode);
        var source = File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"));
        Assert.Contains("## ¿Qué estamos asumiendo como cierto?", source, StringComparison.Ordinal);
        Assert.Contains("Assumption answer", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discovery_reveals_a_grandchild_only_after_its_external_parent_is_answered()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root, includeGrandchild: true);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);

        var beforeParent = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, beforeParent.ExitCode);
        Assert.DoesNotContain("¿Qué pasa si este supuesto cambia?", beforeParent.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Assumption answer")).ExitCode);

        var afterParent = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, afterParent.ExitCode);
        Assert.Contains("[3] ¿Qué pasa si este supuesto cambia?", afterParent.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "vslices update document tooling-context --question-id 3 --answer \"<answer>\"",
            afterParent.StandardOutput,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discovery_exposes_many_cardinality_without_claiming_multiple_answer_authoring()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root, childCardinality: "many");

        Assert.Equal(0, (await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);

        var discovered = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, discovered.ExitCode);
        Assert.Contains("[1] ¿Dónde existe?", discovered.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cardinality: one", discovered.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[2] ¿Qué estamos asumiendo como cierto?", discovered.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cardinality: many", discovered.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "multiple-answer authoring is not supported in the current preview",
            discovered.StandardOutput,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vslices update document tooling-context --question-id 2 --answer",
            discovered.StandardOutput,
            StringComparison.Ordinal);

        var updateMany = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "First repeated answer");

        Assert.NotEqual(0, updateMany.ExitCode);
        Assert.Contains("UPDATE109", updateMany.StandardError, StringComparison.Ordinal);
        Assert.Contains("cardinality 'many'", updateMany.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discovery_uses_question_wording_from_the_installed_standard()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root, childQuestion: "¿Qué estamos presuponiendo?");

        Assert.Equal(0, (await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);

        var result = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[2] ¿Qué estamos presuponiendo?", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("¿Qué estamos asumiendo como cierto?", result.StandardOutput, StringComparison.Ordinal);
    }

    private static void WriteDocsStandard(
        string projectRoot,
        bool includeGrandchild = false,
        string childQuestion = "¿Qué estamos asumiendo como cierto?",
        string? childCardinality = null)
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

        var cardinality = string.IsNullOrWhiteSpace(childCardinality)
            ? string.Empty
            : $"        cardinality: {childCardinality}\n";
        var grandchild = includeGrandchild
            ? "        children:\n          - id: assumption-risk\n            text: ¿Qué pasa si este supuesto cambia?\n"
            : string.Empty;

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
                text: ¿Dónde existe?
                children:
                  - id: assumptions
                    text: {{childQuestion}}
            {{cardinality}}{{grandchild}}
            """);
    }
}
