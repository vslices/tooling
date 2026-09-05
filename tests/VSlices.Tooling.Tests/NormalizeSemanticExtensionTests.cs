namespace VSlices.Tooling.Tests;

public sealed class NormalizeSemanticExtensionTests
{
    [Fact]
    public async Task Undeclared_normalize_semantic_remains_VSIR221()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        WriteRuleset(project.RulesetRoot, declareExtension: false, extensionRenderer: false, targetRenderer: false);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(1, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.Contains("VSIR221", output, StringComparison.Ordinal);
        Assert.DoesNotContain("CSL031", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Declared_normalize_semantic_without_CSharp_realization_reaches_CSL031()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        WriteRuleset(project.RulesetRoot, declareExtension: true, extensionRenderer: false, targetRenderer: false);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(1, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.DoesNotContain("VSIR221", output, StringComparison.Ordinal);
        Assert.Contains("CSL031", output, StringComparison.Ordinal);
        Assert.Contains("intrinsic.normalize-boundary-probe", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Declared_normalize_semantic_and_co_located_CSharp_realization_lower_successfully()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        WriteRuleset(project.RulesetRoot, declareExtension: true, extensionRenderer: true, targetRenderer: false);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("VSIR221", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("new(input.Value.Trim())", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Renderer_without_semantic_declaration_still_fails_as_VSIR221()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        WriteRuleset(project.RulesetRoot, declareExtension: false, extensionRenderer: false, targetRenderer: true);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(1, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.Contains("VSIR221", output, StringComparison.Ordinal);
        Assert.DoesNotContain("CSL031", output, StringComparison.Ordinal);
    }

    private static Task<CliResult> Run(ToolingTestProject project, string vsir) =>
        project.Run(
            project.Root,
            "transpile", vsir,
            "--namespace", "Tests.Domain",
            "--stdout");

    private static void WriteRuleset(
        string root,
        bool declareExtension,
        bool extensionRenderer,
        bool targetRenderer)
    {
        Directory.CreateDirectory(Path.Combine(root, "csharp"));
        Directory.CreateDirectory(Path.Combine(root, "extensions"));

        var extensionReference = declareExtension
            ? """
              extensions:
                - extensions/normalize.yaml

              """
            : string.Empty;

        File.WriteAllText(Path.Combine(root, "manifest.yaml"), extensionReference + """
            targets:
              csharp:
                rules:
                  - csharp/intrinsics.yaml
            """);

        var targetRule = targetRenderer
            ? """
              rules:
                - node: intrinsic.normalize-boundary-probe
                  mode: deterministic
                  renderer: expression
                  template: "{value}.Trim()"
              """
            : "rules: []";
        File.WriteAllText(Path.Combine(root, "csharp", "intrinsics.yaml"), targetRule);

        if (!declareExtension)
            return;

        var extensionCatalog = extensionRenderer
            ? """
              extensions:
                - node: intrinsic.normalize-boundary-probe
                  semantic:
                    kind: normalize
                  targets:
                    csharp:
                      mode: deterministic
                      renderer: expression
                      template: "{value}.Trim()"
              """
            : """
              extensions:
                - node: intrinsic.normalize-boundary-probe
                  semantic:
                    kind: normalize
              """;

        File.WriteAllText(Path.Combine(root, "extensions", "normalize.yaml"), extensionCatalog);
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
