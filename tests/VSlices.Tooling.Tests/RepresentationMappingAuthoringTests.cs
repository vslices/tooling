namespace VSlices.Tooling.Tests;

public sealed class RepresentationMappingAuthoringTests
{
    [Fact]
    public void Representation_mapping_can_be_set_on_existing_field()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetExtension
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Name: string
              Value: string
            representation:
              Value: string
            input:
              Value: string
            construction:
              - refine:
                  state:
                    Name: input.Value
                    Value: input.Value
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [new(
                VsirMutationKind.Set,
                "representation.Value.mapping",
                "{intrinsic: concat-space, values: [state.Name, state.Value]}")]);

        Assert.True(result.IsSuccess, result.Error);
        var normalized = VsirSourceFormatter.FormatAfterMutation(result.Source!).Replace("\r\n", "\n");

        Assert.Contains("Value:\n    type: string\n    mapping:\n      intrinsic: concat-space", normalized);
        Assert.Contains("- state.Name", normalized);
        Assert.Contains("- state.Value", normalized);
    }

    [Fact]
    public void Representation_mapping_and_from_are_mutually_exclusive()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            state:
              Name: string
            representation:
              Value:
                type: string
                from: state.Name
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [new(
                VsirMutationKind.Set,
                "representation.Value.mapping",
                "{intrinsic: concat-space, values: [state.Name]}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE030:", result.Error);
    }

    [Fact]
    public void Representation_mapping_requires_existing_representation_field()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            state:
              Name: string
            representation:
              Name: string
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [new(
                VsirMutationKind.Set,
                "representation.Value.mapping",
                "{stringify: state.Name}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE022:", result.Error);
    }

    [Fact]
    public void Representation_mapping_supports_only_set()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            state:
              Name: string
            representation:
              Value: string
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [new(
                VsirMutationKind.Add,
                "representation.Value.mapping",
                "{stringify: state.Name}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE012:", result.Error);
    }

    [Fact]
    public void StreetExtension_can_be_authored_with_mapping_input_and_construction()
    {
        var created = VsirTemplate.Create(
            "StreetExtension",
            "domain-type",
            "product",
            "value-object",
            []);

        Assert.True(created.IsSuccess, created.Error);

        var core = VsirMutationPipeline.Apply(
            created.Source!,
            [
                new(VsirMutationKind.Add, "state.Name", "string"),
                new(VsirMutationKind.Add, "state.Value", "string"),
                new(VsirMutationKind.Add, "representation.Value", "string"),
                new(VsirMutationKind.Add, "traits", "transform"),
                new(VsirMutationKind.Add, "input.Value", "string"),
                new(
                    VsirMutationKind.Set,
                    "construction",
                    "[{ensure: {condition: {intrinsic: not-whitespace, args: {value: input.Value}}, failure: {message: 'Debes especificar la extensión'}}}, {ensure: {condition: {intrinsic: length-between, args: {value: input.Value, min: 3, max: 16}}, failure: {message: 'Debe tener entre 3 y 16 caracteres'}}}, {refine: {intrinsic: split-first-rest, value: input.Value, as: {Name: name, Value: value}, failure: {message: 'Debes especificar un nombre y un valor, separados por espacio'}}}, {refine: {state: {Name: name, Value: value}}}]"),
                new(
                    VsirMutationKind.Set,
                    "representation.Value.mapping",
                    "{intrinsic: concat-space, values: [state.Name, state.Value]}")
            ]);

        Assert.True(core.IsSuccess, core.Error);
        var normalized = VsirSourceFormatter.FormatAfterMutation(core.Source!).Replace("\r\n", "\n");

        Assert.Contains("name: StreetExtension", normalized);
        Assert.Contains("intrinsic: concat-space", normalized);
        Assert.Contains("intrinsic: not-whitespace", normalized);
        Assert.Contains("intrinsic: length-between", normalized);
        Assert.Contains("intrinsic: split-first-rest", normalized);
        Assert.Contains("Name: name", normalized);
        Assert.Contains("Value: value", normalized);
    }
}
