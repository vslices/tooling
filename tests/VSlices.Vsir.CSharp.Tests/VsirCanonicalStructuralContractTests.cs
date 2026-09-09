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

    [Theory]
    [MemberData(nameof(ExpandedFormsWithUnknownDeclarationKey))]
    public void Canonical_forms_share_unknown_expanded_field_key_rejection(string source)
    {
        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR104");
    }

    [Theory]
    [MemberData(nameof(ExpandedFormsWithoutType))]
    public void Canonical_forms_require_type_when_source_metadata_expands_a_field(string source)
    {
        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR156");
    }

    [Fact]
    public void Product_rejects_non_scalar_from_instead_of_treating_it_as_absent()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Probe
            shape: product
            classification: value-object
            traits: [transform]

            state:
              Value: string

            representation:
              Value:
                type: string
                from: [state.Value]

            input:
              Value: string
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR153");
    }

    [Fact]
    public void Sum_representation_rejects_simultaneous_from_and_mapping()
    {
        const string source = """
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
                  Value:
                    type: string
                    from: state.Value
                    mapping:
                      stringify: state.Value
                input:
                  Value: string
                construction:
                  - refine:
                      state:
                        Value: input.Value
            """;

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

    public static IEnumerable<object[]> ExpandedFormsWithUnknownDeclarationKey()
    {
        yield return ["""
            vsir: 0.1
            kind: domain-type
            name: Probe
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value:
                type: string
                from: state.Value
                unsupported: true
            input:
              Value: string
            """];

        yield return ["""
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
                  Value:
                    type: string
                    from: state.Value
                    unsupported: true
                input:
                  Value: string
                construction:
                  - refine:
                      state:
                        Value: input.Value
            """];

        yield return ["""
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
                unsupported: true
            values:
              One:
                state:
                  Name: One
            equality:
              intrinsic: ordinal-equals
              by: state.Name
            """];
    }

    public static IEnumerable<object[]> ExpandedFormsWithoutType()
    {
        yield return ["""
            vsir: 0.1
            kind: domain-type
            name: Probe
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value:
                from: state.Value
            input:
              Value: string
            """];

        yield return ["""
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
                  Value:
                    from: state.Value
                input:
                  Value: string
                construction:
                  - refine:
                      state:
                        Value: input.Value
            """];

        yield return ["""
            vsir: 0.1
            kind: domain-type
            name: ProbeMaintained
            shape: product
            classification: maintained
            state:
              Name: string
            representation:
              Value:
                from: state.Name
            values:
              One:
                state:
                  Name: One
            equality:
              intrinsic: ordinal-equals
              by: state.Name
            """];
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
