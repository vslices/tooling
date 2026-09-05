namespace VSlices.Tooling.Tests;

public sealed class NormalizeSemanticExtensionTests
{
    [Fact]
    public async Task Undeclared_normalize_semantic_remains_VSIR221_even_with_project_ruleset()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        WriteRuleset(project.RulesetRoot, declareExtension: false);
        var vsir = WriteProbe(project.Root);

        var result = await project.Run(
            project.Root,
            "transpile", vsir,
            "--namespace", "Tests.Domain",
            "--stdout");

        Assert.Equal(1, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.Contains("VSIR221", output, StringComparison.Ordinal);
        Assert.DoesNotContain("CSL031", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Declared_normalize_semantic_reaches_missing_target_rule_as_CSL031()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        WriteRuleset(project.RulesetRoot, declareExtension: true);
        var vsir = WriteProbe(project.Root);

        var result = await project.Run(
            project.Root,
            "transpile", vsir,
            "--namespace", "Tests.Domain",
            "--stdout");

        Assert.Equal(1, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.DoesNotContain("VSIR221", output, StringComparison.Ordinal);
        Assert.Contains("CSL031", output, StringComparison.Ordinal);
        Assert.Contains("intrinsic.normalize-boundary-probe", output, StringComparison.Ordinal);
    }

    private static void WriteRuleset(string root, bool declareExtension)
    {
        Directory.CreateDirectory(Path.Combine(root, "csharp"));
        var extensions = declareExtension
            ? """
              semantic-extensions:
                normalize-boundary-probe:
                  kind: normalize

              """
            : string.Empty;

        File.WriteAllText(Path.Combine(root, "manifest.yaml"), extensions + """
            targets:
              csharp:
                rules:
                  - csharp/intrinsics.yaml
            """);

        File.WriteAllText(Path.Combine(root, "csharp", "intrinsics.yaml"), """
            rules: []
            """);
    }

    private static string WriteProbe(string root)
    {
        var path = Path.Combine(root, "NormalizeBoundaryProbe.vsir");
        File.WriteAllText(path, """
            vsir: 0.1
            kind: domain-type
            name: NormalizeBoundaryProbe
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
                    intrinsic: normalize-boundary-probe
            """);
        return path;
    }
}
