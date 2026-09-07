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
            "value-object");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("kind: domain-type", result.Source);
        Assert.Contains("shape: sum", result.Source);
        Assert.Contains("classification: value-object", result.Source);
    }

    [Fact]
    public void Sum_discovery_exposes_shared_state_shared_representation_and_required_variants()
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
        Assert.Equal("map<property, declaration>", state.ValueKind);
        Assert.Contains("shared by every variant", state.Meaning, StringComparison.Ordinal);
        Assert.Contains("may be empty", state.Meaning, StringComparison.Ordinal);

        var representation = Assert.Single(frontier, item => item.Path == "representation");
        Assert.Contains("shared by every variant", representation.Meaning, StringComparison.Ordinal);
        Assert.Contains("active variant", representation.Meaning, StringComparison.Ordinal);

        var variants = Assert.Single(frontier, item => item.Path == "variants");
        Assert.Equal(VsirFrontierStatus.Required, variants.Status);
        Assert.Equal("map<variant, declaration>", variants.ValueKind);
        Assert.Single(variants.Operations);
        Assert.Contains(VsirMutationKind.Set, variants.Operations);
    }

    [Fact]
    public void Sum_shared_state_is_authored_like_product_state_and_may_become_empty()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: SrvIdentity
            shape: sum
            classification: aggregate-root
            state:
              Document: Rut
            representation: {}
            variants:
              NaturalIdentity: {}
            """;

        var added = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "state.Address", "Location")]);

        Assert.True(added.IsSuccess, added.Error);
        Assert.Contains("Address: Location", added.Source);

        var removedAddress = VsirMutationEngine.Apply(
            added.Source!,
            [new(VsirMutationKind.Remove, "state.Address", null)]);

        Assert.True(removedAddress.IsSuccess, removedAddress.Error);

        var removedLast = VsirMutationEngine.Apply(
            removedAddress.Source!,
            [new(VsirMutationKind.Remove, "state.Document", null)]);

        Assert.True(removedLast.IsSuccess, removedLast.Error);
        Assert.Contains("state: {}", removedLast.Source);
    }

    [Fact]
    public void Sum_can_establish_explicit_empty_shared_state_and_representation()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Name
            shape: sum
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [
                new(VsirMutationKind.Set, "state", "{}"),
                new(VsirMutationKind.Set, "representation", "{}")
            ]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("state: {}", result.Source);
        Assert.Contains("representation: {}", result.Source);
    }

    [Fact]
    public void Sum_variants_support_low_level_add_set_and_remove_at_the_variant_boundary()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: ContactMethod
            shape: sum
            classification: value-object
            state: {}
            representation: {}
            """;

        var added = VsirMutationEngine.Apply(
            source,
            [
                new(VsirMutationKind.Add, "variants.Email", "{state: {Value: EmailAddress}, representation: {Value: EmailAddress}}"),
                new(VsirMutationKind.Add, "variants.Phone", "{state: {Number: PhoneNumber}, representation: {Number: PhoneNumber}}")
            ]);

        Assert.True(added.IsSuccess, added.Error);
        Assert.Contains("Email:", added.Source);
        Assert.Contains("Value: EmailAddress", added.Source);
        Assert.Contains("Phone:", added.Source);
        Assert.Contains("Number: PhoneNumber", added.Source);

        var changed = VsirMutationEngine.Apply(
            added.Source!,
            [new(VsirMutationKind.Set, "variants.Phone", "{state: {Number: PhoneNumber, Extension: string}, representation: {Number: PhoneNumber}}")]);

        Assert.True(changed.IsSuccess, changed.Error);
        Assert.Contains("Extension: string", changed.Source);

        var removed = VsirMutationEngine.Apply(
            changed.Source!,
            [new(VsirMutationKind.Remove, "variants.Email", null)]);

        Assert.True(removed.IsSuccess, removed.Error);
        Assert.DoesNotContain("Email:", removed.Source);
        Assert.Contains("Phone:", removed.Source);
    }

    [Fact]
    public void Sum_variant_may_be_empty_but_transform_variant_requires_local_input_and_construction()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Name
            shape: sum
            classification: value-object
            state: {}
            representation: {}
            """;

        var empty = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "variants.Marker", "{}")]);

        Assert.True(empty.IsSuccess, empty.Error);

        var invalidTransform = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "variants.FullName", "{traits: [transform], state: {Names: string}}")]);

        Assert.False(invalidTransform.IsSuccess);
        Assert.StartsWith("UPDATE038:", invalidTransform.Error);

        var validTransform = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "variants.FullName", "{traits: [transform], state: {Names: string}, representation: {Names: string}, input: {Names: string}, construction: []}")]);

        Assert.True(validTransform.IsSuccess, validTransform.Error);
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
            state:
              Value: string
            representation:
              Value: string
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "variants.Other", "{}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE036:", result.Error);
    }

    [Fact]
    public void Existing_product_state_can_become_shared_state_when_shape_changes_to_sum()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            state:
              Value: string
            representation:
              Value: string
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "shape", "sum")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("shape: sum", result.Source);
        Assert.Contains("Value: string", result.Source);
    }
}
