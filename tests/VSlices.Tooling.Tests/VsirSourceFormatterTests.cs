namespace VSlices.Tooling.Tests;

public sealed class VsirSourceFormatterTests
{
    [Fact]
    public void Equality_flow_mapping_is_persisted_as_block_mapping_without_document_markers()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            shape: product
            classification: value-object
            traits: [transform, identifier]
            state:
              Name: string
            representation:
              Value:
                type: string
                from: state.Name
            input: string
            construction:
              - refine:
                  value: input
                  as: state.Name
            """;

        var mutated = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "equality", "{intrinsic: ordinal-equals, by: state.Name}")]);

        Assert.True(mutated.IsSuccess, mutated.Error);

        var formatted = VsirSourceFormatter.FormatAfterMutation(mutated.Source!);
        var normalized = formatted.Replace("\r\n", "\n");

        Assert.Contains("equality:\n  intrinsic: ordinal-equals\n  by: state.Name", normalized);
        Assert.DoesNotContain("equality: {", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("\n...", normalized, StringComparison.Ordinal);
        Assert.False(normalized.StartsWith("---", StringComparison.Ordinal));
        Assert.EndsWith("\n", normalized, StringComparison.Ordinal);
        Assert.False(normalized.EndsWith("\n\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Construction_flow_sequence_is_persisted_as_recursive_block_yaml_without_expanding_scalar_sets()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName2
            shape: product
            classification: value-object
            traits: [transform]
            tags: [ticket]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            construction: [{ensure: {condition: {intrinsic: non-empty, args: {value: input.Value}}, failure: {message: required}}}, {ensure: {condition: {intrinsic: length-at-most, args: {value: input.Value, max: 30}}, failure: {message: too long}}}]
            """;

        var formatted = VsirSourceFormatter.FormatAfterMutation(source);
        var normalized = formatted.Replace("\r\n", "\n");

        Assert.Matches("construction:\\n\\s*- ensure:\\n", normalized);
        Assert.Contains("condition:\n", normalized);
        Assert.Contains("args:\n", normalized);
        Assert.Contains("failure:\n", normalized);
        Assert.DoesNotContain("construction: [", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("- {ensure:", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("condition: {", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("args: {", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("failure: {", normalized, StringComparison.Ordinal);
        Assert.Contains("traits: [transform]", normalized);
        Assert.Contains("tags: [ticket]", normalized);
    }
}
