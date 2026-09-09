using VSlices.Vsir;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirCanonicalStructuralContractTests
{
    [Theory]
    [MemberData(nameof(CanonicalForms))]
    public void Canonical_forms_reject_non_scalar_root_semantic_keys(string source)
    {
        var withUnknown = source.Replace(
            "name:",
            "? [unsupported, semantic]\n: true\nname:",
            StringComparison.Ordinal);

        var parsed = VsirParser.Parse(withUnknown);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR104");
    }

    [Fact]
    public void Product_rejects_non_scalar_from_instead_of_treating_it_as_absent()
    {
        var source = ProductSource.Replace(
            "Value: string\n\n        input:",
            "Value:\n            type: string\n            from: [state.Value]\n\n        input:",
            StringComparison.Ordinal);

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR153");
    }

    [Fact]
    public void Sum_representation_rejects_simultaneous_from_and_mapping()
    {
        var source = SumSource.Replace(
            "Value: string\n            input:",
            "Value:\n                type: string\n                from: state.Value\n                mapping:\n                  stringify: state.Value\n            input:",
            StringComparison.Ordinal);

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR155");
    }

    public static IEnumerable<object[]> CanonicalForms()
    {
        yield return [ProductSource];
        yield return [SumSource];
        yield return [MaintainedSource];
    }

    private const string ProductSource = """
        vsir: 0.1
        kind: domain-type
        name: Probe
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

    private const string SumSource = """
        vsir: 0.1
        kind: domain-type
        name: ProbeSum
        shape: sum
        classification: value-object

        state: {}
        representation: {}

        variants:
          ProbeValue:
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            construction:
              - refine:
                  state:
                    Value: input.Value
        """;

    private const string MaintainedSource = """
        vsir: 0.1
        kind: domain-type
        name: ProbeMaintained
        shape: product
        classification: maintained

        state:
          Name: string

        representation:
          Value:
            type: string
            from: state.Name

        values:
          One:
            state:
              Name: One

        equality:
          intrinsic: ordinal-equals
          by: state.Name
        """;
}
