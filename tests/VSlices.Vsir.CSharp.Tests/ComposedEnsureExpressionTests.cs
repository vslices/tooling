using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class ComposedEnsureExpressionTests
{
    private const string Source = """
        vsir: 0.1
        kind: domain-type
        name: FullNameLike
        shape: product
        classification: value-object
        traits: [transform]

        state:
          Names: string
          FirstSurname: string
          SecondSurname: string

        representation:
          Names: string
          FirstSurname: string
          SecondSurname: string

        input:
          Names: string
          FirstSurname: string
          SecondSurname: string

        construction:
        - ensure:
            condition:
              intrinsic: length-at-most
              args:
                value:
                  intrinsic: concat-space
                  values:
                  - input.Names
                  - input.FirstSurname
                  - input.SecondSurname
                max: 92
            failure:
              message: El nombre debe tener 92 caracteres o menos
        - refine:
            state:
              Names: input.Names
              FirstSurname: input.FirstSurname
              SecondSurname: input.SecondSurname
        """;

    private static string RulesetPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset");

    [Fact]
    public void Parser_preserves_nested_intrinsic_condition_expression()
    {
        var parsed = VsirParser.Parse(Source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var ensure = Assert.IsType<EnsureStep>(parsed.Document!.Construction.Steps[0]);
        var length = Assert.IsType<LengthAtMostCondition>(ensure.Condition);
        Assert.Equal(92, length.Max);

        var concat = Assert.IsType<SemanticIntrinsicExpression>(length.Value);
        Assert.Equal("concat-space", concat.Intrinsic);
        Assert.Collection(
            concat.Values,
            value => Assert.Equal("input.Names", Assert.IsType<SemanticReferenceExpression>(value).Value),
            value => Assert.Equal("input.FirstSurname", Assert.IsType<SemanticReferenceExpression>(value).Value),
            value => Assert.Equal("input.SecondSurname", Assert.IsType<SemanticReferenceExpression>(value).Value));
    }

    [Fact]
    public void Lowering_composes_concat_space_before_length_at_most()
    {
        var parsed = VsirParser.Parse(Source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var loaded = CSharpLoweringRuleSet.Load(RulesetPath);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var lowered = CSharpLanguageLowerer.Lower(
            parsed.Document!,
            new("Identities.Domain.ValueObjects", loaded.RuleSet!));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains(
            "string.Join(\" \", new[] { input.Names, input.FirstSurname, input.SecondSurname }).Length <= 92",
            lowered.Source);
    }

    [Fact]
    public void Missing_nested_intrinsic_rule_fails_closed()
    {
        var parsed = VsirParser.Parse(Source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var root = Path.Combine(Path.GetTempPath(), "vslices-condition-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "csharp"));
        try
        {
            File.WriteAllText(
                Path.Combine(root, "manifest.yaml"),
                "targets:\n  csharp:\n    rules:\n      - csharp/rules.yaml\n");
            File.WriteAllText(
                Path.Combine(root, "csharp", "rules.yaml"),
                """
                rules:
                  - node: intrinsic.length-at-most
                    mode: deterministic
                    renderer: expression
                    bindings: [value, max]
                    template: "{value}.Length <= {max}"
                """);

            var loaded = CSharpLoweringRuleSet.Load(root);
            Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));
            var lowered = CSharpLanguageLowerer.Lower(
                parsed.Document!,
                new("Identities.Domain.ValueObjects", loaded.RuleSet!));

            Assert.False(lowered.IsSuccess);
            Assert.Contains(lowered.Diagnostics, diagnostic =>
                diagnostic.Code == "CSL011" &&
                diagnostic.Message.Contains("intrinsic.concat-space", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
