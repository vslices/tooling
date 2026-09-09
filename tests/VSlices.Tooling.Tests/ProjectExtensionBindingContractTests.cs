namespace VSlices.Tooling.Tests;

public sealed class ProjectExtensionBindingContractTests
{
    [Fact]
    public async Task Project_extension_requires_explicit_bindings()
    {
        using var project = ReadyProject();
        WriteExtension(project, """
            mode: deterministic
            renderer: expression
            template: "{value}.Trim()"
            """);

        var result = await Run(project);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("EXT017", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_extension_rejects_duplicate_binding_declarations()
    {
        using var project = ReadyProject();
        WriteExtension(project, """
            mode: deterministic
            renderer: expression
            bindings: [value, value]
            template: "{value}.Trim()"
            """);

        var result = await Run(project);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("EXT021", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_extension_rejects_undeclared_template_placeholder()
    {
        using var project = ReadyProject();
        WriteExtension(project, """
            mode: deterministic
            renderer: expression
            bindings: [value]
            template: "{banana}.Trim()"
            """);

        var result = await Run(project);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("CSR018", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_extension_rejects_declared_binding_unused_by_template()
    {
        using var project = ReadyProject();
        WriteExtension(project, """
            mode: deterministic
            renderer: expression
            bindings: [value, extra]
            template: "{value}.Trim()"
            """);

        var result = await Run(project);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("CSR019", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
    }

    private static ToolingTestProject ReadyProject()
    {
        var project = new ToolingTestProject();
        project.WriteConfiguration();
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);
        return project;
    }

    private static void WriteExtension(ToolingTestProject project, string csharp)
    {
        Directory.CreateDirectory(project.ExtensionsRoot);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "manifest.yaml"), """
            version: 0.1
            catalogs:
              - normalize.yaml
            """);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "normalize.yaml"), $$"""
            extensions:
              - node: intrinsic.binding-contract-probe
                semantic:
                  kind: normalize
                targets:
                  csharp:
            {{Indent(csharp, 10)}}
            """);
        File.WriteAllText(Path.Combine(project.Root, "BindingContractProbe.vsir"), """
            vsir: 0.1
            kind: domain-type
            name: BindingContractProbe
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            construction:
              - normalize:
                  target: input.Value
                  intrinsic: binding-contract-probe
            """);
    }

    private static Task<CliResult> Run(ToolingTestProject project) =>
        project.Run(
            project.Root,
            "transpile", "BindingContractProbe.vsir",
            "--namespace", "Tests.Domain",
            "--stdout");

    private static string Indent(string value, int spaces)
    {
        var padding = new string(' ', spaces);
        return string.Join(
            Environment.NewLine,
            value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n')
                .Select(line => padding + line));
    }
}
