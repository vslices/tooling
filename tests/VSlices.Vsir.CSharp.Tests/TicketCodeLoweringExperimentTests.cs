using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class TicketCodeLoweringExperimentTests
{
    private const string TicketCodeSource = """
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

    [Fact]
    public void Normalize_is_preserved_and_missing_target_knowledge_is_explicit()
    {
        var parsed = VsirParser.Parse(TicketCodeSource);

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

        var rules = LoadFixtureRules();
        var lowered = CSharpLowerer.Lower(
            parsed.Document,
            new CSharpLoweringContext("Tickets.Domain.Aggregates", rules));

        Assert.False(lowered.IsSuccess);
        Assert.Null(lowered.Source);
        Assert.Contains(lowered.Diagnostics, diagnostic =>
            diagnostic.Code == "CSL031" &&
            diagnostic.Message.Contains("intrinsic.trim", StringComparison.Ordinal));
        Assert.DoesNotContain(lowered.Diagnostics, diagnostic => diagnostic.Code == "CSL030");
    }

    [Fact]
    public void Existing_expression_renderer_is_sufficient_for_trim_and_normalized_value_flows_forward()
    {
        var parsed = VsirParser.Parse(TicketCodeSource);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var rules = LoadRulesWithTrim();
        var lowered = CSharpLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Tickets.Domain.Aggregates", rules));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.NotNull(lowered.Source);
        Assert.Contains(
            "!string.IsNullOrEmpty(input.Value.Trim())",
            lowered.Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "new(input.Value.Trim())",
            lowered.Source,
            StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(_value, other._value, StringComparison.Ordinal)",
            lowered.Source,
            StringComparison.Ordinal);
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

    private static CSharpLoweringRuleSet LoadFixtureRules()
    {
        var loaded = CSharpLoweringRuleSet.Load(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset"));
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));
        return loaded.RuleSet!;
    }

    private static CSharpLoweringRuleSet LoadRulesWithTrim()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-ticket-code-" + Guid.NewGuid().ToString("N"));
        var csharp = Path.Combine(root, "csharp");
        Directory.CreateDirectory(csharp);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "manifest.yaml"),
                """
                targets:
                  csharp:
                    rules:
                      - csharp/intrinsics.yaml
                """);

            File.WriteAllText(
                Path.Combine(csharp, "intrinsics.yaml"),
                """
                rules:
                  - node: intrinsic.trim
                    mode: deterministic
                    renderer: expression
                    template: "{value}.Trim()"

                  - node: intrinsic.non-empty
                    mode: deterministic
                    renderer: expression
                    template: "!string.IsNullOrEmpty({value})"

                  - node: equality.ordinal-equals.equals
                    mode: deterministic
                    renderer: expression
                    template: "string.Equals({left}, {right}, StringComparison.Ordinal)"

                  - node: equality.ordinal-equals.hash
                    mode: deterministic
                    renderer: expression
                    template: "StringComparer.Ordinal.GetHashCode({value})"
                """);

            var loaded = CSharpLoweringRuleSet.Load(root);
            Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));
            return loaded.RuleSet!;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
