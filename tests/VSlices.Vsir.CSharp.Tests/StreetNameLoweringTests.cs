using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class StreetNameLoweringTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "StreetName.vsir");

    [Fact]
    public async Task StreetName_can_be_parsed()
    {
        var parsed = VsirParser.Parse(await File.ReadAllTextAsync(FixturePath));

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal("StreetName", parsed.Document!.Name);
        Assert.Equal("value-object", parsed.Document.Classification);
        Assert.Equal("product", parsed.Document.Shape);
        Assert.Collection(
            parsed.Document.Construction.Steps,
            x => Assert.IsType<EnsureStep>(x),
            x => Assert.IsType<EnsureStep>(x));
    }

    [Fact]
    public async Task StreetName_lowering_is_deterministic()
    {
        var parsed = VsirParser.Parse(await File.ReadAllTextAsync(FixturePath));
        var context = new CSharpLoweringContext("Identities.Domain.ValueObjects");

        var first = CSharpLowerer.Lower(parsed.Document!, context);
        var second = CSharpLowerer.Lower(parsed.Document!, context);

        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics));
        Assert.Equal(first.Source, second.Source);
    }

    [Fact]
    public async Task StreetName_lowering_preserves_the_current_semantic_contract()
    {
        var parsed = VsirParser.Parse(await File.ReadAllTextAsync(FixturePath));
        var result = CSharpLowerer.Lower(parsed.Document!, new("Identities.Domain.ValueObjects"));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Contains("sealed class StreetName", result.Source);
        Assert.Contains("DomainType<StreetName, StreetName.Repr>", result.Source);
        Assert.Contains("Transform<StreetName, StreetName.Input>", result.Source);
        Assert.Contains("!string.IsNullOrEmpty(input.Value)", result.Source);
        Assert.Contains("input.Value.Length <= 30", result.Source);
        Assert.Contains("{length}", result.Source);
        Assert.Contains("new(input.Value)", result.Source);
        Assert.Contains("new(_value)", result.Source);
    }

    [Fact]
    public void Unknown_intrinsic_is_rejected()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Something
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
                - ensure:
                    condition:
                      intrinsic: probably-valid
                      value: input.Value
                    failure:
                      message: no
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, x => x.Code == "VSIR102");
    }

    [Fact]
    public void Missing_state_mapping_is_rejected_instead_of_guessed()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Something
            classification: value-object
            shape: product
            traits: [transform]
            state:
              Name: string
            representation:
              Name: string
            construction:
              input:
                Value: string
              steps: []
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, x => x.Code == "VSIR209");
    }
}
