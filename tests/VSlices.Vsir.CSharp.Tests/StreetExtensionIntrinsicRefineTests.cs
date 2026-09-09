using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class StreetExtensionIntrinsicRefineTests
{
    private const string Source = """
        vsir: 0.1
        kind: domain-type
        name: StreetExtension
        shape: product
        classification: value-object
        traits: [transform]

        state:
          Name: string
          Value: string

        representation:
          Value:
            type: string
            mapping:
              intrinsic: concat-space
              values:
              - state.Name
              - state.Value

        input:
          Value: string

        construction:
        - ensure:
            condition:
              intrinsic: not-whitespace
              args:
                value: input.Value
            failure:
              message: Debes especificar la extensión
        - ensure:
            condition:
              intrinsic: length-between
              args:
                value: input.Value
                min: 3
                max: 16
            failure:
              message: Debe tener entre 3 y 16 caracteres
        - refine:
            intrinsic: split-first-rest
            value: input.Value
            as:
              Name: name
              Value: value
            failure:
              message: Debes especificar un nombre y un valor, separados por espacio
        - refine:
            state:
              Name: name
              Value: value
        """;

    private static string RulesetPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset");

    [Fact]
    public void Intrinsic_refine_produces_ordered_bindings_consumed_by_state_refine()
    {
        var parsed = VsirParser.Parse(Source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var intrinsic = Assert.IsType<IntrinsicRefineStep>(parsed.Document!.Construction.Steps[2]);
        Assert.Equal("split-first-rest", intrinsic.Intrinsic);
        Assert.Equal("input.Value", intrinsic.Value);
        Assert.Equal("name", intrinsic.As["Name"]);
        Assert.Equal("value", intrinsic.As["Value"]);
        Assert.Equal("Debes especificar un nombre y un valor, separados por espacio", intrinsic.FailureMessage);

        var stateSteps = parsed.Document.Construction.Steps.OfType<RefineStep>().ToArray();
        Assert.Equal(2, stateSteps.Length);
        Assert.Contains(stateSteps, step => step.Value == "name" && step.As == "state.Name");
        Assert.Contains(stateSteps, step => step.Value == "value" && step.As == "state.Value");
    }

    [Fact]
    public void StreetExtension_lowers_intrinsic_refinement_through_Ruleset_relations()
    {
        var parsed = VsirParser.Parse(Source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var loaded = CSharpLoweringRuleSet.Load(RulesetPath);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var lowered = CSharpLanguageLowerer.Lower(
            parsed.Document!,
            new("Identities.Domain.ValueObjects", loaded.RuleSet!));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("DomainType<StreetExtension, StreetExtension.Repr>", lowered.Source);
        Assert.Contains("Transform<StreetExtension, StreetExtension.Input>", lowered.Source);
        Assert.Contains("!string.IsNullOrWhiteSpace(input.Value)", lowered.Source);
        Assert.Contains("input.Value.Length >= 3 && input.Value.Length <= 16", lowered.Source);
        Assert.Contains("input.Value.Split(\" \", StringSplitOptions.RemoveEmptyEntries).Length > 1", lowered.Source);
        Assert.Contains("input.Value.Split(\" \", StringSplitOptions.RemoveEmptyEntries)[0]", lowered.Source);
        Assert.Contains("string.Join(\" \", input.Value.Split(\" \", StringSplitOptions.RemoveEmptyEntries)[1..])", lowered.Source);
        Assert.Contains("new(input.Value.Split", lowered.Source);
        Assert.Contains("string.Join(\" \", new[] { _name, _value })", lowered.Source);
    }

    [Fact]
    public async Task Missing_intrinsic_refine_output_rule_fails_closed()
    {
        var parsed = VsirParser.Parse(Source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var root = Path.Combine(Path.GetTempPath(), "vslices-refine-rules-" + Guid.NewGuid().ToString("N"));
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
                  - node: intrinsic.not-whitespace
                    mode: deterministic
                    renderer: expression
                    bindings: [value]
                    template: "!string.IsNullOrWhiteSpace({value})"
                  - node: intrinsic.length-between
                    mode: deterministic
                    renderer: expression
                    bindings: [value, min, max]
                    template: "{value}.Length >= {min} && {value}.Length <= {max}"
                  - node: intrinsic.concat-space
                    mode: deterministic
                    renderer: expression
                    bindings: [values]
                    template: "string.Join(\" \", new[] { {values} })"
                  - node: refine.split-first-rest.condition
                    mode: deterministic
                    renderer: expression
                    bindings: [value]
                    template: "{value}.Length > 1"
                  - node: refine.split-first-rest.output.Name
                    mode: deterministic
                    renderer: expression
                    bindings: [value]
                    template: "{value}"
                """);

            var loaded = CSharpLoweringRuleSet.Load(root);
            Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));
            var lowered = CSharpLanguageLowerer.Lower(
                parsed.Document!,
                new("Identities.Domain.ValueObjects", loaded.RuleSet!));

            Assert.False(lowered.IsSuccess);
            Assert.Contains(lowered.Diagnostics, diagnostic =>
                diagnostic.Code == "CSL081" &&
                diagnostic.Message.Contains("refine.split-first-rest.output.Value", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
