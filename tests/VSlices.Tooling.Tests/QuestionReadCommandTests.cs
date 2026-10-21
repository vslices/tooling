using static VSlices.Tooling.Tests.RelationalTestSupport;

namespace VSlices.Tooling.Tests;

public sealed class QuestionReadCommandTests
{
    [Fact]
    public async Task Document_read_selects_one_question_and_returns_complete_multiline_answer()
    {
        using var project = new ToolingTestProject();
        WriteDocumentEnvironment(project);
        await Success(project, "new", "document", "sistema", "--kind", "context", "--target", "Serviu");
        await Success(project, "update", "document", "sistema",
            "--question-id", "root", "--answer", "Una plataforma de gestión de solicitudes.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "1", "--answer", "Funcionarios y administradores.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "1.1", "--answer", "Crear y consultar solicitudes.\n\nTambién pueden revisar su estado.");

        var path = Path.Combine(project.Root, "sistema.md");
        var before = File.ReadAllBytes(path);
        var direct = await Success(project, "read", "document", "sistema",
            "--question-id", "1.1");
        Assert.Contains("Question: ¿Qué puede hacer un funcionario?", direct, StringComparison.Ordinal);
        Assert.Contains("Answer: Crear y consultar solicitudes.", direct, StringComparison.Ordinal);
        Assert.Contains("También pueden revisar su estado.", direct, StringComparison.Ordinal);
        Assert.DoesNotContain("¿Qué es el sistema?", direct, StringComparison.Ordinal);
        Assert.DoesNotContain("Funcionarios y administradores.", direct, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));

