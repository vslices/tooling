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

    [Fact]
    public void Sum_discovery_treats_state_entries_as_mutually_exclusive_variants()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: ContactMethod
            shape: sum
            classification: value-object
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        var state = Assert.Single(frontier, item => item.Path == "state");
        Assert.Equal("map<variant, product-payload>", state.ValueKind);
        Assert.Contains("Exactly one variant", state.Meaning, StringComparison.Ordinal);

        var representation = Assert.Single(frontier, item => item.Path == "representation");
        Assert.Contains("which state variant is active", representation.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public void Sum_variants_support_add_set_and_remove_at_the_variant_boundary()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: ContactMethod
            shape: sum
            classification: value-object
            """;

        var added = VsirMutationEngine.Apply(
            source,
            [
                new(VsirMutationKind.Add, "state.Email", "{Value: EmailAddress}"),
                new(VsirMutationKind.Add, "state.Phone", "{Number: PhoneNumber}")
            ]);

        Assert.True(added.IsSuccess, added.Error);
        Assert.Contains("Email:", added.Source);
        Assert.Contains("Value: EmailAddress", added.Source);
        Assert.Contains("Phone:", added.Source);
        Assert.Contains("Number: PhoneNumber", added.Source);

        var changed = VsirMutationEngine.Apply(
            added.Source!,
            [new(VsirMutationKind.Set, "state.Phone", "{Number: PhoneNumber, Extension: string}")]);

        Assert.True(changed.IsSuccess, changed.Error);
        Assert.Contains("Extension: string", changed.Source);

        var removed = VsirMutationEngine.Apply(
            changed.Source!,
            [new(VsirMutationKind.Remove, "state.Email", null)]);

        Assert.True(removed.IsSuccess, removed.Error);
        Assert.DoesNotContain("Email:", removed.Source);
        Assert.Contains("Phone:", removed.Source);
    }

    [Fact]
    public void Sum_variant_may_have_an_empty_payload_but_scalar_state_is_rejected()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: ApprovalState
            shape: sum
            classification: value-object
            """;

        var empty = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "state.Pending", "{}")]);

        Assert.True(empty.IsSuccess, empty.Error);
        Assert.Contains("Pending: {}", empty.Source);

        var invalid = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "state.Pending", "string")]);

        Assert.False(invalid.IsSuccess);
        Assert.StartsWith("UPDATE035:", invalid.Error);
    }

    [Fact]
    public void Existing_product_state_prevents_reclassification_of_shape_to_sum()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            state:
              Value: string
            representation:
              Value: string
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "shape", "sum")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE035:", result.Error);
    }
}
