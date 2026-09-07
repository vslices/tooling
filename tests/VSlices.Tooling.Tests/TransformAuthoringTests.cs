namespace VSlices.Tooling.Tests;

public sealed class TransformAuthoringTests
{
    [Fact]
    public void Transform_discovery_exposes_writable_input_and_construction()
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
            """;

        var frontier = VsirMutationPipeline.Discover(source, out var error);

        Assert.Null(error);

        var input = Assert.Single(frontier, item => item.Path == "input");
        Assert.Equal(VsirFrontierStatus.Required, input.Status);
        Assert.Equal("map<property, declaration> | scalar semantic type", input.ValueKind);
        Assert.Single(input.Operations);
        Assert.Contains(VsirMutationKind.Set, input.Operations);

        var construction = Assert.Single(frontier, item => item.Path == "construction");
        Assert.Equal(VsirFrontierStatus.Required, construction.Status);
        Assert.Equal("sequence<step>", construction.ValueKind);
        Assert.Single(construction.Operations);
        Assert.Contains(VsirMutationKind.Set, construction.Operations);
    }

    [Fact]
    public void Structured_input_supports_low_level_add_set_and_remove_at_property_boundary()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            """;

        var added = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Add, "input.Value", "string")]);

        Assert.True(added.IsSuccess, added.Error);
        Assert.Contains("input:", added.Source);
        Assert.Contains("Value: string", added.Source);

        var changed = VsirMutationEngine.Apply(
            added.Source!,
            [new(VsirMutationKind.Set, "input.Value", "Rut")]);

        Assert.True(changed.IsSuccess, changed.Error);
        Assert.Contains("Value: Rut", changed.Source);

        var removed = VsirMutationEngine.Apply(
            changed.Source!,
            [new(VsirMutationKind.Remove, "input.Value", null)]);

        Assert.True(removed.IsSuccess, removed.Error);
        Assert.DoesNotContain("input:", removed.Source);
    }

    [Fact]
    public void Complete_input_can_be_set_to_scalar_or_product_contract()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            """;

        var scalar = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "input", "Rut")]);

        Assert.True(scalar.IsSuccess, scalar.Error);
        Assert.Contains("input: Rut", scalar.Source);

        var product = VsirMutationPipeline.Apply(
            scalar.Source!,
            [new(VsirMutationKind.Set, "input", "{Value: string}")]);

        Assert.True(product.IsSuccess, product.Error);
        Assert.Contains("input:", product.Source);
        Assert.Contains("Value: string", product.Source);
    }

    [Fact]
    public void Construction_set_accepts_the_normalized_StreetName_sequence()
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
            input:
              Value: string
            """;

        var construction = """
            [{ensure: {condition: {intrinsic: non-empty, args: {value: input.Value}}, failure: {message: 'Debes especificar una calle'}}}, {ensure: {condition: {intrinsic: length-at-most, args: {value: input.Value, max: 30}}, failure: {message: 'Debe tener 30 caracteres o menos (Enviados {length})'}}}, {refine: {state: {Value: input.Value}}}]
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "construction", construction)]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("construction:", result.Source);
        Assert.Contains("ensure:", result.Source);
        Assert.Contains("intrinsic: non-empty", result.Source);
        Assert.Contains("intrinsic: length-at-most", result.Source);
        Assert.Contains("refine:", result.Source);
        Assert.Contains("Value: input.Value", result.Source);
    }

    [Fact]
    public void StreetName_can_be_authored_from_the_progressive_template_using_only_advertised_cli_semantics()
    {
        var created = VsirTemplate.Create(
            "StreetName",
            "domain-type",
            "product",
            "value-object");

        Assert.True(created.IsSuccess, created.Error);

        var core = VsirMutationPipeline.Apply(
            created.Source!,
            [
                new(VsirMutationKind.Set, "state.Value", "string"),
                new(VsirMutationKind.Set, "representation.Value", "string"),
                new(VsirMutationKind.Add, "traits", "transform")
            ]);

        Assert.True(core.IsSuccess, core.Error);

        var input = VsirMutationPipeline.Apply(
            core.Source!,
            [new(VsirMutationKind.Set, "input.Value", "string")]);

        Assert.True(input.IsSuccess, input.Error);

        var construction = """
            [{ensure: {condition: {intrinsic: non-empty, args: {value: input.Value}}, failure: {message: 'Debes especificar una calle'}}}, {ensure: {condition: {intrinsic: length-at-most, args: {value: input.Value, max: 30}}, failure: {message: 'Debe tener 30 caracteres o menos (Enviados {length})'}}}, {refine: {state: {Value: input.Value}}}]
            """;

        var complete = VsirMutationPipeline.Apply(
            input.Source!,
            [new(VsirMutationKind.Set, "construction", construction)]);

        Assert.True(complete.IsSuccess, complete.Error);
        Assert.Contains("kind: domain-type", complete.Source);
        Assert.Contains("shape: product", complete.Source);
        Assert.Contains("classification: value-object", complete.Source);
        Assert.Contains("traits: [transform]", complete.Source);
        Assert.Contains("state:", complete.Source);
        Assert.Contains("representation:", complete.Source);
        Assert.Contains("input:", complete.Source);
        Assert.Contains("construction:", complete.Source);
    }

    [Fact]
    public void Input_and_construction_fail_closed_without_transform()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            state:
              Value: string
            representation:
              Value: string
            """;

        var input = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "input.Value", "string")]);

        Assert.False(input.IsSuccess);
        Assert.StartsWith("UPDATE039:", input.Error);

        var construction = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "construction", "[{refine: {state: {Value: input.Value}}}]")]);

        Assert.False(construction.IsSuccess);
        Assert.StartsWith("UPDATE039:", construction.Error);
    }

    [Fact]
    public void Construction_rejects_unknown_steps()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "construction", "[{invent: {value: input.Value}}]")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE041:", result.Error);
    }
}
