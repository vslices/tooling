namespace VSlices.Tooling.Tests;

public sealed class IdentifierAuthoringTests
{
    [Fact]
    public void Identifier_discovery_adds_only_equality_beyond_domain_type_obligations()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: SrvIdentityId
            shape: product
            classification: identifier
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);

        var state = Assert.Single(frontier, item => item.Path == "state");
        Assert.Equal(VsirFrontierStatus.Required, state.Status);

        var representation = Assert.Single(frontier, item => item.Path == "representation");
        Assert.Equal(VsirFrontierStatus.Required, representation.Status);

        var equality = Assert.Single(frontier, item => item.Path == "equality");
        Assert.Equal(VsirFrontierStatus.Required, equality.Status);
        Assert.Equal("strategy", equality.ValueKind);
        Assert.Single(equality.Operations);
        Assert.Contains(VsirMutationKind.Set, equality.Operations);

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
