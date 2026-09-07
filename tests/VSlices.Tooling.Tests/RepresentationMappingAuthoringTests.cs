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
    public void Discovery_exposes_only_set_for_new_representation_sources()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetExtension
            shape: product
            classification: value-object
            state:
              Name: string
              Value: string
            representation:
              Value: string
            """;

        var frontier = VsirMutationPipeline.Discover(source, out var error);

        Assert.Null(error);

        var mapping = Assert.Single(frontier, item => item.Path == "representation.Value.mapping");
        Assert.Equal(VsirFrontierStatus.Optional, mapping.Status);
        Assert.Equal("mapping", mapping.ValueKind);
        Assert.Single(mapping.Operations);
        Assert.Contains(VsirMutationKind.Set, mapping.Operations);

        var from = Assert.Single(frontier, item => item.Path == "representation.Value.from");
        Assert.Single(from.Operations);
        Assert.Contains(VsirMutationKind.Set, from.Operations);
    }

    [Fact]
    public void Discovery_does_not_offer_mapping_while_direct_from_source_is_active()
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

        var frontier = VsirMutationPipeline.Discover(source, out var error);

        Assert.Null(error);
        Assert.DoesNotContain(frontier, item => item.Path == "representation.Value.mapping");

        var from = Assert.Single(frontier, item => item.Path == "representation.Value.from");
        Assert.Contains(VsirMutationKind.Remove, from.Operations);
        Assert.Contains(VsirMutationKind.Set, from.Operations);
        Assert.DoesNotContain(VsirMutationKind.Add, from.Operations);
    }

    [Fact]
    public void Discovery_does_not_offer_direct_from_while_mapping_source_is_active()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetExtension
            shape: product
            classification: value-object
            state:
              Name: string
              Value: string
            representation:
              Value:
                type: string
                mapping:
                  intrinsic: concat-space
                  values:
                    - state.Name
                    - state.Value
            """;

        var frontier = VsirMutationPipeline.Discover(source, out var error);

        Assert.Null(error);
        Assert.DoesNotContain(frontier, item => item.Path == "representation.Value.from");

        var mapping = Assert.Single(frontier, item => item.Path == "representation.Value.mapping");
        Assert.Single(mapping.Operations);
        Assert.Contains(VsirMutationKind.Set, mapping.Operations);
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
    public void Representation_mapping_add_is_rejected_as_non_collection_semantics()
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
        Assert.StartsWith("UPDATE044:", result.Error);
    }

    [Fact]
    public void StreetExtension_can_be_authored_step_by_step_through_discovery_and_supported_updates()
    {
        var created = VsirTemplate.Create(
            "StreetExtension",
            "domain-type",
            "product",
            "value-object",
            []);

        Assert.True(created.IsSuccess, created.Error);
        var current = created.Source!;

        var initialFrontier = VsirMutationPipeline.Discover(current, out var initialError);
        Assert.Null(initialError);
        AssertCan(initialFrontier, "state", VsirMutationKind.Set);
        AssertCan(initialFrontier, "representation", VsirMutationKind.Set);
        AssertCan(initialFrontier, "traits", VsirMutationKind.Add);

        current = Apply(
            current,
            new(VsirMutationKind.Set, "state.Name", "string"),
            new(VsirMutationKind.Set, "state.Value", "string"),
            new(VsirMutationKind.Set, "representation.Value", "string"),
            new(VsirMutationKind.Add, "traits", "transform"));

        var transformFrontier = VsirMutationPipeline.Discover(current, out var transformError);
        Assert.Null(transformError);
        AssertCan(transformFrontier, "input", VsirMutationKind.Set);
        AssertCan(transformFrontier, "construction", VsirMutationKind.Set);
        AssertCan(transformFrontier, "representation.Value.mapping", VsirMutationKind.Set);
        AssertCan(transformFrontier, "representation.Value.from", VsirMutationKind.Set);

        current = Apply(current, new(VsirMutationKind.Set, "input.Value", "string"));

        var inputFrontier = VsirMutationPipeline.Discover(current, out var inputError);
        Assert.Null(inputError);
        Assert.DoesNotContain(inputFrontier, item => item.Path == "input");
        AssertCan(inputFrontier, "construction", VsirMutationKind.Set);
        AssertCan(inputFrontier, "representation.Value.mapping", VsirMutationKind.Set);

        current = Apply(
            current,
            new(
                VsirMutationKind.Set,
                "representation.Value.mapping",
                "{intrinsic: concat-space, values: [state.Name, state.Value]}"));

        var mappedFrontier = VsirMutationPipeline.Discover(current, out var mappedError);
        Assert.Null(mappedError);
        AssertCan(mappedFrontier, "representation.Value.mapping", VsirMutationKind.Set);
        Assert.DoesNotContain(mappedFrontier, item => item.Path == "representation.Value.from");
        AssertCan(mappedFrontier, "construction", VsirMutationKind.Set);

        var construction = "[{ensure: {condition: {intrinsic: not-whitespace, args: {value: input.Value}}, failure: {message: 'Debes especificar la extensión'}}}, {ensure: {condition: {intrinsic: length-between, args: {value: input.Value, min: 3, max: 16}}, failure: {message: 'Debe tener entre 3 y 16 caracteres'}}}, {refine: {intrinsic: split-first-rest, value: input.Value, as: {Name: name, Value: value}, failure: {message: 'Debes especificar un nombre y un valor, separados por espacio'}}}, {refine: {state: {Name: name, Value: value}}}]";
        current = Apply(current, new(VsirMutationKind.Set, "construction", construction));

        var completeFrontier = VsirMutationPipeline.Discover(current, out var completeError);
        Assert.Null(completeError);
        Assert.DoesNotContain(completeFrontier, item => item.Path == "input");
        Assert.DoesNotContain(completeFrontier, item => item.Path == "construction");
        AssertCan(completeFrontier, "representation.Value.mapping", VsirMutationKind.Set);

        var normalized = VsirSourceFormatter.FormatAfterMutation(current).Replace("\r\n", "\n");
        Assert.Contains("name: StreetExtension", normalized);
        Assert.Contains("intrinsic: concat-space", normalized);
        Assert.Contains("intrinsic: not-whitespace", normalized);
        Assert.Contains("intrinsic: length-between", normalized);
        Assert.Contains("intrinsic: split-first-rest", normalized);
        Assert.Contains("Name: name", normalized);
        Assert.Contains("Value: value", normalized);
    }

    private static string Apply(string source, params VsirMutation[] mutations)
    {
        var result = VsirMutationPipeline.Apply(source, mutations);
        Assert.True(result.IsSuccess, result.Error);
        return result.Source!;
    }

    private static void AssertCan(
        IReadOnlyList<VsirPathContract> frontier,
        string path,
        VsirMutationKind operation)
    {
        var contract = Assert.Single(frontier, item => item.Path == path);
        Assert.Contains(operation, contract.Operations);
    }
}
