using VSlices.Vsir;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirNominalTypeReferencesTests
{
    [Fact]
    public void Variant_only_nominal_types_are_part_of_the_semantic_dependency_set()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Probe
            shape: sum
            classification: value-object

            state: {}
            representation: {}

            variants:
              WithWidget:
                traits: [transform]
                state:
                  Value:
                    optional: Widget
                representation:
                  Value:
                    optional: Widget
                input:
                  Value:
                    optional: Widget
                construction:
                  - refine:
                      state:
                        Value: input.Value
            """;

        var parsed = VsirParser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var references = VsirNominalTypeReferences.Enumerate(parsed.Document!);

        Assert.Contains("Widget", references, StringComparer.Ordinal);
    }
}
