using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class MaintainedDomainTypeLoweringTests
{
    private const string IdentityTypeSource = """
        vsir: 0.1
        kind: domain-type
        name: IdentityType
        shape: product
        classification: maintained

        state:
          Name: string

        representation:
          Value:
            type: string
            from: state.Name

        values:
          Natural:
            state:
              Name: Natural
          Juridical:
            state:
              Name: Juridica

        equality:
          intrinsic: ordinal-equals
          by: state.Name
        """;

    [Fact]
    public void IdentityType_parses_as_closed_maintained_domain_without_transform()
    {
        var parsed = VsirParser.Parse(IdentityTypeSource);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var document = parsed.Document!;
        Assert.Equal("maintained", document.Classification);
        Assert.Empty(document.Construction.Input.Fields);
        Assert.Empty(document.Construction.Steps);
        Assert.NotNull(document.Values);
        Assert.Collection(
            document.Values!,
            natural =>
            {
                Assert.Equal("Natural", natural.Name);
                Assert.Equal("Natural", natural.State["Name"]);
            },
            juridical =>
            {
                Assert.Equal("Juridical", juridical.Name);
                Assert.Equal("Juridica", juridical.State["Name"]);
            });
        Assert.Equal("state.Name", document.Equality!.By);
    }

    [Fact]
    public void IdentityType_lowers_to_maintained_contract_and_declared_values()
    {
        var parsed = VsirParser.Parse(IdentityTypeSource);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var lowered = CSharpMaintainedDomainTypeLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Identities.Domain.Maintainers", LoadRules()));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("Maintained<IdentityType, IdentityType.Repr>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public readonly record struct Repr(string Value);", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public string Name { get; }", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public static IdentityType Natural { get; } =", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("new(\"Natural\")", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public static IdentityType Juridical { get; } =", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("new(\"Juridica\")", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public static Seq<IdentityType> All { get; }", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(Name, other.Name, StringComparison.Ordinal)", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("new(Name)", lowered.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("Transform<", lowered.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Maintained_value_must_establish_every_state_coordinate()
    {
        var parsed = VsirParser.Parse(IdentityTypeSource.Replace("Name: Juridica", "Other: Juridica", StringComparison.Ordinal));

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR284");
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "VSIR285");
    }

    private static CSharpLoweringRuleSet LoadRules()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-maintained-" + Guid.NewGuid().ToString("N"));
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
                      - csharp/rules.yaml
                """);

            File.WriteAllText(
                Path.Combine(csharp, "rules.yaml"),
                """
                rules:
                  - node: equality.ordinal-equals.equals
                    mode: deterministic
                    renderer: expression
                    bindings: [left, right]
                    template: "string.Equals({left}, {right}, StringComparison.Ordinal)"

                  - node: equality.ordinal-equals.hash
                    mode: deterministic
                    renderer: expression
                    bindings: [value]
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
