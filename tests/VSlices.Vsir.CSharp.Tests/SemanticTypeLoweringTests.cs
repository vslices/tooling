using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class SemanticTypeLoweringTests
{
    private static string RulesetPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset");

    [Fact]
    public void Sequence_is_parsed_as_structural_semantic_type()
    {
        var parsed = VsirParser.Parse(SequenceSource("sequence"));

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var field = Assert.Single(parsed.Document!.State.Fields);
        Assert.Equal(
            new UnaryVsirType("sequence", new NamedVsirType("StreetExtension")),
            field.Type);
    }

    [Fact]
    public void Sequence_target_realization_is_owned_by_ruleset()
    {
        var parsed = VsirParser.Parse(SequenceSource("sequence"));
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var loaded = CSharpLoweringRuleSet.Load(
            RulesetPath,
            [new CSharpLoweringRule(
                "type.sequence",
                "deterministic",
                "type",
                "Seq<{value}>")]);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var lowered = CSharpLowerer.Lower(
            parsed.Document!,
            new("Identities.Domain.Entities", loaded.RuleSet!));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("record struct Repr(Seq<StreetExtension> Extensions)", lowered.Source);
        Assert.Contains("record struct Input(Seq<StreetExtension> Extensions)", lowered.Source);
        Assert.Contains("private readonly Seq<StreetExtension> _extensions;", lowered.Source);
    }

    [Fact]
    public void Unary_type_constructor_name_is_not_whitelisted_by_tooling()
    {
        var parsed = VsirParser.Parse(SequenceSource("vendor-sequence"));
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var loaded = CSharpLoweringRuleSet.Load(
            RulesetPath,
            [new CSharpLoweringRule(
                "type.vendor-sequence",
                "deterministic",
                "type",
                "VendorSequence<{value}>")]);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var lowered = CSharpLowerer.Lower(
            parsed.Document!,
            new("Identities.Domain.Entities", loaded.RuleSet!));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("VendorSequence<StreetExtension>", lowered.Source);
    }

    [Fact]
    public void Missing_type_realization_stops_at_ruleset_boundary()
    {
        var parsed = VsirParser.Parse(SequenceSource("sequence"));
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var loaded = CSharpLoweringRuleSet.Load(RulesetPath);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var lowered = CSharpLowerer.Lower(
            parsed.Document!,
            new("Identities.Domain.Entities", loaded.RuleSet!));

        Assert.False(lowered.IsSuccess);
        Assert.Contains(lowered.Diagnostics, x => x.Code == "CSL050");
    }

    private static string SequenceSource(string constructor) => $$"""
        vsir: 0.1
        kind: domain-type
        name: SequenceHolder
        classification: value-object
        shape: product
        traits: [transform]

        state:
          Extensions:
            {{constructor}}: StreetExtension

        representation:
          Extensions:
            {{constructor}}: StreetExtension

        construction:
          input:
            Extensions:
              {{constructor}}: StreetExtension
        """;
}
