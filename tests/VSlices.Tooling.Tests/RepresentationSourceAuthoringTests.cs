namespace VSlices.Tooling.Tests;

public sealed class RepresentationSourceAuthoringTests
{
    [Fact]
    public void State_from_can_only_reference_declared_state_space()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            classification: value-object
            state:
              Province: Province
            representation:
              Province: Province
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "state.Province.from", "input.Province")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE029:", result.Error);
    }

    [Fact]
    public void Representation_from_can_be_established_changed_and_removed_without_losing_type()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            classification: maintained
            state:
              Name: string
              Code: string
            representation:
              Value: string
            values:
              Natural:
                state:
                  Name: Natural
                  Code: N
            """;

        var established = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "representation.Value.from", "state.Name")]);

        Assert.True(established.IsSuccess, established.Error);
        Assert.Contains("type: string", established.Source);
        Assert.Contains("from: state.Name", established.Source);

        var changed = VsirMutationEngine.Apply(
            established.Source!,
            [new(VsirMutationKind.Set, "representation.Value.from", "state.Code")]);

        Assert.True(changed.IsSuccess, changed.Error);
        Assert.Contains("from: state.Code", changed.Source);

        var removed = VsirMutationEngine.Apply(
            changed.Source!,
            [new(VsirMutationKind.Remove, "representation.Value.from", null)]);

        Assert.True(removed.IsSuccess, removed.Error);
        Assert.Contains("Value: string", removed.Source);
        Assert.DoesNotContain("from:", removed.Source);
    }

    [Fact]
    public void Representation_from_requires_a_state_reference()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            classification: value-object
            state:
              Value: string
            representation:
              Value: string
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "representation.Value.from", "input.Value")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE029:", result.Error);
    }

    [Fact]
    public void Representation_from_and_mapping_are_mutually_exclusive()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: WrappedRut
            classification: value-object
            state:
              Value: Rut
            representation:
              Value:
                type: string
                mapping:
                  stringify: state.Value
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "representation.Value.from", "state.Value")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE030:", result.Error);
    }

    [Fact]
    public void Discovery_explains_direct_source_authoring_for_state_and_representation()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            shape: product
            classification: value-object
            state:
              Commune: Commune
              Province: Province
            representation:
              Commune: Commune
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.Contains("state.<property>.from", Assert.Single(frontier, item => item.Path == "state").Meaning);
        Assert.Contains("representation.<property>.from", Assert.Single(frontier, item => item.Path == "representation").Meaning);
    }
}
