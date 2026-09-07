namespace VSlices.Tooling.Tests;

public sealed class ShapeAuthoringTests
{
    [Fact]
    public void Domain_type_discovery_requires_shape_and_exposes_product_and_sum()
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
        Assert.Equal(["product", "sum"], shape.AllowedValues);
        Assert.Contains("Product", shape.Meaning, StringComparison.Ordinal);
        Assert.Contains("sum", shape.Meaning, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("product")]
    [InlineData("sum")]
    public void Shape_can_be_set_to_supported_domain_type_shapes(string value)
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "shape", value)]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains($"shape: {value}", result.Source);
    }

    [Fact]
    public void Unknown_shape_fails_closed()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "shape", "scalar")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE034:", result.Error);
    }

    [Fact]
    public void Progressive_template_can_materialize_shape()
    {
        var result = VsirTemplate.Create(
            "ContactMethod",
            "domain-type",
            "sum",
            "value-object",
            []);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("kind: domain-type", result.Source);
        Assert.Contains("shape: sum", result.Source);
        Assert.Contains("classification: value-object", result.Source);
    }
}
