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

    [Fact]
    public void Maintained_values_support_add_and_set_as_member_mutations()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            classification: maintained
            state:
              Name: string
            representation:
              Value: string
            """;

        var added = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "values.Natural", "{state: {Name: Natural}}")]);

        Assert.True(added.IsSuccess, added.Error);
        Assert.Contains("values:", added.Source);
        Assert.Contains("Natural:", added.Source);
        Assert.Contains("Name: Natural", added.Source);

        var changed = VsirMutationEngine.Apply(
            added.Source!,
            [new(VsirMutationKind.Set, "values.Natural", "{state: {Name: NaturalPerson}}")]);

        Assert.True(changed.IsSuccess, changed.Error);
        Assert.Contains("Name: NaturalPerson", changed.Source);
        Assert.DoesNotContain("Name: Natural\n", changed.Source);
    }

    [Fact]
    public void Maintained_values_support_remove_but_keep_at_least_one_member()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            classification: maintained
            state:
              Name: string
            representation:
              Value: string
            values:
              Natural:
                state:
                  Name: Natural
              Juridical:
                state:
                  Name: Juridica
            """;

        var removed = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Remove, "values.Natural", null)]);

        Assert.True(removed.IsSuccess, removed.Error);
        Assert.DoesNotContain("Natural:", removed.Source);
        Assert.Contains("Juridical:", removed.Source);

        var last = VsirMutationEngine.Apply(
            removed.Source!,
            [new(VsirMutationKind.Remove, "values.Juridical", null)]);

        Assert.False(last.IsSuccess);
        Assert.StartsWith("UPDATE024:", last.Error);
    }

    [Fact]
    public void Maintained_member_declaration_requires_non_empty_state()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            classification: maintained
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "values.Natural", "{state: {}}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE028:", result.Error);
    }

    [Fact]
    public void Values_are_rejected_for_non_maintained_classifications()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "values.Natural", "{state: {Name: Natural}}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE027:", result.Error);
    }
}
