using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class CSharpLoweringRuleSetTests
{
    [Fact]
    public void Case_distinct_sibling_is_not_inside_ruleset_root_on_case_sensitive_platforms()
    {
        if (OperatingSystem.IsWindows())
            return;

        var parent = Path.Combine(Path.GetTempPath(), "vslices-ruleset-boundary-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "ruleset");
        var sibling = Path.Combine(parent, "RuleSet");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(sibling);

        try
        {
            File.WriteAllText(Path.Combine(root, "manifest.yaml"), """
                targets:
                  csharp:
                    rules:
                      - ../RuleSet/evil.yaml
                """);
            File.WriteAllText(Path.Combine(sibling, "evil.yaml"), """
                rules:
                  - node: intrinsic.non-empty
                    mode: deterministic
                    renderer: expression
                    bindings: [value]
                    template: "!string.IsNullOrEmpty({value})"
                """);

            var result = CSharpLoweringRuleSet.Load(root);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CSR003");
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Undeclared_template_binding_fails_when_ruleset_is_loaded()
    {
        using var ruleset = TemporaryRuleset.Create("""
            rules:
              - node: projection.map
                mode: deterministic
                renderer: expression
                bindings: [source, bind, value]
                template: "{source}.Map({banana} => {value})"
            """);

        var result = CSharpLoweringRuleSet.Load(ruleset.Root);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CSR018");
    }

    [Fact]
    public void Declared_binding_not_used_by_template_fails_when_ruleset_is_loaded()
    {
        using var ruleset = TemporaryRuleset.Create("""
            rules:
              - node: projection.map
                mode: deterministic
                renderer: expression
                bindings: [source, bind, value]
                template: "{source}.Map(item => {value})"
            """);

        var result = CSharpLoweringRuleSet.Load(ruleset.Root);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CSR019");
    }

    [Fact]
    public void Duplicate_binding_declaration_fails_when_ruleset_is_loaded()
    {
        using var ruleset = TemporaryRuleset.Create("""
            rules:
              - node: projection.stringify
                mode: deterministic
                renderer: expression
                bindings: [value, value]
                template: "{value}.ToString()"
            """);

        var result = CSharpLoweringRuleSet.Load(ruleset.Root);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CSR017");
    }

    [Fact]
    public void Hyphenated_binding_names_are_part_of_the_tooling_placeholder_grammar()
    {
        using var ruleset = TemporaryRuleset.Create("""
            rules:
              - node: projection.hyphen-probe
                mode: deterministic
                renderer: expression
                bindings: [source-value]
                template: "{source-value}.ToString()"
            """);

        var loaded = CSharpLoweringRuleSet.Load(ruleset.Root);

        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));
        Assert.True(loaded.RuleSet!.TryRenderDeterministicExpression(
            "projection.hyphen-probe",
            new Dictionary<string, string> { ["source-value"] = "input" },
            out var expression));
        Assert.Equal("input.ToString()", expression);
    }

    [Fact]
    public void Leading_underscore_binding_names_are_outside_the_tooling_placeholder_grammar()
    {
        using var ruleset = TemporaryRuleset.Create("""
            rules:
              - node: projection.leading-underscore-probe
                mode: deterministic
                renderer: expression
                bindings: [_value]
                template: "{_value}"
            """);

        var loaded = CSharpLoweringRuleSet.Load(ruleset.Root);

        Assert.False(loaded.IsSuccess);
        Assert.Contains(loaded.Diagnostics, diagnostic => diagnostic.Code == "CSR019");
    }

    [Fact]
    public void Rendering_requires_exact_declared_binding_set()
    {
        using var ruleset = TemporaryRuleset.Create("""
            rules:
              - node: projection.select
                mode: deterministic
                renderer: expression
                bindings: [source, field]
                template: "{source}.{field}"
            """);

        var loaded = CSharpLoweringRuleSet.Load(ruleset.Root);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        Assert.False(loaded.RuleSet!.TryRenderDeterministicExpression(
            "projection.select",
            new Dictionary<string, string> { ["source"] = "value" },
            out _));

        Assert.False(loaded.RuleSet.TryRenderDeterministicExpression(
            "projection.select",
            new Dictionary<string, string>
            {
                ["source"] = "value",
                ["field"] = "Name",
                ["extra"] = "unexpected"
            },
            out _));

        Assert.True(loaded.RuleSet.TryRenderDeterministicExpression(
            "projection.select",
            new Dictionary<string, string>
            {
                ["source"] = "value",
                ["field"] = "Name"
            },
            out var expression));
        Assert.Equal("value.Name", expression);
    }

    private sealed class TemporaryRuleset : IDisposable
    {
        private TemporaryRuleset(string root) => Root = root;

        public string Root { get; }

        public static TemporaryRuleset Create(string rules)
        {
            var root = Path.Combine(Path.GetTempPath(), "vslices-ruleset-contract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "csharp"));
            File.WriteAllText(Path.Combine(root, "manifest.yaml"), """
                targets:
                  csharp:
                    rules:
                      - csharp/rules.yaml
                """);
            File.WriteAllText(Path.Combine(root, "csharp", "rules.yaml"), rules);
            return new(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
