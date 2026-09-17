using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class SumDomainTypeLoweringTests
{
    private const string NameSource = """
        vsir: 0.1
        kind: domain-type
        name: Name
        shape: sum
        classification: value-object

        state: {}
        representation: {}

        variants:
          FullName:
            traits: [transform]
            state:
              Names: string
              FirstSurname: string
              SecondSurname:
                optional: string
            representation:
              Names: string
              FirstSurname: string
              SecondSurname:
                optional: string
            input:
              Names: string
              FirstSurname: string
              SecondSurname:
                optional: string
            construction:
              - ensure:
                  condition:
                    intrinsic: non-empty
                    args:
                      value: input.Names
                  failure:
                    message: Debes especificar al menos un nombre
              - ensure:
                  condition:
                    intrinsic: non-empty
                    args:
                      value: input.FirstSurname
                  failure:
                    message: Debes especificar el primer apellido
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

          CompanyName:
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
                    message: Debes especificar el nombre de la empresa
              - ensure:
                  condition:
                    intrinsic: length-at-most
                    args:
                      value: input.Value
                      max: 92
                  failure:
                    message: El nombre debe tener 92 caracteres o menos
              - refine:
                  state:
                    Value: input.Value
        """;

    [Fact]
    public void Name_sum_is_parsed_without_product_cascade_diagnostics()
    {
        var parsed = VsirParser.Parse(NameSource);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal("sum", parsed.Document!.Shape);
        Assert.Equal(2, parsed.Document.Variants!.Count);
        Assert.DoesNotContain(parsed.Diagnostics, diagnostic =>
            diagnostic.Code is "VSIR111" or "VSIR203" or "VSIR204" or "VSIR205" or "VSIR206" or "VSIR207");

        var fullName = Assert.Single(parsed.Document.Variants, variant => variant.Name == "FullName");
        var ensure = Assert.IsType<EnsureStep>(fullName.Construction.Steps[2]);
        var combinedLength = Assert.IsType<LengthAtMostCondition>(ensure.Condition);
        var concat = Assert.IsType<SemanticIntrinsicExpression>(combinedLength.Value);
        Assert.Equal("concat-space", concat.Intrinsic);
        Assert.Equal(3, concat.Values.Count);
    }

    [Fact]
    public void Name_sum_lowers_to_closed_root_and_transform_variants()
    {
        var parsed = VsirParser.Parse(NameSource);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var rules = LoadRules();
        var lowered = CSharpSumDomainTypeLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Identities.Domain.ValueObjects", rules));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("public abstract class Name", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("DomainType<Name, Name.Repr>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public sealed class FullName", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public new sealed record Repr(", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Transform<FullName, FullName.Input>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Option<string> SecondSurname", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("!string.IsNullOrEmpty(input.Names)", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("string.Join(\" \", new[] { input.Names, input.FirstSurname, input.SecondSurname }).Length <= 92", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("new(input.Names, input.FirstSurname, input.SecondSurname)", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public sealed class CompanyName", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("input.Value.Length <= 92", lowered.Source, StringComparison.Ordinal);
    }

    private static CSharpLoweringRuleSet LoadRules()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-sum-domain-" + Guid.NewGuid().ToString("N"));
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
                  - node: type.optional
                    mode: deterministic
                    renderer: type
                    bindings: [value]
                    template: "Option<{value}>"

                  - node: intrinsic.non-empty
                    mode: deterministic
                    renderer: expression
                    bindings: [value]
                    template: "!string.IsNullOrEmpty({value})"

                  - node: intrinsic.length-at-most
                    mode: deterministic
                    renderer: expression
                    bindings: [value, max]
                    template: "{value}.Length <= {max}"

                  - node: intrinsic.concat-space
                    mode: deterministic
                    renderer: expression
                    bindings: [values]
                    template: "string.Join(\" \", new[] { {values} })"
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
