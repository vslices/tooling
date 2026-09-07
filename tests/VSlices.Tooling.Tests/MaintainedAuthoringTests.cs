namespace VSlices.Tooling.Tests;

public sealed class MaintainedAuthoringTests
{
    [Fact]
    public void Maintained_is_a_valid_domain_type_classification()
    {
        Assert.Contains("maintained", VsirAuthoringContract.DomainTypeClassifications);
    }

    [Fact]
    public void Maintained_discovery_requires_state_representation_and_values()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            classification: maintained
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);

        var state = Assert.Single(frontier, item => item.Path == "state");
        Assert.Equal(VsirFrontierStatus.Required, state.Status);

        var representation = Assert.Single(frontier, item => item.Path == "representation");
        Assert.Equal(VsirFrontierStatus.Required, representation.Status);

        var values = Assert.Single(frontier, item => item.Path == "values");
        Assert.Equal(VsirFrontierStatus.Required, values.Status);
        Assert.Equal("map<member, state>", values.ValueKind);
        Assert.Contains(VsirMutationKind.Add, values.Operations);
        Assert.Contains(VsirMutationKind.Remove, values.Operations);
        Assert.Contains(VsirMutationKind.Set, values.Operations);

        var traits = Assert.Single(frontier, item => item.Path == "traits");
        Assert.Equal(VsirFrontierStatus.Optional, traits.Status);
        Assert.DoesNotContain("maintained", traits.AllowedValues ?? []);
    }
}
