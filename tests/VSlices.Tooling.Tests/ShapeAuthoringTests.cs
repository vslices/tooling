namespace VSlices.Tooling.Tests;

public sealed class ShapeAuthoringTests
{
    [Fact]
    public void Domain_type_discovery_exposes_only_end_to_end_supported_product_shape()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        var shape = Assert.Single(frontier, item => item.Path == "shape");
        Assert.Equal(VsirFrontierStatus.Required, shape.Status);
        Assert.Equal("enum", shape.ValueKind);
        Assert.Single(shape.Operations);
        Assert.Contains(VsirMutationKind.Set, shape.Operations);
        Assert.Equal(["product"], shape.AllowedValues);
        Assert.Contains("product", shape.Meaning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Product_shape_can_be_set()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "shape", "product")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("shape: product", result.Source);
    }

    [Theory]
    [InlineData("sum")]
    [InlineData("scalar")]
    public void Shapes_without_end_to_end_support_fail_closed(string value)
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "shape", value)]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE034:", result.Error);
    }

    [Fact]
    public void Progressive_template_rejects_sum_until_parser_validator_and_lowering_support_it()
    {
        var result = VsirTemplate.Create(
            "ContactMethod",
            "domain-type",
            "sum",
            "value-object");

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE034:", result.Error);
    }

    [Fact]
    public void Product_shape_rejects_variants()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input: string
            construction:
              - refine:
                  value: input
                  as: state.Value
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "variants.Other", "{}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE036:", result.Error);
    }
}
