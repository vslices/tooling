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
    public void Discovery_from_named_artifact_exposes_tags_and_kind()
    {
        var frontier = VsirMutationEngine.Discover(Named, out var error);

        Assert.Null(error);
        Assert.Contains(frontier, item => item.Path == "tags");
        var kind = Assert.Single(frontier, item => item.Path == "kind");
        Assert.Equal(["domain-type"], kind.AllowedValues);
    }

    [Fact]
    public void Discovery_after_kind_exposes_tags_and_classification()
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
        Assert.Contains("value-object", classification.AllowedValues!);
        Assert.Contains("entity", classification.AllowedValues!);
        Assert.Contains("aggregate-root", classification.AllowedValues!);
    }

    [Fact]
    public void Discovery_after_classification_keeps_only_tags_as_current_frontier()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            classification: value-object
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        var tags = Assert.Single(frontier);
        Assert.Equal("tags", tags.Path);
        Assert.Contains(VsirMutationKind.Add, tags.Operations);
        Assert.Contains(VsirMutationKind.Remove, tags.Operations);
        Assert.Contains(VsirMutationKind.Set, tags.Operations);
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
