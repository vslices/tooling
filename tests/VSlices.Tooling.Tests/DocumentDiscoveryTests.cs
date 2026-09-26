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
    public async Task Many_question_materializes_repeated_answer_instances_and_remains_available()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(
            project.Root,
            includeGrandchild: true,
            childCardinality: "many");

        Assert.Equal(0, (await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);

        var before = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, before.ExitCode);
        Assert.Contains("[2] ¿Qué estamos asumiendo como cierto?", before.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cardinality: many", before.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "vslices update document tooling-context --question-id 2 --answer \"<answer>\"",
            before.StandardOutput,
            StringComparison.Ordinal);

        var first = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Account");

        Assert.Equal(0, first.ExitCode);

        var afterFirst = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, afterFirst.ExitCode);
        Assert.Contains("[2] ¿Qué estamos asumiendo como cierto?", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: answered", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("instance: answer-", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("text: Account", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("sub-questions: [3]", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[3] ¿Qué pasa si este supuesto cambia?", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("from:", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("text: Account", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[4] ¿Qué estamos asumiendo como cierto?", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: available", afterFirst.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "vslices update document tooling-context --question-id 4 --answer \"<answer>\"",
            afterFirst.StandardOutput,
            StringComparison.Ordinal);

        var second = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "4",
            "--answer", "Service");

        Assert.Equal(0, second.ExitCode);

        var reconstructed = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, reconstructed.ExitCode);
        Assert.Contains("text: Account", reconstructed.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("text: Service", reconstructed.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[3] ¿Qué pasa si este supuesto cambia?", reconstructed.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("text: Account", reconstructed.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[5] ¿Qué pasa si este supuesto cambia?", reconstructed.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("text: Service", reconstructed.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[6] ¿Qué estamos asumiendo como cierto?", reconstructed.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: available", reconstructed.StandardOutput, StringComparison.Ordinal);

        var source = File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"));
        Assert.Contains("vslices:answer-instance question=assumptions id=answer-", source, StringComparison.Ordinal);
        Assert.Equal(
            2,
            source.Split("<!-- vslices:answer-instance question=assumptions ", StringSplitOptions.None).Length - 1);
        Assert.Contains("Account", source, StringComparison.Ordinal);
        Assert.Contains("Service", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Child_question_is_scoped_to_one_repeated_answer_instance()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(
            project.Root,
            includeGrandchild: true,
            childCardinality: "many");

        Assert.Equal(0, (await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Account")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "4",
            "--answer", "Service")).ExitCode);

        var before = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, before.ExitCode);
        Assert.Contains("[3] ¿Qué pasa si este supuesto cambia?", before.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("text: Account", before.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[5] ¿Qué pasa si este supuesto cambia?", before.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("text: Service", before.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "3",
            "--answer", "Account-specific risk")).ExitCode);

        var after = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, after.ExitCode);

        var accountChild = SliceQuestion(after.StandardOutput, 3);
        var serviceChild = SliceQuestion(after.StandardOutput, 5);

        Assert.Contains("status: answered", accountChild, StringComparison.Ordinal);
        Assert.Contains("text: Account", accountChild, StringComparison.Ordinal);
        Assert.Contains("status: available", serviceChild, StringComparison.Ordinal);
        Assert.Contains("text: Service", serviceChild, StringComparison.Ordinal);

        var source = File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"));
        Assert.Contains(
            "vslices:scoped-question question=assumption-risk parent-answer-instance=answer-",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Account-specific risk", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnswerInstance_scope_flows_through_deeper_descendants()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(
            project.Root,
            includeGrandchild: true,
            includeGreatGrandchild: true,
            childCardinality: "many");

        Assert.Equal(0, (await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Account")).ExitCode);

        var beforeChild = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, beforeChild.ExitCode);
        Assert.Contains("sub-questions: [3]", beforeChild.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "3",
            "--answer", "Account identity")).ExitCode);

        var afterChild = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, afterChild.ExitCode);
        Assert.Contains("[4] ¿Cómo se expresa este supuesto?", afterChild.StandardOutput, StringComparison.Ordinal);

        var deeper = SliceQuestion(afterChild.StandardOutput, 4);
        Assert.Contains("status: available", deeper, StringComparison.Ordinal);
        Assert.Contains("from:", deeper, StringComparison.Ordinal);
        Assert.Contains("text: Account", deeper, StringComparison.Ordinal);

        var accountInstanceLine = afterChild.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.TrimStart().StartsWith("instance: answer-", StringComparison.Ordinal))
            .Trim();
        var accountInstanceId = accountInstanceLine["instance: ".Length..];

        Assert.Contains(
            $"instance: {accountInstanceId}",
            deeper,
            StringComparison.Ordinal);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "4",
            "--answer", "Preferred account wording")).ExitCode);

        var reconstructed = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, reconstructed.ExitCode);
        var reconstructedDeeper = SliceQuestion(reconstructed.StandardOutput, 4);
        Assert.Contains("status: answered", reconstructedDeeper, StringComparison.Ordinal);
        Assert.Contains("text: Account", reconstructedDeeper, StringComparison.Ordinal);

        var source = File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"));
        Assert.Contains(
            $"vslices:scoped-question question=assumption-expression parent-answer-instance={accountInstanceId}",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Preferred account wording", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nested_many_answers_are_scoped_to_their_outer_answer_instance()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(
            project.Root,
            includeGrandchild: true,
            includeGreatGrandchild: true,
            greatGrandchildCardinality: "many",
            childCardinality: "many");

        Assert.Equal(0, (await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Account")).ExitCode);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "3",
            "--answer", "Account identity")).ExitCode);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "4",
            "--answer", "Rut")).ExitCode);

        var afterAccountProperty = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, afterAccountProperty.ExitCode);
        var accountNested = SliceQuestion(afterAccountProperty.StandardOutput, 4);
        Assert.Contains("status: answered", accountNested, StringComparison.Ordinal);
        Assert.Contains("text: Rut", accountNested, StringComparison.Ordinal);
        Assert.Contains("from:", accountNested, StringComparison.Ordinal);
        Assert.Contains("text: Account", accountNested, StringComparison.Ordinal);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "6",
            "--answer", "Service")).ExitCode);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "7",
            "--answer", "Service identity")).ExitCode);

        var beforeServiceProperty = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, beforeServiceProperty.ExitCode);
        var serviceNestedAvailable = SliceQuestion(beforeServiceProperty.StandardOutput, 8);
        Assert.Contains("status: available", serviceNestedAvailable, StringComparison.Ordinal);
        Assert.Contains("text: Service", serviceNestedAvailable, StringComparison.Ordinal);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "8",
            "--answer", "Endpoint")).ExitCode);

        var reconstructed = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, reconstructed.ExitCode);
        Assert.Contains("text: Rut", reconstructed.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("text: Endpoint", reconstructed.StandardOutput, StringComparison.Ordinal);

        var source = File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"));
        Assert.Contains(
            "vslices:answer-instance question=assumption-expression id=answer-",
            source,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            source.Split(
                "<!-- vslices:answer-instance question=assumption-expression ",
                StringSplitOptions.None).Length - 1);
        Assert.Equal(
            2,
            source
                .Split('\n')
                .Count(line =>
                    line.Contains(
                        "vslices:answer-instance question=assumption-expression",
                        StringComparison.Ordinal) &&
                    line.Contains(
                        " parent-answer-instance=answer-",
                        StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Repeated_answer_instance_identity_does_not_derive_from_answer_text()
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

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Account")).ExitCode);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "3",
            "--answer", "Account")).ExitCode);

        var discovered = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, discovered.ExitCode);
        Assert.Equal(
            2,
            discovered.StandardOutput.Split("text: Account", StringSplitOptions.None).Length - 1);

        var instanceLines = discovered.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.TrimStart().StartsWith("instance: answer-", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .ToArray();

        Assert.Equal(2, instanceLines.Length);
        Assert.NotEqual(instanceLines[0], instanceLines[1]);
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

    private static string SliceQuestion(string output, int selection)
    {
        var marker = $"[{selection}] ";
        var start = output.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Selection [{selection}] was not found in discovery output.");

        var next = output.IndexOf(
            Environment.NewLine + "[",
            start + marker.Length,
            StringComparison.Ordinal);

        return next < 0
            ? output[start..]
            : output[start..next];
    }

    private static void WriteDocsStandard(
        string projectRoot,
        bool includeGrandchild = false,
        bool includeGreatGrandchild = false,
        string childQuestion = "¿Qué estamos asumiendo como cierto?",
        string? childCardinality = null,
        string? greatGrandchildCardinality = null)
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
        var greatGrandchildCardinalityLine = string.IsNullOrWhiteSpace(greatGrandchildCardinality)
            ? string.Empty
            : $"                cardinality: {greatGrandchildCardinality}\n";
        var greatGrandchild = includeGreatGrandchild
            ? "            children:\n              - id: assumption-expression\n                text: ¿Cómo se expresa este supuesto?\n" +
              greatGrandchildCardinalityLine
            : string.Empty;
        var grandchild = includeGrandchild
            ? "        children:\n          - id: assumption-risk\n            text: ¿Qué pasa si este supuesto cambia?\n" + greatGrandchild
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
