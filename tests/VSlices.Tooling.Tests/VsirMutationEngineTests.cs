namespace VSlices.Tooling.Tests;

public sealed class VsirMutationEngineTests
{
    private const string Named = """
        vsir: 0.1
        name: StreetName
        """;

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
    public void Discovery_from_named_artifact_exposes_only_kind()
    {
        var frontier = VsirMutationEngine.Discover(Named, out var error);

        Assert.Null(error);
        var item = Assert.Single(frontier);
        Assert.Equal("kind", item.Path);
        Assert.Equal(["domain-type"], item.AllowedValues);
    }

    [Fact]
    public void Discovery_after_kind_exposes_only_classification()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        var item = Assert.Single(frontier);
        Assert.Equal("classification", item.Path);
        Assert.Contains("value-object", item.AllowedValues!);
        Assert.Contains("entity", item.AllowedValues!);
        Assert.Contains("aggregate-root", item.AllowedValues!);
    }

    [Fact]
    public void Discovery_after_classification_has_no_further_implemented_frontier()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            classification: value-object
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.Empty(frontier);
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
