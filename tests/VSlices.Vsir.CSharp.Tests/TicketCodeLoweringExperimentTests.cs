using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class TicketCodeLoweringExperimentTests
{
    [Fact]
    public void Normalize_is_preserved_and_csharp_lowering_stops_explicitly()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: TicketCode
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
                - normalize:
                    target: input.Value
                    intrinsic: trim
                - ensure:
                    condition:
                      intrinsic: non-empty
                      value: input.Value
                    failure:
                      message: Debes especificar el correlativo de la solicitud

            equality:
              intrinsic: ordinal-equals
              by: state.Value
            """;

        var parsed = VsirParser.Parse(source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Collection(
            parsed.Document!.Construction.Steps,
            step =>
            {
                var normalize = Assert.IsType<NormalizeStep>(step);
                Assert.Equal("input.Value", normalize.Target);
                Assert.Equal("trim", normalize.Intrinsic);
            },
            step =>
            {
                var ensure = Assert.IsType<EnsureStep>(step);
                var condition = Assert.IsType<NonEmptyCondition>(ensure.Condition);
                Assert.Equal("input.Value", condition.Value);
            });

        var rules = CSharpLoweringRuleSet.Load(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset"));
        Assert.True(rules.IsSuccess, string.Join(Environment.NewLine, rules.Diagnostics));

        var lowered = CSharpLowerer.Lower(
            parsed.Document,
            new CSharpLoweringContext("Tickets.Domain.Aggregates", rules.RuleSet!));

        Assert.False(lowered.IsSuccess);
        Assert.Null(lowered.Source);
        Assert.Contains(lowered.Diagnostics, diagnostic =>
            diagnostic.Code == "CSL030" &&
            diagnostic.Message.Contains("trim", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("input.Value", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_normalize_semantics_are_rejected_instead_of_disappearing()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: TicketCode
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
                - normalize:
                    target: input.Value
                    intrinsic: trim
                    imaginary-new-semantic: true
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic =>
            diagnostic.Code == "VSIR104" &&
            diagnostic.Message.Contains(
                "construction.steps[].normalize.imaginary-new-semantic",
                StringComparison.Ordinal));
    }
}
