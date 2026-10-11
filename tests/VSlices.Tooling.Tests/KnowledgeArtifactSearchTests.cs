using YamlDotNet.RepresentationModel;
using static VSlices.Tooling.Tests.RelationalTestSupport;

namespace VSlices.Tooling.Tests;

public sealed class KnowledgeArtifactSearchTests
{
    [Fact]
    public async Task Tags_are_authorable_and_searchable_across_all_knowledge_artifact_families()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        AddSupportNotes(project);

        await Success(
            project,
            "new", "document", "context",
            "--kind", "context",
            "--target", "External payment requests",
            "--tags", "serviu,payment-request");

        await Success(
            project,
            "new", "support-note", "note",
            "--kind", "draft",
            "--target", "External payment requests",
            "--tags", "serviu,payment-request");

        await Success(
            project,
            "new", "nexus", "capability",
            "--kind", "capability",
            "--target", "External payment requests",
            "--tags", "serviu,composition");

        await Success(
            project,
            "new", "continuity-path", "journey",
            "--kind", "domain-context",
            "--target", "External payment requests",
            "--tags", "serviu,continuity");

        File.WriteAllText(
            Path.Combine(project.Root, "Legacy.vsir"),
            """
            vsir: 0.1
            kind: domain-type
            name: Legacy
            tags: [serviu, legacy]
            """);

        var all = await Success(
            project,
            "search",
            "--filter", "tags:contains:serviu");

        AssertSearchResults(
            all,
            "Legacy.vsir",
            "capability.md",
            "context.md",
            "journey.md",
            "note.md");

        AssertSearchResults(
            await Success(project, "search", "--filter", "kind:equals:document"),
            "context.md");
        AssertSearchResults(
            await Success(project, "search", "--filter", "kind:equals:support-note"),
            "note.md");
        AssertSearchResults(
            await Success(project, "search", "--filter", "kind:equals:nexus"),
            "capability.md");
        AssertSearchResults(
            await Success(project, "search", "--filter", "kind:equals:continuity-path"),
            "journey.md");

        await Success(project, "update", "document", "context", "--add-tags", "migration");
        await Success(project, "update", "support-note", "note", "--add-tags", "migration");
        await Success(project, "update", "nexus", "capability", "--add-tags", "migration");
        await Success(project, "update", "continuity-path", "journey", "--add-tags", "migration");

        var migration = await Success(
            project,
            "search",
            "--filter", "tags:contains:migration");

        AssertSearchResults(
            migration,
            "capability.md",
            "context.md",
            "journey.md",
            "note.md");

        await Success(
            project,
            "update", "document", "context",
            "--tags", "document-only,serviu");
        await Success(
            project,
            "update", "nexus", "capability",
            "--remove-tags", "serviu,composition");

        AssertTags(project, "context", "document-only", "serviu");
        AssertTags(project, "capability", "migration");

        var documentDiscovery = await Success(project, "discovery", "document", "context");
        var noteDiscovery = await Success(project, "discovery", "support-note", "note");
        var nexusDiscovery = await Success(project, "discovery", "nexus", "capability");
        var pathDiscovery = await Success(project, "discovery", "continuity-path", "journey");

        foreach (var discovery in new[] { documentDiscovery, noteDiscovery, nexusDiscovery, pathDiscovery })
        {
            Assert.Contains("tag commands:", discovery, StringComparison.Ordinal);
            Assert.Contains("--add-tags", discovery, StringComparison.Ordinal);
            Assert.Contains("--remove-tags", discovery, StringComparison.Ordinal);
        }

        // Installed standards are implementation context, not searchable project knowledge.
        Assert.DoesNotContain(".vslices", all, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tag_updates_preserve_existing_artifact_relations()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        AddSupportNotes(project);

        await Success(
            project,
            "new", "support-note", "observed",
            "--kind", "result",
            "--target", "Legacy advances",
            "--tags", "advance");

        await Success(
            project,
            "new", "support-note", "validated",
            "--kind", "validation",
            "--target", "Legacy advances",
            "--related-to", "observed",
            "--role", "Interprets the observed result");

        await Success(
            project,
            "update", "support-note", "observed",
            "--add-tags", "legacy");

        var outgoing = Assert.Single(Relations(project, "observed"));
        var incoming = Assert.Single(Relations(project, "validated"));

        Assert.Equal("validated.md", Value(outgoing, "target"));
        Assert.Equal("observed.md", Value(incoming, "target"));
        AssertTags(project, "observed", "advance", "legacy");
    }

    private static void AddSupportNotes(ToolingTestProject project)
    {
        var root = StandardRoot(project);
        var notes = Path.Combine(root, "support-notes");
        Directory.CreateDirectory(notes);

        File.AppendAllText(
            Path.Combine(root, "manifest.yaml"),
            """

            support-notes:
              - support-notes/draft.yml
              - support-notes/result.yml
              - support-notes/validation.yml
              - support-notes/risk.yml
            """);

        WriteSupportNote(notes, "draft", "What are we sketching?", "open", "What remains open?");
        WriteSupportNote(notes, "result", "What did we obtain?", "conditions", "Under what conditions?");
        WriteSupportNote(notes, "validation", "What does the result mean against a criterion?", "criterion", "Which criterion?");
        WriteSupportNote(notes, "risk", "What could go wrong?", "condition", "Under what condition?");
    }

    private static void WriteSupportNote(
        string root,
        string type,
        string question,
        string childId,
        string child)
    {
        File.WriteAllText(
            Path.Combine(root, type + ".yml"),
            $$"""
            kind: vslices-support-note-definition
            version: 0.1

            support-note:
              type: {{type}}
              question:
                id: {{type}}
                text: {{question}}
                children:
                  - id: {{childId}}
                    text: {{child}}
            """);
    }

    private static void AssertTags(
        ToolingTestProject project,
        string name,
        params string[] expected)
    {
        var metadata = Metadata(project, name);
        var tags = Assert.IsType<YamlSequenceNode>(
            ArtifactTestMetadata.Node(metadata, "metadata", "tags"))
            .Children
            .Select(node => Assert.IsType<YamlScalarNode>(node).Value!)
            .ToArray();

        Assert.Equal(expected, tags);
    }

    private static void AssertSearchResults(
        string output,
        params string[] expected)
    {
        var lines = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(expected.OrderBy(value => value, StringComparer.Ordinal), lines);
    }
}
