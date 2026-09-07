using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class TicketIdLoweringTests
{
    private const string Source = """
        vsir: 0.1
        kind: domain-type
        name: TicketId
        classification: identifier
        shape: product
        traits: [transform]

        state:
          Value: string

        representation:
          Value: string

        input:
          Value: string

        equality:
          intrinsic: ordinal-equals
          by: state.Value
        """;

    private static string RulesetPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset");

    [Fact]
    public void TicketId_corpus_is_canonical_without_identifier_trait_or_construction_steps()
    {
        var parsed = VsirParser.Parse(Source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
        Assert.Equal("identifier", parsed.Document!.Classification);
        Assert.Equal(["transform"], parsed.Document.Traits);
        Assert.Empty(parsed.Document.Construction.Steps);
        Assert.Equal("ordinal-equals", parsed.Document.Equality!.Intrinsic);
        Assert.Equal("state.Value", parsed.Document.Equality.By);
    }

    [Fact]
    public void TicketId_classification_lowers_to_identifier_with_direct_product_transform()
    {
        var parsed = VsirParser.Parse(Source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));

        var loaded = CSharpLoweringRuleSet.Load(RulesetPath);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var lowered = CSharpLanguageLowerer.Lower(
            parsed.Document!,
            new("TicketSupport.Domain", loaded.RuleSet!));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("Identifier<TicketId, TicketId.Repr>", lowered.Source);
        Assert.Contains("Transform<TicketId, TicketId.Input>", lowered.Source);
        Assert.Contains("record struct Input(string Value)", lowered.Source);
        Assert.Contains("Transform((TicketId.Input input) => Instance(input))", lowered.Source);
        Assert.Contains("new(input.Value)", lowered.Source);
        Assert.Contains("StringComparer.Ordinal.Equals(_value, other._value)", lowered.Source);
        Assert.Contains("StringComparer.Ordinal.GetHashCode(_value)", lowered.Source);
    }
}
