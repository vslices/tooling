using YamlDotNet.RepresentationModel;
using static VSlices.Tooling.Tests.RelationalTestSupport;

namespace VSlices.Tooling.Tests;

public sealed class RelationalDefinitionTests
{
    [Theory]
    [InlineData("nexus", "nexus:\n  type: broken\n  questions: scalar\n")]
    [InlineData("nexus", "nexus:\n  type: broken\n  recommendations:\n    - scalar\n")]
    [InlineData("nexus", "nexus:\n  type: broken\n  recommendations:\n    - document: context\n      nexus: capability\n      role: conflict\n")]
    [InlineData("nexus", "nexus:\n  type: broken\n  recommendations:\n    - document: context\n")]
    [InlineData("nexus", "nexus:\n  type: broken\n  recommendations:\n    - document: [context]\n      role: malformed\n")]
    [InlineData("nexus", "nexus:\n  type: broken\n  unknown-semantics: true\n")]
    [InlineData("nexus", "nexus:\n  type: broken\n  scopes: [project, {unknown: value}]\n")]
    [InlineData("nexus", "nexus:\n  type: broken\n  questions:\n    - id: duplicate\n      text: First\n    - id: duplicate\n      text: Second\n")]
    [InlineData("nexus", "nexus:\n  type: broken\n  questions:\n    - id: parent\n      text: Parent\n      children: [invalid]\n")]
    [InlineData("continuity-path", "continuity-path:\n  type: broken\n  purpose: Intent\n  recommended-traversal: Start\n  question: invalid\n")]
    [InlineData("continuity-path", "continuity-path:\n  type: broken\n  purpose: Intent\n  recommended-traversal: Start\n  question:\n    id: root\n    text: Root\n    connection: invalid\n")]
    [InlineData("continuity-path", "continuity-path:\n  type: broken\n  purpose: Intent\n  question:\n    id: root\n    text: Root\n")]
    public async Task Malformed_candidate_semantics_are_rejected_instead_of_silently_discarded(string family, string body)
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        var directory = family == "nexus" ? "nexus" : "continuity-paths";
        File.WriteAllText(Path.Combine(StandardRoot(project), directory, "broken.yml"),
            $"kind: vslices-{family}-definition\nversion: 0.1\n{body}");
        var error = await Failure(project, "new", "nexus", "must-not-exist", "--kind", "capability", "--target", "Tooling");
        Assert.Contains("RELSTD", error, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "must-not-exist.md")));
    }

    [Fact]
    public async Task Optional_candidate_folders_may_be_absent_but_not_malformed_files()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        Directory.Delete(Path.Combine(StandardRoot(project), "nexus"), recursive: true);
        Directory.Delete(Path.Combine(StandardRoot(project), "continuity-paths"), recursive: true);
        await Success(project, "new", "document", "context", "--kind", "context", "--target", "Tooling");
        await Failure(project, "new", "nexus", "nexus", "--kind", "capability", "--target", "Tooling");
        File.WriteAllText(Path.Combine(StandardRoot(project), "nexus"), "not a candidate directory");
        Assert.Contains("RELSTD003", await Failure(project, "new", "document", "blocked", "--kind", "context", "--target", "Tooling"), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "blocked.md")));
    }

    [Fact]
    public async Task Snapshot_installs_candidates_without_promoting_them_and_preserves_previous_snapshot_on_failure()
    {
        using var source = new ToolingTestProject();
        using var consumer = new ToolingTestProject();
        WriteEnvironment(source);
        ToolingTestProject.WriteDocumentAuthoringSupport(consumer.Root);
        await Success(consumer, "update", "docs-standard", "--origin", StandardRoot(source));
        var candidate = Path.Combine(StandardRoot(consumer), "nexus", "capability.yml");
        Assert.Equal(Capability, File.ReadAllText(candidate));
        Assert.True(File.Exists(Path.Combine(StandardRoot(consumer), "continuity-paths", "domain-context.yml")));
        Assert.DoesNotContain("nexus", File.ReadAllText(Path.Combine(StandardRoot(consumer), "manifest.yaml")), StringComparison.Ordinal);
        await Success(consumer, "new", "nexus", "installed", "--kind", "capability", "--target", "Tooling");
        var before = File.ReadAllBytes(candidate);
        File.WriteAllText(Path.Combine(StandardRoot(source), "nexus", "capability.yml"), "malformed candidate");
        await Failure(consumer, "update", "docs-standard", "--origin", StandardRoot(source));
        Assert.Equal(before, File.ReadAllBytes(candidate));
        await Success(consumer, "discovery", "nexus", "installed");
    }

    [Fact]
    public async Task Ambiguous_scope_is_not_invented_and_target_round_trips_yaml_sensitive_text()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        const string target = "Hernán: \"Tooling\"\nA second line";
        await Success(project, "new", "document", "context", "--kind", "context", "--target", target);
        var metadata = Metadata(project, "context");
        Assert.Equal(target, Value(metadata, "artifact", "target"));
        Assert.False(Assert.IsType<YamlMappingNode>(ArtifactTestMetadata.Node(metadata, "artifact"))
            .Children.ContainsKey(new YamlScalarNode("scope")));
        await Success(project, "discovery", "document", "context");
        await Success(project, "new", "nexus", "detail", "--kind", "detail", "--target", "Tooling");
        Assert.False(Assert.IsType<YamlMappingNode>(ArtifactTestMetadata.Node(Metadata(project, "detail"), "artifact"))
            .Children.ContainsKey(new YamlScalarNode("scope")));
    }

    [Fact]
    public async Task Materialization_that_cannot_be_reconstructed_is_not_published()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        File.WriteAllText(Path.Combine(StandardRoot(project), "nexus", "detail.yml"), Detail.Replace(
            "default: Existing perspective", "default: '<!-- /vslices:artifact-question -->'", StringComparison.Ordinal));
        Assert.Contains("RELART025", await Failure(project, "new", "nexus", "invalid", "--kind", "detail", "--target", "Tooling"), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project.Root, "invalid.md")));
    }
}
