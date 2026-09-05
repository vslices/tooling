namespace VSlices.Tooling.Tests;

public sealed class NormalizeSemanticExtensionTests
{
    [Fact]
    public async Task Undeclared_normalize_semantic_remains_VSIR221()
    {
        using var project = ReadyProject();
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
        using var project = ReadyProject();
        WriteProjectExtension(project, includeRenderer: false);
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
        using var project = ReadyProject();
        WriteProjectExtension(project, includeRenderer: true);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("VSIR221", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("new(input.Value.Trim())", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Renderer_in_ruleset_without_project_semantic_declaration_still_fails_as_VSIR221()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        WriteRulesetWithProbeRenderer(project.RulesetRoot);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(1, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.Contains("VSIR221", output, StringComparison.Ordinal);
        Assert.DoesNotContain("CSL031", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_project_extension_catalog_fails_closed()
    {
        using var project = ReadyProject();
        Directory.CreateDirectory(project.ExtensionsRoot);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "manifest.yaml"), """
            version: 0.1
            catalogs:
              - missing.yaml
            """);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("EXT006", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_scalar_catalog_reference_fails_closed()
    {
        using var project = ReadyProject();
        Directory.CreateDirectory(project.ExtensionsRoot);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "manifest.yaml"), """
            version: 0.1
            catalogs:
              - path: normalize.yaml
            """);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("EXT003", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_mapping_extension_entry_fails_closed()
    {
        using var project = ReadyProject();
        Directory.CreateDirectory(project.ExtensionsRoot);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "manifest.yaml"), """
            version: 0.1
            catalogs:
              - normalize.yaml
            """);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "normalize.yaml"), """
            extensions:
              - normalize-boundary-probe
            """);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("EXT007", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CSharp_realization_without_semantic_metadata_fails_closed()
    {
        using var project = ReadyProject();
        Directory.CreateDirectory(project.ExtensionsRoot);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "manifest.yaml"), """
            version: 0.1
            catalogs:
              - normalize.yaml
            """);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "normalize.yaml"), """
            extensions:
              - node: intrinsic.normalize-boundary-probe
                targets:
                  csharp:
                    mode: deterministic
                    renderer: expression
                    template: "{value}.Trim()"
            """);
        var vsir = WriteProbe(project.Root);

        var result = await Run(project, vsir);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("EXT016", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
    }

    private static ToolingTestProject ReadyProject()
    {
        var project = new ToolingTestProject();
        project.WriteConfiguration();
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);
        return project;
    }

    private static Task<CliResult> Run(ToolingTestProject project, string vsir) =>
        project.Run(
            project.Root,
            "transpile", vsir,
            "--namespace", "Tests.Domain",
            "--stdout");

    private static void WriteProjectExtension(ToolingTestProject project, bool includeRenderer)
    {
        Directory.CreateDirectory(project.ExtensionsRoot);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "manifest.yaml"), """
            version: 0.1
            catalogs:
              - normalize.yaml
            """);

        var catalog = includeRenderer
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

        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "normalize.yaml"), catalog);
    }

    private static void WriteRulesetWithProbeRenderer(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "csharp"));
        File.WriteAllText(Path.Combine(root, "manifest.yaml"), """
            targets:
              csharp:
                rules:
                  - csharp/intrinsics.yaml
            """);
        File.WriteAllText(Path.Combine(root, "csharp", "intrinsics.yaml"), """
            rules:
              - node: intrinsic.normalize-boundary-probe
                mode: deterministic
                renderer: expression
                template: "{value}.Trim()"
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
