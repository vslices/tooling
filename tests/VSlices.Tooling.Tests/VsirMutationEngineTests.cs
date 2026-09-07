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
    public void Add_and_remove_same_value_is_rejected_before_candidate_is_returned()
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

    [Fact]
    public void Related_scalar_facts_can_be_established_in_one_transaction()
    {
        var result = VsirMutationEngine.Apply(
            Named,
            [
                new(VsirMutationKind.Set, "kind", "domain-type"),
                new(VsirMutationKind.Set, "classification", "identifier")
            ]);

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
    public void Discovery_from_named_artifact_exposes_only_immediate_frontier()
    {
        var frontier = VsirMutationEngine.Discover(Named, out var error);

        Assert.Null(error);
        Assert.Contains(frontier, path => path.Path == "tags");
        Assert.Contains(frontier, path => path.Path == "kind");
        Assert.DoesNotContain(frontier, path => path.Path == "classification");
        Assert.DoesNotContain(frontier, path => path.Path == "shape");
    }

    [Fact]
    public void Discovery_after_kind_exposes_classification_but_not_later_choices()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.Contains(frontier, path => path.Path == "classification");
        Assert.DoesNotContain(frontier, path => path.Path == "shape");
        Assert.DoesNotContain(frontier, path => path.Path == "traits");
    }

    [Fact]
    public void Deep_path_is_rejected_until_contract_authorizes_it()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "state.Region.from.value", "state.Commune.InProvince.InRegion")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE004:", result.Error);
    }
}
