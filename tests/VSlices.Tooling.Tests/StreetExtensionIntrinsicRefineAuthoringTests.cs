using VSlices.Vsir;

namespace VSlices.Tooling.Tests;

public sealed class StreetExtensionIntrinsicRefineAuthoringTests
{
    private const string Construction = "[{ensure: {condition: {intrinsic: not-whitespace, args: {value: input.Value}}, failure: {message: 'Debes especificar la extensión'}}}, {ensure: {condition: {intrinsic: length-between, args: {value: input.Value, min: 3, max: 16}}, failure: {message: 'Debe tener entre 3 y 16 caracteres'}}}, {refine: {intrinsic: split-first-rest, value: input.Value, as: {Name: name, Value: value}, failure: {message: 'Debes especificar un nombre y un valor, separados por espacio'}}}, {refine: {state: {Name: name, Value: value}}}]";

    [Fact]
    public void Discovery_advertises_intrinsic_refine_with_named_output_bindings()
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
            """;

        var frontier = VsirMutationPipeline.Discover(source, out var error);

        Assert.Null(error);
        var construction = Assert.Single(frontier, item => item.Path == "construction");
        var grammar = Assert.IsType<VsirValueGrammar>(VsirGrammarDiscovery.For(construction));
        var refine = Assert.Single(grammar.Forms, form => form.Name == "refine-intrinsic");

        Assert.Equal(
            "{refine: {intrinsic: <ruleset-intrinsic>, value: <expression>, as: <output-binding-map>, failure: {message: <text>}}}",
            refine.Template);
        Assert.Contains(refine.Slots, slot => slot.Name == "intrinsic" && slot.ValueKind == "ruleset-intrinsic");
        Assert.Contains(refine.Slots, slot => slot.Name == "value" && slot.ValueKind == "expression");
        Assert.Contains(refine.Slots, slot => slot.Name == "as" && slot.ValueKind == "output-binding-map");
        Assert.Contains(refine.Slots, slot => slot.Name == "message" && slot.ValueKind == "text");
    }

    [Fact]
    public void Update_accepts_StreetExtension_intrinsic_refine_and_later_state_binding()
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
              Value:
                type: string
                mapping:
                  intrinsic: concat-space
                  values:
                  - state.Name
                  - state.Value
            input:
              Value: string
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "construction", Construction)]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("intrinsic: split-first-rest", result.Source);
        Assert.Contains("Name: name", result.Source);
        Assert.Contains("Value: value", result.Source);
        Assert.Contains("state:", result.Source);
    }

    [Fact]
    public void Duplicate_intrinsic_refine_binding_remains_authorable_but_fails_semantic_conformance()
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
              Value:
                type: string
                mapping:
                  intrinsic: concat-space
                  values:
                  - state.Name
                  - state.Value
            input:
              Value: string
            """;

        var invalid = "[{refine: {intrinsic: split-first-rest, value: input.Value, as: {Name: part, Value: part}, failure: {message: invalid}}}, {refine: {state: {Name: part, Value: part}}}]";
        var update = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "construction", invalid)]);

        // update protects the progressive authoring grammar, not final semantic conformance;
        // discovery/conformance must therefore be able to explain the invalid candidate.
        Assert.True(update.IsSuccess, update.Error);

        var parsed = VsirParser.Parse(update.Source!);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR243");
    }
}
