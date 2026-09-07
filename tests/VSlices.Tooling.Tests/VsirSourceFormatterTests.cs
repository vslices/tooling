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
            classification: maintained
            state:
              Name: string
            representation:
              Value:
                type: string
                from: state.Name
            values:
              Natural:
                state:
                  Name: Natural
              Juridical:
                state:
                  Name: Juridica
            """;

        var mutated = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "equality", "{intrinsic: ordinal-equals, by: state.Name}")]);

        Assert.True(mutated.IsSuccess, mutated.Error);

        var formatted = VsirSourceFormatter.FormatAfterMutation(mutated.Source!);

        Assert.Contains("equality:\n  intrinsic: ordinal-equals\n  by: state.Name", formatted.Replace("\r\n", "\n"));
        Assert.DoesNotContain("equality: {", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("\n...", formatted.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotStartWith("---", formatted, StringComparison.Ordinal);
        Assert.True(formatted.EndsWith("\n", StringComparison.Ordinal));
        Assert.False(formatted.EndsWith("\n\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Formatter_preserves_windows_newline_convention()
    {
        var source = "vsir: 0.1\r\nname: Example\r\n";
        var serialized = "vsir: 0.1\r\nname: Example\r\n...\r\n";

        var formatted = VsirSourceFormatter.FormatAfterMutation(serialized.Replace("\r\n", "\n"));
        var normalized = formatted.Replace("\r\n", "\n");

        Assert.DoesNotContain("...", normalized, StringComparison.Ordinal);
        Assert.EndsWith("\n", normalized, StringComparison.Ordinal);
    }
}
