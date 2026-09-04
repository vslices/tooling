using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirParserSemanticConservationTests
{
    [Fact]
    public void TicketId_semantics_are_represented_without_being_lowered_implicitly()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: TicketIdLike
            classification: value-object
            shape: product
            traits: [identifier, transform]

            state:
              Value: string

            representation:
              Value: string

            construction:
              input:
                Value: string

            equality:
              intrinsic: ordinal-equals
              by: state.Value
            """;

        var parsed = VsirParser.Parse(source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal(["identifier", "transform"], parsed.Document!.Traits);
        Assert.Equal(new EqualitySemantics("ordinal-equals", "state.Value"), parsed.Document.Equality);

        var rules = CSharpLoweringRuleSet.Load(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset"));
        Assert.True(rules.IsSuccess, string.Join(Environment.NewLine, rules.Diagnostics));

        var lowered = CSharpLowerer.Lower(
            parsed.Document,
            new CSharpLoweringContext("Tickets.Domain.Aggregates", rules.RuleSet!));

        Assert.False(lowered.IsSuccess);
        Assert.Contains(lowered.Diagnostics, diagnostic => diagnostic.Code == "CSL020");
        Assert.Contains(lowered.Diagnostics, diagnostic => diagnostic.Code == "CSL021");
    }

    [Fact]
    public void Unsupported_root_semantics_are_still_rejected_instead_of_silently_discarded()
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

            lifecycle:
              imaginary: true
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic =>
            diagnostic.Code == "VSIR104" && diagnostic.Message.Contains("lifecycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Equality_must_reference_known_state()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: BrokenIdentifier
            classification: value-object
            shape: product
            traits: [identifier, transform]

            state:
              Value: string

            representation:
              Value: string

            construction:
              input:
                Value: string

            equality:
              intrinsic: ordinal-equals
              by: state.Missing
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR215");
    }
}
