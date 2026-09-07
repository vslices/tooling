namespace VSlices.Tooling.Tests;

public sealed class IdentifierAuthoringTests
{
    [Fact]
    public void Identifier_discovery_requires_only_equality_from_its_classification()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: SrvIdentityId
            classification: identifier
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);

        var equality = Assert.Single(frontier, item => item.Path == "equality");
        Assert.Equal(VsirFrontierStatus.Required, equality.Status);
        Assert.Equal("strategy", equality.ValueKind);
        Assert.Single(equality.Operations);
        Assert.Contains(VsirMutationKind.Set, equality.Operations);

        Assert.DoesNotContain(frontier, item => item.Path == "state");
        Assert.DoesNotContain(frontier, item => item.Path == "representation");
        Assert.DoesNotContain(frontier, item => item.Path == "values");
    }

    [Fact]
    public void Identifier_supports_setting_equality()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: SrvIdentityId
            classification: identifier
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "equality", "{over: Rut, by: state.Value}")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("equality:", result.Source);
        Assert.Contains("over: Rut", result.Source);
        Assert.Contains("by: state.Value", result.Source);
    }
}
