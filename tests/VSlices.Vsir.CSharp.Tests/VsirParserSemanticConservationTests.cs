using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirParserSemanticConservationTests
{
    [Fact]
    public void TicketId_identifier_classification_does_not_require_an_identifier_trait()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[transform]"));

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal("identifier", parsed.Document!.Classification);
        Assert.Equal(["transform"], parsed.Document.Traits);
    }

    [Fact]
    public void TicketCode_identifier_trait_is_independent_from_value_object_classification()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: TicketCode
            classification: value-object
            shape: product
            traits: [transform, identifier]
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

        var parsed = VsirParser.Parse(source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal("value-object", parsed.Document!.Classification);
        Assert.Contains("identifier", parsed.Document.Traits);
        Assert.NotNull(parsed.Document.Equality);
    }

    [Fact]
    public void Equality_without_identifier_semantics_is_rejected()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Value
            classification: value-object
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

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR252");
    }

    [Fact]
    public void TicketId_semantics_are_preserved_and_lowered_through_identifier_structure_and_ruleset_equality()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[transform]"));
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal(new EqualitySemantics("ordinal-equals", null, "state.Value"), parsed.Document!.Equality);
        var rules = CSharpLoweringRuleSet.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset"));
        Assert.True(rules.IsSuccess, string.Join(Environment.NewLine, rules.Diagnostics));
        var lowered = CSharpLanguageLowerer.Lower(parsed.Document, new CSharpLoweringContext("Tickets.Domain.Aggregates", rules.RuleSet!));
        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("Identifier<TicketIdLike, TicketIdLike.Repr>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(_value, other._value, StringComparison.Ordinal)", lowered.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Identifier_lowering_stops_when_equality_ruleset_knowledge_is_missing()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[transform]"));
        Assert.True(parsed.IsSuccess);
        var temporary = Path.Combine(Path.GetTempPath(), "vslices-ruleset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temporary, "csharp"));
        try
        {
            File.WriteAllText(Path.Combine(temporary, "manifest.yaml"), """
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
                    bindings: [value]
                    template: "!string.IsNullOrEmpty({value})"
                """);
            var rules = CSharpLoweringRuleSet.Load(temporary);
            Assert.True(rules.IsSuccess);
            var lowered = CSharpLanguageLowerer.Lower(parsed.Document!, new CSharpLoweringContext("Tickets.Domain.Aggregates", rules.RuleSet!));
            Assert.False(lowered.IsSuccess);
            Assert.Contains(lowered.Diagnostics, d => d.Code == "CSL021");
            Assert.Contains(lowered.Diagnostics, d => d.Code == "CSL022");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void Identifier_requires_explicit_equality_semantics()
    {
        var source = TicketIdLike("[transform]");
        var equalityStart = source.IndexOf("\nequality:", StringComparison.Ordinal);
        Assert.True(equalityStart >= 0);
        source = source[..equalityStart];
        var parsed = VsirParser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR216");
    }

    [Fact]
    public void Duplicate_traits_are_rejected()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[transform, transform]"));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR217");
    }

    [Fact]
    public void Unknown_traits_are_rejected()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[transform, imaginary]"));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR218");
    }

    [Fact]
    public void Non_scalar_trait_entry_is_rejected_instead_of_disappearing()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Something
            classification: value-object
            shape: product
            traits:
              - transform
              - imaginary:
                  something: true
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

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR107");
    }

    [Fact]
    public void Non_mapping_construction_step_is_rejected_instead_of_disappearing()
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
            input:
              Value: string
            construction:
              - ensure:
                  condition:
                    intrinsic: non-empty
                    args:
                      value: input.Value
                  failure:
                    message: required
              - hey-I-am-semantics
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR100");
    }

    [Fact]
    public void Unsupported_root_semantics_are_rejected()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[transform]") + "\nlifecycle:\n  imaginary: true\n");
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR104" && d.Message.Contains("lifecycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_equality_semantics_are_rejected()
    {
        var source = TicketIdLike("[transform]").Replace("  by: state.Value", "  by: state.Value\n  imaginary-new-semantic: true", StringComparison.Ordinal);
        var parsed = VsirParser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR104" && d.Message.Contains("equality.imaginary-new-semantic", StringComparison.Ordinal));
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
            input:
              Value: string
            construction:
              - ensure:
                  condition:
                    intrinsic: non-empty
                    args:
                      value: input.Value
                    retry: true
                  failure:
                    message: required
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d =>
            d.Code == "VSIR104" &&
            d.Message.Contains("construction[].ensure.condition.retry", StringComparison.Ordinal));
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
            input:
              Value: string
            construction:
              - ensure:
                  condition:
                    intrinsic: non-empty
                    args:
                      value: input.Value
                  failure:
                    message: required
                    retry: true
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d =>
            d.Code == "VSIR104" &&
            d.Message.Contains("construction[].ensure.failure.retry", StringComparison.Ordinal));
    }

    [Fact]
    public void Equality_must_reference_known_state()
    {
        var parsed = VsirParser.Parse(TicketIdLike("[transform]").Replace("state.Value", "state.Missing", StringComparison.Ordinal));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR215");
    }

    [Fact]
    public void Obsolete_construction_shape_is_rejected_without_a_compatibility_parser()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: OldShape
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
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR111");
        Assert.Contains(parsed.Diagnostics, d => d.Code == "VSIR002");
    }

    private static string TicketIdLike(string traits) => $$"""
        vsir: 0.1
        kind: domain-type
        name: TicketIdLike
        classification: identifier
        shape: product
        traits: {{traits}}
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
        equality:
          intrinsic: ordinal-equals
          by: state.Value
        """;
}
