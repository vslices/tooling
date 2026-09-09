namespace VSlices.Tooling.Tests;

public sealed class VsirMutationCandidateParityTests
{
    [Fact]
    public async Task Discovery_projection_and_update_persistence_share_the_same_prepared_candidate()
    {
        using var project = new ToolingTestProject();
        var path = Path.Combine(project.Root, "Probe.vsir");
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Probe
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value:
                sequence: string
            representation:
              Value:
                sequence: string
            input:
              Value:
                sequence: string
            construction:
              - refine:
                  state:
                    Value: input.Value
            """;
        File.WriteAllText(path, source);

        const string decision = "representation.Value.mapping={map: {source: state.Value, bind: item, value: item}}";
        var mutation = new VsirMutation(VsirMutationKind.Set, "representation.Value.mapping", "{map: {source: state.Value, bind: item, value: item}}");
        var projectedCandidate = VsirMutationCandidate.Build(source, [mutation]);
        Assert.True(projectedCandidate.IsSuccess, projectedCandidate.Error);

        var projectedDiscovery = await project.Run(
            project.Root,
            "discovery", "vsir", path,
            "--set", decision);

        Assert.Equal(0, projectedDiscovery.ExitCode);
        Assert.Contains("conformance: conforming", projectedDiscovery.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("VSIR118", projectedDiscovery.StandardOutput, StringComparison.Ordinal);

        var updated = await project.Run(
            project.Root,
            "update", "vsir", path,
            "--set", decision);

        Assert.Equal(0, updated.ExitCode);
        var persisted = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var expected = VsirSourceFormatter
            .FormatAfterMutation(projectedCandidate.Source!)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(expected, persisted);
        Assert.Contains("type:\n      sequence: string", persisted, StringComparison.Ordinal);
        Assert.Contains("mapping:\n      map:", persisted, StringComparison.Ordinal);

        var persistedDiscovery = await project.Run(project.Root, "discovery", "vsir", path);
        Assert.Equal(0, persistedDiscovery.ExitCode);
        Assert.Contains("conformance: conforming", persistedDiscovery.StandardOutput, StringComparison.Ordinal);
    }
}
