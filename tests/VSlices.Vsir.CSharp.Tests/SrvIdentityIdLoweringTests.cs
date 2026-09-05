using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class SrvIdentityIdLoweringTests
{
    private const string Source = """
        vsir: 0.1
        kind: domain-type
        name: SrvIdentityId
        classification: value-object
        shape: product
        traits: [identifier, refined, transform]
        refined-from: Rut

        state:
          Value: Rut

        representation:
          Value: string

        representation-mapping:
          Value:
            stringify: state.Value

        construction:
          input: Rut
          steps:
            - refine:
                value: input
                as: state.Value

        equality:
          domain: Rut
          by: state.Value
        """;

    private static string RulesetPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset");

    [Fact]
    public void Refined_domain_type_semantics_are_preserved_by_the_parser()
    {
        var parsed = VsirParser.Parse(Source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal("Rut", parsed.Document!.RefinedFrom);
        Assert.True(parsed.Document.Construction.Input.IsScalar);
        Assert.Equal("Rut", parsed.Document.Construction.Input.ScalarType);
        Assert.IsType<RefineStep>(Assert.Single(parsed.Document.Construction.Steps));
        Assert.IsType<StringifyProjection>(Assert.Single(parsed.Document.RepresentationMapping!.Fields).Value);
        Assert.Equal("Rut", parsed.Document.Equality!.Domain);
        Assert.Null(parsed.Document.Equality.Intrinsic);
    }

    [Fact]
    public void Canonical_refined_from_uses_kebab_case()
    {
        var parsed = VsirParser.Parse(Source.Replace("refined-from: Rut", "refinedFrom: Rut", StringComparison.Ordinal));

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, x => x.Code == "VSIR104" && x.Message.Contains("refinedFrom", StringComparison.Ordinal));
    }

    [Fact]
    public void Refined_domain_type_lowers_without_mapping_Rut_to_a_primitive()
    {
        var parsed = VsirParser.Parse(Source);
        var loaded = CSharpLoweringRuleSet.Load(RulesetPath);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var lowered = CSharpLowerer.Lower(
            parsed.Document!,
            new("Identities.Domain.Aggregates", loaded.RuleSet!));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("Identifier<SrvIdentityId, SrvIdentityId.Repr>", lowered.Source);
        Assert.Contains("Refined<SrvIdentityId, Rut, SrvIdentityId.Repr>", lowered.Source);
        Assert.Contains("Transform<SrvIdentityId, Rut>", lowered.Source);
        Assert.DoesNotContain("record struct Input", lowered.Source);
        Assert.Contains("Req<Rut, SrvIdentityId>.Full", lowered.Source);
        Assert.Contains("Transform((Rut input) => Instance(input))", lowered.Source);
        Assert.Contains("new(input)", lowered.Source);
        Assert.Contains("_value.Equals(other._value)", lowered.Source);
        Assert.Contains("_value.GetHashCode()", lowered.Source);
        Assert.Contains("public Rut ToBase()", lowered.Source);
        Assert.Contains("new(_value.ToString())", lowered.Source);
    }

    [Fact]
    public async Task Stringify_projection_requires_explicit_target_realization()
    {
        var parsed = VsirParser.Parse(Source);
        var root = Path.Combine(Path.GetTempPath(), "vslices-refined-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "csharp"));

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "manifest.yaml"),
                "targets:\n  csharp:\n    rules:\n      - csharp/rules.yaml\n");
            await File.WriteAllTextAsync(
                Path.Combine(root, "csharp", "rules.yaml"),
                """
                rules:
                  - node: equality.domain.equals
                    mode: deterministic
                    renderer: expression
                    template: "{left}.Equals({right})"
                  - node: equality.domain.hash
                    mode: deterministic
                    renderer: expression
                    template: "{value}.GetHashCode()"
                """);

            var loaded = CSharpLoweringRuleSet.Load(root);
            Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

            var lowered = CSharpLowerer.Lower(
                parsed.Document!,
                new("Identities.Domain.Aggregates", loaded.RuleSet!));

            Assert.False(lowered.IsSuccess);
            Assert.Contains(lowered.Diagnostics, x => x.Code == "CSL040");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
