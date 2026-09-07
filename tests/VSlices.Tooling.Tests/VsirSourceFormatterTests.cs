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
}