        var explicitPath = await Success(project, "read", "document", path, "--question-id", "1.1");
        Assert.Contains("Crear y consultar solicitudes.", explicitPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Document_deep_uses_parent_relationships_not_selection_prefixes()
    {
        using var project = new ToolingTestProject();
        WriteDocumentEnvironment(project);
        await Success(project, "new", "document", "sistema", "--kind", "context", "--target", "Serviu");
        await Success(project, "update", "document", "sistema",
            "--question-id", "root", "--answer", "Una plataforma de gestión de solicitudes.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "1", "--answer", "Funcionarios y administradores.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "1.1", "--answer", "Crear y consultar solicitudes.");

        var deep = await Success(project, "read", "document", "sistema",
            "--question-id", "1.1", "--deep");
        AssertInOrder(deep, "[root] ¿Qué es el sistema?", "[1] ¿Quién utiliza el sistema?",
            "[1.1] ¿Qué puede hacer un funcionario?");
        AssertInOrder(deep, "Una plataforma de gestión de solicitudes.",
            "Funcionarios y administradores.", "Crear y consultar solicitudes.");
        Assert.DoesNotContain("Question:", deep, StringComparison.Ordinal);

        var rootOnly = await Success(project, "read", "document", "sistema",
            "--question-id", "root", "--deep");
        Assert.Contains("[root] ¿Qué es el sistema?", rootOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("[1]", rootOnly, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repeated_document_answers_and_scoped_descendants_remain_distinct()
    {
        using var project = new ToolingTestProject();
        WriteDocumentEnvironment(project);
        await Success(project, "new", "document", "sistema", "--kind", "context", "--target", "Serviu");
        await Success(project, "update", "document", "sistema",
            "--question-id", "root", "--answer", "Gestión de solicitudes.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "1", "--answer", "Funcionarios\nCon cuentas habilitadas.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "1.1", "--answer", "Crear y consultar solicitudes.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "2", "--answer", "Administradores.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "2.1", "--answer", "Revisar permisos.");

        var first = await Success(project, "read", "document", "sistema",
            "--question-id", "1", "--deep");
        Assert.Contains("Funcionarios", first, StringComparison.Ordinal);
        Assert.Contains("Con cuentas habilitadas.", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Administradores.", first, StringComparison.Ordinal);

        var scoped = await Success(project, "read", "document", "sistema",
            "--question-id", "2.1", "--deep");
        AssertInOrder(scoped, "[root] ¿Qué es el sistema?", "[2] ¿Quién utiliza el sistema?",
            "[2.1] ¿Qué puede hacer un funcionario?");
        Assert.Contains("Administradores.", scoped, StringComparison.Ordinal);
        Assert.Contains("Revisar permisos.", scoped, StringComparison.Ordinal);
        Assert.DoesNotContain("Funcionarios\n", scoped, StringComparison.Ordinal);
        Assert.DoesNotContain("Crear y consultar solicitudes.", scoped, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_works_with_named_multi_standard_installations_and_qualified_artifact_types()
    {
        using var project = new ToolingTestProject();
        WriteDocumentEnvironment(project);
        var sourceRoot = Path.Combine(project.Root, "imported-vocabulary");
        Directory.Move(StandardRoot(project), sourceRoot);
        var manifest = Path.Combine(sourceRoot, "manifest.yaml");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace(
            "version: 0.1", "version: 0.1\nname: vslices", StringComparison.Ordinal));
        await Success(project, "new", "docs-standard", "--origin", sourceRoot);
        await Success(project, "new", "docs-standard", "serviu");
        Assert.True(File.Exists(Path.Combine(project.VslicesRoot, "docs", "vslices", "manifest.yaml")));

        await Success(project, "new", "document", "sistema", "--kind", "context", "--target", "Serviu");
        Assert.Contains("type: vslices.context", File.ReadAllText(Path.Combine(project.Root, "docs", "sistema.md")));
        await Success(project, "update", "document", "sistema",
            "--question-id", "root", "--answer", "Versión oficial.");
        await Success(project, "update", "document", "sistema",
            "--question-id", "1", "--answer", "Funcionarios.");

        var result = await Success(project, "read", "document", "sistema",
            "--question-id", "1", "--deep");
        AssertInOrder(result, "[root] ¿Qué es el sistema?", "[1] ¿Quién utiliza el sistema?");
        AssertInOrder(result, "Versión oficial.", "Funcionarios.");
    }

    [Fact]
    public async Task Unanswered_and_inaccessible_document_questions_are_not_invented()
    {
        using var project = new ToolingTestProject();
        WriteDocumentEnvironment(project);
        await Success(project, "new", "document", "sistema", "--kind", "context", "--target", "Serviu");
        var first = await Success(project, "read", "document", "sistema", "--question-id", "root");
        Assert.Contains("Question: ¿Qué es el sistema?", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Answer:", first, StringComparison.Ordinal);
        Assert.Equal(2, (await project.Run(project.Root,
            "read", "document", "sistema", "--question-id", "1.1")).ExitCode);
        var missingFlag = await project.Run(project.Root, "read", "document", "sistema");
        Assert.NotEqual(0, missingFlag.ExitCode);
        Assert.Contains("READ007", missingFlag.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("--path", first, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nexus_read_uses_question_surface_not_recommendation_slots()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "nexus", "perspectivas", "--kind", "detail", "--target", "Serviu");
        await Success(project, "update", "nexus", "perspectivas",
            "--question-id", "1", "--answer", "Perspectiva de funcionarios.");
        await Success(project, "update", "nexus", "perspectivas",
            "--question-id", "1.1", "--answer", "Límites operacionales.");
        var before = Read(project, "perspectivas");
        var selected = await Success(project, "read", "nexus", "perspectivas", "--question-id", "1.1");
        Assert.Contains("Question: What boundaries?", selected, StringComparison.Ordinal);
        Assert.Contains("Answer: Límites operacionales.", selected, StringComparison.Ordinal);
        Assert.DoesNotContain("Perspectiva de funcionarios.", selected, StringComparison.Ordinal);
        var deep = await Success(project, "read", "nexus", "perspectivas",
            "--question-id", "1.1", "--deep");
        AssertInOrder(deep, "[1] Which perspective?", "[1.1] What boundaries?");
        AssertInOrder(deep, "Perspectiva de funcionarios.", "Límites operacionales.");
        Assert.Equal(before, Read(project, "perspectivas"));

        await Success(project, "new", "nexus", "capacidad", "--kind", "capability", "--target", "Serviu");
        var recommendations = await project.Run(project.Root,
            "read", "nexus", "capacidad", "--question-id", "1");
        Assert.NotEqual(0, recommendations.ExitCode);
        Assert.Contains("READ008", recommendations.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Continuity_path_read_traces_graph_ancestors_and_can_read_purpose()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "continuity-path", "trayectoria",
            "--kind", "domain-context", "--target", "Serviu");
        await Success(project, "update", "continuity-path", "trayectoria",
            "--question-id", "3", "--answer", "Contexto de permisos.");
        await Success(project, "update", "continuity-path", "trayectoria",
            "--question-id", "3.1", "--answer", "Conocimiento del proceso.");
        await Success(project, "update", "continuity-path", "trayectoria",
            "--question-id", "3.1.2", "--answer", "Capacidad de autorización.");

        var selected = await Success(project, "read", "continuity-path", "trayectoria",
            "--question-id", "3.1.2");
        Assert.Contains("Question: Which capability composes it?", selected, StringComparison.Ordinal);
        Assert.Contains("Answer: Capacidad de autorización.", selected, StringComparison.Ordinal);
        var deep = await Success(project, "read", "continuity-path", "trayectoria",
            "--question-id", "3.1.2", "--deep");
        AssertInOrder(deep, "[3] Which context?", "[3.1] What \"knowledge\" matters?",
            "[3.1.2] Which capability composes it?");
        AssertInOrder(deep, "Contexto de permisos.", "Conocimiento del proceso.",
            "Capacidad de autorización.");
        Assert.DoesNotContain("[1] Propósito del recorrido", deep, StringComparison.Ordinal);
        var purpose = await Success(project, "read", "continuity-path", "trayectoria",
            "--question-id", "1");
        Assert.Contains("Answer: Preserve contextual continuity", purpose, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Support_note_uses_same_read_contract_with_optional_ancestors()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        var standard = StandardRoot(project);
        Directory.CreateDirectory(Path.Combine(standard, "support-notes"));
        File.AppendAllText(Path.Combine(standard, "manifest.yaml"),
            "\nsupport-notes:\n  - support-notes/draft.yml\n");
        File.WriteAllText(Path.Combine(standard, "support-notes", "draft.yml"), """
            kind: vslices-support-note-definition
            version: 0.1
            support-note:
              type: draft
              question:
                id: draft
                text: What are we drafting?
                children:
                  - id: open
                    text: What remains open?
            """);
        await Success(project, "new", "support-note", "first-submission",
            "--kind", "draft", "--target", "Serviu");
        await Success(project, "update", "support-note", "first-submission",
            "--question-id", "1", "--answer", "A request workflow.");
        await Success(project, "update", "support-note", "first-submission",
            "--question-id", "1.1", "--answer", "Authentication details.");
        var deep = await Success(project, "read", "support-note", "first-submission",
            "--question-id", "1.1", "--deep");
        AssertInOrder(deep, "[1] What are we drafting?", "[1.1] What remains open?");
        AssertInOrder(deep, "A request workflow.", "Authentication details.");
    }

    [Fact]
    public async Task Read_rejects_wrong_artifact_kind_and_missing_files()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "nexus", "perspectivas", "--kind", "detail", "--target", "Serviu");
        var wrong = await project.Run(project.Root, "read", "continuity-path",
            "perspectivas", "--question-id", "1");
        Assert.NotEqual(0, wrong.ExitCode);
        Assert.Contains("READ002", wrong.StandardError, StringComparison.Ordinal);
        var absent = await project.Run(project.Root, "read", "document",
            "missing-document", "--question-id", "root");
        Assert.NotEqual(0, absent.ExitCode);
        Assert.Contains("READ001", absent.StandardError, StringComparison.Ordinal);
    }

    private static void WriteDocumentEnvironment(ToolingTestProject project)
    {
        WriteEnvironment(project);
        File.WriteAllText(Path.Combine(StandardRoot(project), "documents", "context.yml"), """
            kind: vslices-document-definition
            version: 0.1
            document:
              type: context
              scopes: [project, concept]
              question:
                id: system
                text: ¿Qué es el sistema?
                children:
                  - id: users
                    text: ¿Quién utiliza el sistema?
                    cardinality: many
                    children:
                      - id: activities
                        text: ¿Qué puede hacer un funcionario?
            """);
    }

    private static void AssertInOrder(string output, params string[] sections)
    {
        var previous = -1;
        foreach (var section in sections)
        {
            var current = output.IndexOf(section, StringComparison.Ordinal);
            Assert.True(current > previous,
                $"Expected '{section}' after position {previous}. Actual output:\n{output}");
            previous = current;
        }
    }
}
