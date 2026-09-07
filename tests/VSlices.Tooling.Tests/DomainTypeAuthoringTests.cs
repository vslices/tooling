namespace VSlices.Tooling.Tests;

public sealed class DomainTypeAuthoringTests
{
    [Fact]
    public void Domain_type_discovery_requires_state_and_representation_before_classification_is_known()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);

        var state = Assert.Single(frontier, item => item.Path == "state");
        Assert.Equal(VsirFrontierStatus.Required, state.Status);

        var representation = Assert.Single(frontier, item => item.Path == "representation");
        Assert.Equal(VsirFrontierStatus.Required, representation.Status);

        var classification = Assert.Single(frontier, item => item.Path == "classification");
        Assert.Equal(VsirFrontierStatus.Required, classification.Status);
    }

    [Fact]
    public void Domain_type_cannot_remove_the_last_state_or_representation_property_regardless_of_classification()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            classification: identifier
            state:
              Value: string
            representation:
              Value: string
            """;

        var state = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Remove, "state.Value", null)]);

        Assert.False(state.IsSuccess);
        Assert.StartsWith("UPDATE024:", state.Error);

        var representation = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Remove, "representation.Value", null)]);

        Assert.False(representation.IsSuccess);
        Assert.StartsWith("UPDATE024:", representation.Error);
    }
}
