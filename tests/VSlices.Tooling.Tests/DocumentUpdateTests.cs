namespace VSlices.Tooling.Tests;

public sealed class DocumentUpdateTests
{
    [Fact]
    public async Task Markerless_unanswered_root_becomes_a_markerless_answered_root()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        await CreateContextDocument(project);

        var result = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Existe dentro del experimento de authoring documental.");

        Assert.Equal(0, result.ExitCode);
        var source = ReadDocument(project);
        Assert.DoesNotContain("vslices:placeholder", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vslices:question question=context",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vslices:question document=context",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Existe dentro del experimento de authoring documental.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<!-- /vslices:question -->", source, StringComparison.Ordinal);
        Assert.DoesNotContain("¿Qué estamos asumiendo como cierto?", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answered_root_exposes_immediate_child_as_selection_two()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        await CreateContextDocument(project);

        var root = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Existe en Tooling.");
        Assert.Equal(0, root.ExitCode);

        var child = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Asumimos que el Docs Standard instalado es la autoridad normativa.");

        Assert.Equal(0, child.ExitCode);
        var source = ReadDocument(project);
        Assert.Contains("## ¿Qué estamos asumiendo como cierto?", source, StringComparison.Ordinal);
        Assert.Contains(
            "<!-- vslices:question question=assumptions -->",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Asumimos que el Docs Standard instalado es la autoridad normativa.",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_answer_can_be_replaced_without_mutating_child_answer()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        await CreateContextDocument(project);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Respuesta inicial.")).ExitCode);
        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Respuesta hija que debe conservarse.")).ExitCode);

        var updated = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Respuesta raíz revisada.");

        Assert.Equal(0, updated.ExitCode);
        var source = ReadDocument(project);
        Assert.Contains("Respuesta raíz revisada.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Respuesta inicial.", source, StringComparison.Ordinal);
        Assert.Contains("Respuesta hija que debe conservarse.", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "vslices:question question=context",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "<!-- vslices:question question=assumptions -->",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Child_is_not_writable_before_parent_is_answered()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        await CreateContextDocument(project);
        var before = ReadDocument(project);

        var result = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "No debería poder entrar todavía.");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("UPDATE105", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, ReadDocument(project));
    }

    [Fact]
    public async Task Deeper_question_enters_surface_only_after_its_parent_is_answered()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root, includeRisk: true);
        await CreateContextDocument(project);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Existe en Tooling.")).ExitCode);

        var tooEarly = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "3",
            "--answer", "Todavía no corresponde.");
        Assert.NotEqual(0, tooEarly.ExitCode);
        Assert.Contains("UPDATE105", tooEarly.StandardError, StringComparison.Ordinal);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Asumimos que la fuente normativa está instalada.")).ExitCode);

        var risk = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "3",
            "--answer", "Si cambia, discovery debe reconstruir otra superficie.");

        Assert.Equal(0, risk.ExitCode);
        var source = ReadDocument(project);
        Assert.Contains("### ¿Qué pasa si este supuesto cambia?", source, StringComparison.Ordinal);
        Assert.Contains("question=risk", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_child_uses_the_same_configured_template_depth_as_document_creation()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        ToolingTestProject.WriteDocumentAuthoringSupport(
            project.Root,
            templateId: "markdown.question-tree-h2",
            rootLevel: 2);

        await CreateContextDocument(project);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Root answer")).ExitCode);

        var child = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "Child answer");

        Assert.Equal(0, child.ExitCode);
        var source = ReadDocument(project);
        Assert.Contains("## ¿Dónde existe?", source, StringComparison.Ordinal);
        Assert.Contains("### ¿Qué estamos asumiendo como cierto?", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_child_wording_comes_from_current_docs_standard_without_recompiling_tooling()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        await CreateContextDocument(project);

        Assert.Equal(0, (await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Existe en Tooling.")).ExitCode);

        WriteDocsStandard(
            project.Root,
            childQuestion: "¿Qué supuesto sostiene esta respuesta?");

        var child = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "2",
            "--answer", "El estándar define el vocabulario.");

        Assert.Equal(0, child.ExitCode);
        Assert.Contains(
            "## ¿Qué supuesto sostiene esta respuesta?",
            ReadDocument(project),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_document_identity_markers_remain_readable_without_front_matter()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        var path = Path.Combine(project.Root, "tooling-context.md");
        File.WriteAllText(
            path,
            "# ¿Dónde existe?\n\n<!-- vslices:placeholder document=context question=context -->\n");

        var root = await project.Run(
            project.Root,
            "update", "document", "tooling-context",
            "--question-id", "1",
            "--answer", "Documento creado antes del front-matter.");

        Assert.Equal(0, root.ExitCode);
        var source = ReadDocument(project);
        Assert.Contains(
            "<!-- vslices:question document=context question=context -->",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("artifact:", source, StringComparison.Ordinal);

        var discovered = await project.Run(
            project.Root,
            "discovery", "document", "tooling-context");

        Assert.Equal(0, discovered.ExitCode);
        Assert.Contains("[2] ¿Qué estamos asumiendo como cierto?", discovered.StandardOutput, StringComparison.Ordinal);
    }

    private static async Task CreateContextDocument(ToolingTestProject project)
    {
        var created = await project.Run(
            project.Root,
            "new", "document", "tooling-context", "--kind", "context");
        Assert.Equal(0, created.ExitCode);
    }

    private static string ReadDocument(ToolingTestProject project) =>
        File.ReadAllText(Path.Combine(project.Root, "tooling-context.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void WriteDocsStandard(
        string projectRoot,
        string childQuestion = "¿Qué estamos asumiendo como cierto?",
        bool includeRisk = false)
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

        var risk = includeRisk
            ? "        children:\n          - id: risk\n            text: ¿Qué pasa si este supuesto cambia?\n"
            : string.Empty;

        File.WriteAllText(
            Path.Combine(documentsRoot, "context-document.yml"),
            "kind: vslices-document-definition\n" +
            "version: 0.1\n\n" +
            "document:\n" +
            "  type: context\n" +
            "  scopes:\n" +
            "    - concept\n" +
            "    - project\n" +
            "  question:\n" +
            "    id: context\n" +
            "    text: ¿Dónde existe?\n" +
            "    children:\n" +
            "      - id: assumptions\n" +
            $"        text: {childQuestion}\n" +
            risk);
    }
}
