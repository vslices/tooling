namespace VSlices.Tooling.Tests;

public sealed class SupportNoteDefinitionTests
{
    [Fact]
    public async Task Docs_standard_update_installs_manifest_promoted_support_notes()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();

        var source = Path.Combine(project.Root, "source-docs-standard");
        WriteStandard(source, threeQuestions: false);

        var result = await project.Run(
            project.Root,
            "update", "docs-standard", "--origin", source);

        Assert.Equal(0, result.ExitCode);

        var installed = Path.Combine(
            project.VslicesRoot,
            "docs-standard",
            "support-notes",
            "draft.yml");

        Assert.True(File.Exists(installed));
        Assert.Contains(
            "kind: vslices-support-note-definition",
            File.ReadAllText(installed),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Support_note_definition_with_more_than_two_questions_fails_closed()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();

        var source = Path.Combine(project.Root, "invalid-docs-standard");
        WriteStandard(source, threeQuestions: true);

        var result = await project.Run(
            project.Root,
            "update", "docs-standard", "--origin", source);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("SUPSTD020", result.StandardError, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(project.VslicesRoot, "docs-standard")));
    }

    private static void WriteStandard(string root, bool threeQuestions)
    {
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

        var grandchild = threeQuestions
            ? """
                      children:
                        - id: detail
                          text: What detail remains?
              """
            : string.Empty;

        File.WriteAllText(
            Path.Combine(notes, "draft.yml"),
            $$"""
            kind: vslices-support-note-definition
            version: 0.1

            support-note:
              type: draft
              question:
                id: draft
                text: What are we sketching?
                children:
                  - id: open
                    text: What remains open?
            {{grandchild}}
            """);
    }
}
