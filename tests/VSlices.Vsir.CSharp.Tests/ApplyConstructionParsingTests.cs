using VSlices.Vsir;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class ApplyConstructionParsingTests
{
    [Fact]
    public void Apply_uses_over_for_direct_nested_construction()
    {
        var parsed = VsirParser.Parse("""
            vsir: 0.1
            kind: domain-type
            name: Holder
            classification: value-object
            shape: product
            traits: [transform]

            state:
              Value: string

            representation:
              Value: string

            construction:
              input:
                Value: string
              steps:
                - apply:
                    over: StreetName
                    input:
                      Value: input.Value
                    as: street
            """);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var apply = Assert.IsType<ApplyStep>(Assert.Single(parsed.Document!.Construction.Steps));
        Assert.Equal("StreetName", apply.Over);
        Assert.Equal("street", apply.As);

        var input = Assert.IsType<DirectApplyInput>(apply.Input);
        Assert.Equal("input.Value", input.Fields["Value"]);
    }

    [Fact]
    public void Apply_uses_mapped_input_instead_of_apply_seq_keyword()
    {
        var parsed = VsirParser.Parse("""
            vsir: 0.1
            kind: domain-type
            name: Holder
            classification: value-object
            shape: product
            traits: [transform]

            state:
              Values:
                sequence: string

            representation:
              Values:
                sequence: string

            construction:
              input:
                Values:
                  sequence: string
              steps:
                - apply:
                    over: StreetExtension
                    input:
                      source: input.Values
                      map:
                        Value: item
                    as: extensions
            """);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var apply = Assert.IsType<ApplyStep>(Assert.Single(parsed.Document!.Construction.Steps));
        var input = Assert.IsType<MappedApplyInput>(apply.Input);
        Assert.Equal("input.Values", input.Source);
        Assert.Equal("item", input.Map["Value"]);
    }

    [Fact]
    public void Domain_is_not_an_alias_for_apply_over()
    {
        var parsed = VsirParser.Parse("""
            vsir: 0.1
            kind: domain-type
            name: Holder
            classification: value-object
            shape: product
            traits: [transform]

            state:
              Value: string

            representation:
              Value: string

            construction:
              input:
                Value: string
              steps:
                - apply:
                    domain: StreetName
                    input:
                      Value: input.Value
                    as: street
            """);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, x =>
            x.Code == "VSIR104" &&
            x.Message.Contains("construction.steps[].apply.domain", StringComparison.Ordinal));
        Assert.Contains(parsed.Diagnostics, x => x.Code == "VSIR119");
    }

    [Fact]
    public void Apply_seq_is_not_a_second_construction_verb()
    {
        var parsed = VsirParser.Parse("""
            vsir: 0.1
            kind: domain-type
            name: Holder
            classification: value-object
            shape: product
            traits: [transform]

            state:
              Values:
                sequence: string

            representation:
              Values:
                sequence: string

            construction:
              input:
                Values:
                  sequence: string
              steps:
                - apply-seq:
                    over: StreetExtension
                    input: input.Values
                    map:
                      Value: item
                    as: extensions
            """);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, x => x.Code == "VSIR100");
    }
}
