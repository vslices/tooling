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
}
