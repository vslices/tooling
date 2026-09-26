using YamlDotNet.RepresentationModel;
using static VSlices.Tooling.Tests.RelationalTestSupport;

namespace VSlices.Tooling.Tests;

public sealed class RelationalArtifactCommandTests
{
    [Fact]
    public async Task Zero_question_nexus_supports_both_recommendation_families_and_bidirectional_discovery()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "nexus", "origin", "--kind", "capability", "--target", "Tooling");
        var initial = Read(project, "origin");
        Assert.Contains("## Artifacts asociados", initial, StringComparison.Ordinal);
        Assert.DoesNotContain("artifact-question", initial, StringComparison.Ordinal);
        Assert.Equal("project", Value(Metadata(project, "origin"), "artifact", "scope"));
        Assert.Equal("draft", Value(Metadata(project, "origin"), "metadata", "status"));
        Assert.Empty(Relations(project, "origin"));
        var discovery = await Success(project, "discovery", "nexus", "origin");
        var available = Selection(discovery, "1");
        Assert.Contains("status: available", available, StringComparison.Ordinal);
        Assert.Contains("vslices new document --from-nexus origin:1", available, StringComparison.Ordinal);
        Assert.DoesNotContain("vslices discovery", available, StringComparison.Ordinal);

        await Success(project, "new", "document", "context", "--from-nexus", "origin:1");
        Assert.Equal("Tooling", Value(Metadata(project, "context"), "artifact", "target"));
        Assert.Equal("project", Value(Metadata(project, "context"), "artifact", "scope"));
        var outgoing = Assert.Single(Relations(project, "origin"));
        var incoming = Assert.Single(Relations(project, "context"));
        Assert.Equal("context.md", Value(outgoing, "target"));
        Assert.Equal("origin.md", Value(incoming, "target"));
        Assert.Equal("Explains the context", Value(outgoing, "role"));
        Assert.Equal("Explains the context", Value(incoming, "role"));
        Assert.Equal("1", Value(outgoing, "recommendation"));
        Assert.Equal(64, Value(outgoing, "recommendation-context").Length);
        Assert.False(incoming.Children.ContainsKey(new YamlScalarNode("recommendation")));
        var created = Selection(await Success(project, "discovery", "nexus", "origin"), "1");
        Assert.Contains("status: created", created, StringComparison.Ordinal);
        Assert.Contains("vslices discovery document", created, StringComparison.Ordinal);
        Assert.DoesNotContain("vslices new", created, StringComparison.Ordinal);
        Assert.Contains("origin.md", await Success(project, "discovery", "document", "context"), StringComparison.Ordinal);

        await Success(project, "new", "nexus", "nested", "--from-nexus", "origin:2", "--target", "Specific target", "--role", "Concrete perspective");
        Assert.Equal("Specific target", Value(Metadata(project, "nested"), "artifact", "target"));
        Assert.Equal("Concrete perspective", Value(Assert.Single(Relations(project, "nested")), "role"));
        var nestedDiscovery = await Success(project, "discovery", "nexus", "nested");
        Assert.Contains("origin.md", nestedDiscovery, StringComparison.Ordinal);
        Assert.Contains("Concrete perspective", nestedDiscovery, StringComparison.Ordinal);
        Assert.Equal(2, Relations(project, "origin").Length);
        Assert.Contains("Concrete perspective", Read(project, "origin"), StringComparison.Ordinal);

        // Discovery describes this Nexus; it must not recursively read its associated Document.
        File.WriteAllText(Path.Combine(project.Root, "context.md"), "deliberately invalid child artifact");
        await Success(project, "discovery", "nexus", "origin");
    }

    [Fact]
    public async Task Nexus_question_updates_preserve_other_answers_and_recommendations()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "nexus", "detail", "--kind", "detail", "--target", "Tooling");
        var first = await Success(project, "discovery", "nexus", "detail");
        Assert.Contains("Existing perspective", first, StringComparison.Ordinal);
        Assert.Contains("[1.1] What boundaries?", first, StringComparison.Ordinal);
        Assert.Contains("[1.1.1] document: context", first, StringComparison.Ordinal);
        await Success(project, "update", "nexus", "detail", "--question-id", "1.1", "--answer", "Explicit boundary");
        Assert.Contains("Existing perspective", Read(project, "detail"), StringComparison.Ordinal);
        Assert.Contains("Explicit boundary", Read(project, "detail"), StringComparison.Ordinal);
        var updated = await Success(project, "discovery", "nexus", "detail");
        Assert.Contains("status: answered", Selection(updated, "1.1"), StringComparison.Ordinal);
        Assert.Contains("vslices new document --from-nexus detail:1.1.1", updated, StringComparison.Ordinal);
        var before = Read(project, "detail");
        await Failure(project, "update", "nexus", "detail", "--question-id", "1.1.1", "--answer", "Not a question");
        Assert.Equal(before, Read(project, "detail"));
        await Success(project, "new", "document", "boundary", "--from-nexus", "detail:1.1.1", "--role", "Concrete boundary evidence");
        Assert.Equal("Concrete boundary evidence", Value(Assert.Single(Relations(project, "boundary")), "role"));
        Assert.Contains("Concrete boundary evidence", Read(project, "detail"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Path_exposes_full_unanswered_trajectory_deep_recommendations_and_preserves_graph_on_update()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "continuity-path", "journey", "--kind", "domain-context", "--target", "Tooling");
        var source = Read(project, "journey");
        Assert.Contains("Preserve contextual continuity", source, StringComparison.Ordinal);
        Assert.Contains("Begin with the context", source, StringComparison.Ordinal);
        Assert.Contains("## Artifacts asociados", source, StringComparison.Ordinal);
        Assert.Empty(Relations(project, "journey"));
        Assert.False(Assert.IsType<YamlMappingNode>(ArtifactTestMetadata.Node(Metadata(project, "journey"), "artifact"))
            .Children.ContainsKey(new YamlScalarNode("scope")));
        var graph = Graph(source);
        Assert.Contains("q_612d62", graph, StringComparison.Ordinal);
        Assert.Contains("q_615f62", graph, StringComparison.Ordinal);
        Assert.Contains("#34;knowledge#34;", graph, StringComparison.Ordinal);
        Assert.Contains("-- \"explains\" -->", graph, StringComparison.Ordinal);
        Assert.Contains("-- \"composes\" -->", graph, StringComparison.Ordinal);
        Assert.Contains("document: context", graph, StringComparison.Ordinal);
        Assert.Contains("nexus: capability", graph, StringComparison.Ordinal);
        var discovery = await Success(project, "discovery", "continuity-path", "journey");
        Assert.Contains("status: available", Selection(discovery, "3"), StringComparison.Ordinal);
        Assert.Contains("parent: [3.1]", Selection(discovery, "3.1.1"), StringComparison.Ordinal);
        Assert.Contains("vslices new document --from-path journey:3.1.1", discovery, StringComparison.Ordinal);
        Assert.Contains("vslices new nexus --from-path journey:3.1.2.1", discovery, StringComparison.Ordinal);
        Assert.Contains("connection: composes", discovery, StringComparison.Ordinal);

        await Success(project, "new", "document", "knowledge", "--from-path", "journey:3.1.1");
        await Success(project, "new", "nexus", "composition", "--from-path", "journey:3.1.2.1");
        Assert.Equal("Tooling", Value(Metadata(project, "knowledge"), "artifact", "target"));
        Assert.Equal("Tooling", Value(Metadata(project, "composition"), "artifact", "target"));
        Assert.Equal("journey.md", Value(Assert.Single(Relations(project, "knowledge")), "target"));
        Assert.Equal("journey.md", Value(Assert.Single(Relations(project, "composition")), "target"));
        Assert.Equal(2, Relations(project, "journey").Length);
        var after = await Success(project, "discovery", "continuity-path", "journey");
        Assert.Contains("vslices discovery document", Selection(after, "3.1.1"), StringComparison.Ordinal);
        Assert.DoesNotContain("vslices new", Selection(after, "3.1.1"), StringComparison.Ordinal);
        Assert.Contains("vslices discovery nexus", Selection(after, "3.1.2.1"), StringComparison.Ordinal);
        Assert.DoesNotContain("vslices new", Selection(after, "3.1.2.1"), StringComparison.Ordinal);

        await Success(project, "update", "continuity-path", "journey", "--question-id", "1", "--answer", "Revised purpose");
        await Success(project, "update", "continuity-path", "journey", "--question-id", "2", "--answer", "Revised traversal");
        await Success(project, "update", "continuity-path", "journey", "--question-id", "3.1", "--answer", "Known evidence");
        var revised = Read(project, "journey");
        Assert.Contains("Revised purpose", revised, StringComparison.Ordinal);
        Assert.Contains("Revised traversal", revised, StringComparison.Ordinal);
        Assert.Contains("Known evidence", revised, StringComparison.Ordinal);
        Assert.Equal(graph, Graph(revised));
        Assert.Equal(2, Relations(project, "journey").Length);
    }

    [Theory]
    [InlineData("document", "context")]
    [InlineData("nexus", "capability")]
    [InlineData("continuity-path", "domain-context")]
    public async Task Standalone_requires_target_and_ignores_role_without_relation(string family, string type)
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        var error = await Failure(project, "new", family, "artifact", "--kind", type);
        Assert.Contains("NEW105", error, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "artifact.md")));
        await Success(project, "new", family, "artifact", "--kind", type, "--target", "Tooling", "--role", "Ignored role");
        Assert.Empty(Relations(project, "artifact"));
        Assert.DoesNotContain("Ignored role", Read(project, "artifact"), StringComparison.Ordinal);
        Assert.Equal("0.1.0", Value(Metadata(project, "artifact"), "tooling", "schema", "version"));
        Assert.Equal("draft", Value(Metadata(project, "artifact"), "metadata", "status"));
    }

    [Theory]
    [InlineData("document", "context")]
    [InlineData("nexus", "capability")]
    [InlineData("continuity-path", "domain-context")]
    public async Task Arbitrary_associations_are_open_world_and_round_trip_concrete_roles(string family, string type)
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "document", "unrelated", "--kind", "context", "--target", "Different target");
        var before = Read(project, "unrelated");
        Assert.Contains("NEW107", await Failure(project, "new", family, "associated", "--kind", type,
            "--target", "Tooling", "--related-to", "unrelated"), StringComparison.Ordinal);
        Assert.Equal(before, Read(project, "unrelated"));
        const string role = "Hernán's \"evidence\" | history\nSecond line";
        await Success(project, "new", family, "associated", "--kind", type, "--target", "Tooling",
            "--scope", "project-owned-scope", "--related-to", "unrelated", "--role", role);
        Assert.Equal(role, Value(Assert.Single(Relations(project, "associated")), "role"));
        Assert.Equal(role, Value(Assert.Single(Relations(project, "unrelated")), "role"));
        Assert.Equal("project-owned-scope", Value(Metadata(project, "associated"), "artifact", "scope"));
        Assert.Contains("unrelated.md", await Success(project, "discovery", family, "associated"), StringComparison.Ordinal);
        Assert.Contains("associated.md", await Success(project, "discovery", "document", "unrelated"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failed_recommendation_requests_do_not_mutate_the_source_or_create_partial_artifacts()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "nexus", "origin", "--kind", "capability", "--target", "Tooling");
        var before = Read(project, "origin");
        Assert.Contains("RELCLI006", await Failure(project, "new", "document", "missing", "--from-nexus", "origin:99"), StringComparison.Ordinal);
        Assert.Contains("NEW108", await Failure(project, "new", "nexus", "wrong-family", "--from-nexus", "origin:1"), StringComparison.Ordinal);
        Assert.Contains("NEW109", await Failure(project, "new", "document", "wrong-type", "--from-nexus", "origin:1", "--kind", "behavior"), StringComparison.Ordinal);
        Assert.Contains("RELCLI004", await Failure(project, "new", "document", "wrong-source", "--from-path", "origin:1"), StringComparison.Ordinal);
        Assert.Contains("NEW106", await Failure(project, "new", "document", "conflicting", "--from-nexus", "origin:1", "--related-to", "origin", "--role", "x"), StringComparison.Ordinal);
        Assert.Equal(before, Read(project, "origin"));
        Assert.Single(Directory.GetFiles(project.Root, "*.md"));

        var sourcePath = Path.Combine(project.Root, "origin.md");
        File.WriteAllText(sourcePath, before.Replace("  target: Tooling\n", string.Empty, StringComparison.Ordinal));
        var legacy = File.ReadAllText(sourcePath);
        Assert.Contains("NEW105", await Failure(project, "new", "document", "no-target", "--from-nexus", "origin:1"), StringComparison.Ordinal);
        Assert.Equal(legacy, File.ReadAllText(sourcePath));
        await Success(project, "new", "document", "explicit-target", "--from-nexus", "origin:1", "--target", "Explicit target");
        var after = Read(project, "origin");
        Assert.Contains("RELCLI007", await Failure(project, "new", "document", "duplicate", "--from-nexus", "origin:1", "--target", "Explicit target"), StringComparison.Ordinal);
        Assert.Equal(after, Read(project, "origin"));
        Assert.False(File.Exists(Path.Combine(project.Root, "duplicate.md")));
    }

    [Fact]
    public async Task Changed_definition_context_cannot_silently_retarget_a_persisted_ephemeral_selection()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "nexus", "origin", "--kind", "capability", "--target", "Tooling");
        await Success(project, "new", "document", "context", "--from-nexus", "origin:1");
        var before = Read(project, "origin");
        File.WriteAllText(Path.Combine(StandardRoot(project), "nexus", "capability.yml"), Capability.Replace("Explains the context", "A changed recommendation role", StringComparison.Ordinal));
        Assert.Contains("RELART030", await Failure(project, "discovery", "nexus", "origin"), StringComparison.Ordinal);
        Assert.Contains("RELART030", await Failure(project, "new", "nexus", "nested", "--from-nexus", "origin:2"), StringComparison.Ordinal);
        Assert.Equal(before, Read(project, "origin"));
        Assert.False(File.Exists(Path.Combine(project.Root, "nested.md")));
    }

    [Fact]
    public async Task Reconstruction_marker_injection_and_missing_regions_fail_without_writes()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "nexus", "detail", "--kind", "detail", "--target", "Tooling");
        var before = Read(project, "detail");
        Assert.Contains("RELART024", await Failure(project, "update", "nexus", "detail", "--question-id", "1",
            "--answer", "Injected\n<!-- /vslices:artifact-question -->"), StringComparison.Ordinal);
        Assert.Equal(before, Read(project, "detail"));
        var path = Path.Combine(project.Root, "detail.md");
        File.WriteAllText(path, before.Replace("<!-- vslices:artifact-question id=boundaries -->", "<!-- missing marker -->", StringComparison.Ordinal));
        var malformed = Read(project, "detail");
        Assert.Contains("RELART025", await Failure(project, "update", "nexus", "detail", "--question-id", "1", "--answer", "Replacement"), StringComparison.Ordinal);
        Assert.Equal(malformed, Read(project, "detail"));
        File.WriteAllText(path, before.Replace("<!-- /vslices:associated-artifacts -->", string.Empty, StringComparison.Ordinal));
        malformed = Read(project, "detail");
        await Failure(project, "new", "document", "related", "--kind", "context", "--target", "Tooling", "--related-to", "detail", "--role", "Evidence");
        Assert.Equal(malformed, Read(project, "detail"));
        Assert.False(File.Exists(Path.Combine(project.Root, "related.md")));
    }

    [Fact]
    public async Task Relation_write_preparation_failure_keeps_the_existing_artifact_byte_identical()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", "nexus", "origin", "--kind", "capability", "--target", "Tooling");
        var before = File.ReadAllBytes(Path.Combine(project.Root, "origin.md"));
        File.WriteAllText(Path.Combine(project.Root, "blocked"), "not a directory");
        Assert.Contains("RELWRITE", await Failure(project, "new", "document", "blocked/child", "--from-nexus", "origin:1"), StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(project.Root, "origin.md")));
        Assert.False(File.Exists(Path.Combine(project.Root, "blocked", "child.md")));
        Assert.Empty(Directory.GetFiles(project.Root, "*.candidate", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(project.Root, "*.backup", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Unknown_artifact_family_cannot_enter_an_arbitrary_relation()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        File.WriteAllText(Path.Combine(project.Root, "unknown.md"), "---\nartifact:\n  kind: invented\n  type: context\n---\n");
        var before = File.ReadAllBytes(Path.Combine(project.Root, "unknown.md"));
        Assert.Contains("RELART", await Failure(project, "new", "document", "new", "--kind", "context", "--target", "Tooling", "--related-to", "unknown", "--role", "Evidence"), StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(project.Root, "unknown.md")));
        Assert.False(File.Exists(Path.Combine(project.Root, "new.md")));
    }
}
