namespace VSlices.Tooling.Tests;

public sealed class VsirMutationEngineTests
{
    private const string Named = """
        vsir: 0.1
        name: StreetName
        """;

    [Fact]
    public void Add_and_remove_tags_are_one_set_transition()
    {
        var source = """
            vsir: 0.1
            name: StreetName
            tags: [addressing, street]
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [
                new(VsirMutationKind.Remove, "tags", "street"),
                new(VsirMutationKind.Add, "tags", "identity,location")
            ]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("addressing", result.Source);
        Assert.Contains("identity", result.Source);
        Assert.Contains("location", result.Source);
        Assert.DoesNotContain("street", result.Source);
    }

    [Fact]
    public void Add_and_remove_same_tag_is_rejected_before_candidate_is_returned()
    {
        var result = VsirMutationEngine.Apply(
            Named,
            [
                new(VsirMutationKind.Add, "tags", "identity"),
                new(VsirMutationKind.Remove, "tags", "identity")
            ]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE019:", result.Error);
        Assert.Null(result.Source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Related_scalar_facts_can_be_established_in_one_transaction_regardless_of_order(bool reverse)
    {
        VsirMutation[] mutations =
        [
            new(VsirMutationKind.Set, "kind", "domain-type"),
            new(VsirMutationKind.Set, "classification", "identifier")
        ];

        if (reverse)
            Array.Reverse(mutations);

        var result = VsirMutationEngine.Apply(Named, mutations);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("kind: domain-type", result.Source);
        Assert.Contains("classification: identifier", result.Source);
    }

    [Fact]
    public void Invalid_late_mutation_rejects_the_complete_candidate()
    {
        var result = VsirMutationEngine.Apply(
            Named,
            [
                new(VsirMutationKind.Set, "kind", "domain-type"),
                new(VsirMutationKind.Set, "classification", "not-a-classification")
            ]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Source);
        Assert.StartsWith("UPDATE008:", result.Error);
    }

    [Fact]
    public void Discovery_from_named_artifact_exposes_explained_tags_and_kind()
    {
        var frontier = VsirMutationEngine.Discover(Named, out var error);

        Assert.Null(error);
        var tags = Assert.Single(frontier, item => item.Path == "tags");
        Assert.Equal(VsirFrontierStatus.Optional, tags.Status);
        Assert.Contains("Organizational", tags.Meaning, StringComparison.Ordinal);

        var kind = Assert.Single(frontier, item => item.Path == "kind");
        Assert.Equal(VsirFrontierStatus.Required, kind.Status);
        Assert.Contains("artifact family", kind.Meaning, StringComparison.Ordinal);
        Assert.Equal(["domain-type"], kind.AllowedValues);
    }

    [Fact]
    public void Discovery_after_kind_exposes_explained_classification()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.Contains(frontier, item => item.Path == "tags");
        var classification = Assert.Single(frontier, item => item.Path == "classification");
        Assert.Equal(VsirFrontierStatus.Required, classification.Status);
        Assert.Contains("semantic class", classification.Meaning, StringComparison.Ordinal);
        Assert.Contains("value-object", classification.AllowedValues!);
        Assert.Contains("entity", classification.AllowedValues!);
        Assert.Contains("aggregate-root", classification.AllowedValues!);
    }

    [Fact]
    public void Value_object_discovery_exposes_required_state_and_representation_plus_optional_traits()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            tags: [addressing, street]
            classification: value-object
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);

        var state = Assert.Single(frontier, item => item.Path == "state");
        Assert.Equal(VsirFrontierStatus.Required, state.Status);
        Assert.Empty(state.Operations);
        Assert.Contains("constitute a valid instance", state.Meaning, StringComparison.Ordinal);

        var representation = Assert.Single(frontier, item => item.Path == "representation");
        Assert.Equal(VsirFrontierStatus.Required, representation.Status);
        Assert.Empty(representation.Operations);
        Assert.Contains("represented", representation.Meaning, StringComparison.Ordinal);

        var traits = Assert.Single(frontier, item => item.Path == "traits");
        Assert.Equal(VsirFrontierStatus.Optional, traits.Status);
        Assert.Contains(VsirMutationKind.Add, traits.Operations);
        Assert.Contains(VsirMutationKind.Remove, traits.Operations);
        Assert.Contains(VsirMutationKind.Set, traits.Operations);
        Assert.Contains("additional semantic capabilities", traits.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovery_stops_reporting_classification_obligations_once_present()
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

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.DoesNotContain(frontier, item => item.Path == "state");
        Assert.DoesNotContain(frontier, item => item.Path == "representation");
        Assert.Contains(frontier, item => item.Path == "traits");
        Assert.Contains(frontier, item => item.Path == "tags");
    }

    [Fact]
    public void Traits_can_be_added_after_domain_type_classification()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "traits", "transform")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("traits: [transform]", result.Source);
    }

    [Fact]
    public void Unsupported_path_is_rejected_until_contract_authorizes_it()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "state.Value", "string")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE004:", result.Error);
    }
}
