using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirParserSemanticConservationTests
{
    [Theory]
    [InlineData("identifier, transform")]
    [InlineData("transform, identifier")]
    public void TicketId_traits_are_unordered_capabilities(string traits)
    {
        var source = TicketIdLike($"[{traits}]");
        var parsed = VsirParser.Parse(source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Contains("identifier", parsed.Document!.Traits);
        Assert.Contains("transform", parsed.Document.Traits);
    }

    [Fact]
    public void TicketId_semantics_are_preserved_and_lowered_through_identifier_structure_and_ruleset_equality()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[identifier, transform]"));
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal(new EqualitySemantics("ordinal-equals", "state.Value"), parsed.Document!.Equality);

        var rules = CSharpLoweringRuleSet.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset"));
        Assert.True(rules.IsSuccess, string.Join(Environment.NewLine, rules.Diagnostics));

        var lowered = CSharpLowerer.Lower(parsed.Document, new CSharpLoweringContext("Tickets.Domain.Aggregates", rules.RuleSet!));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("Identifier<TicketIdLike, TicketIdLike.Repr>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Transform<TicketIdLike, TicketIdLike.Input>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("VSlices.Arrows.Req<Input, TicketIdLike>.Transform((Input input) => Instance(input));", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(_value, other._value, StringComparison.Ordinal)", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("StringComparer.Ordinal.GetHashCode(_value)", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public static bool operator ==", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public static bool operator !=", lowered.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainType<TicketIdLike, TicketIdLike.Repr>", lowered.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Identifier_lowering_stops_when_equality_ruleset_knowledge_is_missing()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[identifier, transform]"));
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var temporary = Path.Combine(Path.GetTempPath(), "vslices-ruleset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temporary, "csharp"));

        try
        {
            File.WriteAllText(Path.Combine(temporary, "manifest.yaml"), """
                kind: vslices-ruleset
                version: 0.1
                targets:
                  csharp:
                    rules:
                      - csharp/intrinsics.yaml
                """);
            File.WriteAllText(Path.Combine(temporary, "csharp", "intrinsics.yaml"), """
                rules:
                  - node: intrinsic.non-empty
                    mode: deterministic
                    renderer: expression
                    template: "!string.IsNullOrEmpty({value})"
                """);

            var rules = CSharpLoweringRuleSet.Load(temporary);
            Assert.True(rules.IsSuccess, string.Join(Environment.NewLine, rules.Diagnostics));

            var lowered = CSharpLowerer.Lower(parsed.Document!, new CSharpLoweringContext("Tickets.Domain.Aggregates", rules.RuleSet!));

            Assert.False(lowered.IsSuccess);
            Assert.Contains(lowered.Diagnostics, diagnostic => diagnostic.Code == "CSL021");
            Assert.Contains(lowered.Diagnostics, diagnostic => diagnostic.Code == "CSL022");
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void Identifier_requires_explicit_equality_semantics()
    {
        var source = TicketIdLike("[identifier, transform]")
            .Replace("\nequality:\n  intrinsic: ordinal-equals\n  by: state.Value\n", string.Empty, StringComparison.Ordinal);

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR216");
    }

    [Fact]
    public void Duplicate_traits_are_rejected()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[identifier, transform, identifier]"));

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR217");
    }

    [Fact]
    public void Unknown_traits_are_rejected()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[transform, imaginary]"));

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR218");
    }

    [Fact]
    public void Unsupported_root_semantics_are_still_rejected_instead_of_silently_discarded()
    {
        var source = TicketIdLike("[identifier, transform]") + "\nlifecycle:\n  imaginary: true\n";
        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR104" && diagnostic.Message.Contains("lifecycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_equality_semantics_are_rejected()
    {
        var source = TicketIdLike("[identifier, transform]")
            .Replace("  by: state.Value", "  by: state.Value\n  imaginary-new-semantic: true", StringComparison.Ordinal);

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR104" && diagnostic.Message.Contains("equality.imaginary-new-semantic", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_condition_semantics_are_rejected()
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
                      intrinsic: non-empty
                      value: input.Value
                      retry: true
                    failure:
                      message: required
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR104" && diagnostic.Message.Contains("condition.retry", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_failure_semantics_are_rejected()
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
                      intrinsic: non-empty
                      value: input.Value
                    failure:
                      message: required
                      retry: true
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR104" && diagnostic.Message.Contains("failure.retry", StringComparison.Ordinal));
    }

    [Fact]
    public void Equality_must_reference_known_state()
    {
        var source = TicketIdLike("[identifier, transform]")
            .Replace("state.Value", "state.Missing", StringComparison.Ordinal);
        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR215");
    }

    private static string TicketIdLike(string traits) => $$"""
        vsir: 0.1
        kind: domain-type
        name: TicketIdLike
        classification: value-object
        shape: product
        traits: {{traits}}

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
}
