using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling.Tests;

public sealed class SupportNoteCommandTests
{
    [Fact]
    public async Task Draft_support_note_progressively_exposes_only_its_two_questions()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);

        var created = await Success(
            project,
            "new", "support-note", "first-submission",
            "--kind", "draft",
            "--target", "External payment request first submission");

        Assert.Contains("Created support-note", created, StringComparison.Ordinal);

        var metadata = Metadata(project, "first-submission");
        Assert.Equal("support-note", Value(metadata, "artifact", "kind"));
        Assert.Equal("draft", Value(metadata, "artifact", "type"));
        Assert.Equal("External payment request first submission", Value(metadata, "artifact", "target"));
        Assert.Equal("draft", Value(metadata, "metadata", "status"));
        Assert.Equal("support-note.question-tree", Value(metadata, "tooling", "template", "name"));

        var initial = await Success(project, "discovery", "support-note", "first-submission");
        Assert.Contains("[1] ¿Qué estamos esbozando?", initial, StringComparison.Ordinal);
        Assert.DoesNotContain("[1.1]", initial, StringComparison.Ordinal);

        var before = Read(project, "first-submission");
        var hidden = await Failure(
            project,
            "update", "support-note", "first-submission",
            "--question-id", "1.1",
            "--answer", "Still open");
        Assert.Contains("SUPART002", hidden, StringComparison.Ordinal);
        Assert.Equal(before, Read(project, "first-submission"));

        await Success(
            project,
            "update", "support-note", "first-submission",
            "--question-id", "1",
            "--answer", "Reconstructed external-user flow up to first submission.");

        var afterRoot = await Success(project, "discovery", "support-note", "first-submission");
        Assert.Contains("[1.1] ¿Qué sigue abierto?", afterRoot, StringComparison.Ordinal);
        Assert.Contains("parent: [1]", afterRoot, StringComparison.Ordinal);

        await Success(
            project,
            "update", "support-note", "first-submission",
            "--question-id", "1.1",
            "--answer", "Institutional confirmation of decree-specific variants.");

        var source = Read(project, "first-submission");
        Assert.Contains("Reconstructed external-user flow", source, StringComparison.Ordinal);
        Assert.Contains("Institutional confirmation", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Result_and_validation_can_be_related_without_becoming_a_mandatory_pair()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);

        await Success(
            project,
            "new", "support-note", "advance-observation",
            "--kind", "result",
            "--target", "Legacy advance validation");

        Assert.Empty(Relations(project, "advance-observation"));

        await Success(
            project,
            "new", "support-note", "advance-validation",
            "--kind", "validation",
            "--target", "Legacy advance validation",
            "--related-to", "advance-observation",
            "--role", "Interprets this observed result against the target criterion");

        var resultRelation = Assert.Single(Relations(project, "advance-observation"));
        var validationRelation = Assert.Single(Relations(project, "advance-validation"));

        Assert.Equal("support-note", Value(resultRelation, "kind"));
        Assert.Equal("validation", Value(resultRelation, "type"));
        Assert.Equal("advance-validation.md", Value(resultRelation, "target"));
        Assert.Equal("support-note", Value(validationRelation, "kind"));
        Assert.Equal("result", Value(validationRelation, "type"));
        Assert.Equal("advance-observation.md", Value(validationRelation, "target"));

        var resultDiscovery = await Success(project, "discovery", "support-note", "advance-observation");
        var validationDiscovery = await Success(project, "discovery", "support-note", "advance-validation");

        Assert.Contains("advance-validation.md", resultDiscovery, StringComparison.Ordinal);
        Assert.Contains("advance-observation.md", validationDiscovery, StringComparison.Ordinal);
        Assert.Contains("Interprets this observed result", resultDiscovery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Support_note_requires_a_known_type_and_target()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);

        Assert.Contains(
            "NEW105",
            await Failure(project, "new", "support-note", "missing-target", "--kind", "risk"),
            StringComparison.Ordinal);

        Assert.Contains(
            "NEWS006",
            await Failure(project, "new", "support-note", "unknown", "--kind", "invented", "--target", "Thing"),
            StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(project.Root, "missing-target.md")));
        Assert.False(File.Exists(Path.Combine(project.Root, "unknown.md")));
    }

    private static void WriteEnvironment(ToolingTestProject project)
    {
        ToolingTestProject.WriteDocumentAuthoringSupport(project.Root);

        var root = Path.Combine(project.Root, ".vslices", "docs-standard");
        var documents = Path.Combine(root, "documents");
        var notes = Path.Combine(root, "support-notes");
        Directory.CreateDirectory(documents);
        Directory.CreateDirectory(notes);

        File.WriteAllText(
            Path.Combine(root, "manifest.yaml"),
            """
            kind: vslices-docs-standard
            version: 0.1

            documents:
              - documents/context.yml

            support-notes:
              - support-notes/draft.yml
              - support-notes/result.yml
              - support-notes/validation.yml
              - support-notes/risk.yml
            """);

        File.WriteAllText(
            Path.Combine(documents, "context.yml"),
            """
            kind: vslices-document-definition
            version: 0.1
            document:
              type: context
              question:
                id: context
                text: Where does it exist?
            """);

        WriteDefinition(notes, "draft", "¿Qué estamos esbozando?", "open", "¿Qué sigue abierto?");
        WriteDefinition(notes, "result", "¿Qué obtuvimos?", "conditions", "¿Bajo qué condiciones lo obtuvimos?");
        WriteDefinition(notes, "validation", "¿Qué significa lo obtenido frente a un criterio?", "criterion", "¿Qué criterio estamos usando?");
        WriteDefinition(notes, "risk", "¿Qué podría salir mal?", "condition", "¿Bajo qué condición podría ocurrir?");
    }

    private static void WriteDefinition(
        string notes,
        string type,
        string rootQuestion,
        string childId,
        string childQuestion)
    {
        File.WriteAllText(
            Path.Combine(notes, type + ".yml"),
            $$"""
            kind: vslices-support-note-definition
            version: 0.1

            support-note:
              type: {{type}}
              question:
                id: {{type}}
                text: {{rootQuestion}}
                children:
                  - id: {{childId}}
                    text: {{childQuestion}}
            """);
    }

    private static string Read(ToolingTestProject project, string name) =>
        File.ReadAllText(Path.Combine(project.Root, name + ".md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static YamlMappingNode Metadata(ToolingTestProject project, string name) =>
        ArtifactTestMetadata.Read(Read(project, name));

    private static YamlMappingNode[] Relations(ToolingTestProject project, string name) =>
        Assert.IsType<YamlSequenceNode>(
                ArtifactTestMetadata.Node(Metadata(project, name), "metadata", "relates"))
            .Children
            .Select(node => Assert.IsType<YamlMappingNode>(node))
            .ToArray();

    private static string Value(YamlMappingNode mapping, params string[] path) =>
        ArtifactTestMetadata.Scalar(mapping, path);

    private static async Task<string> Success(
        ToolingTestProject project,
        params string[] arguments)
    {
        var result = await project.Run(project.Root, arguments);
        Assert.True(
            result.ExitCode == 0,
            $"{string.Join(' ', arguments)}\n{result.StandardError}\n{result.StandardOutput}");
        return result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static async Task<string> Failure(
        ToolingTestProject project,
        params string[] arguments)
    {
        var result = await project.Run(project.Root, arguments);
        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("Created ", result.StandardOutput, StringComparison.Ordinal);
        return result.StandardError;
    }
}
