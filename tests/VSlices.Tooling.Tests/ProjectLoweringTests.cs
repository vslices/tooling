namespace VSlices.Tooling.Tests;

public sealed class ProjectLoweringTests
{
    [Fact]
    public async Task Lower_project_symbol_lowers_all_supported_vsir_artifacts()
    {
        using var project = CreateProject("Identities.Domain");
        project.WriteStreetName(directory: Path.Combine(project.Root, "ValueObjects"));
        WriteSupportedValueObject(Path.Combine(project.Root, "EmailAddress.vsir"), "EmailAddress");

        var result = await project.Run(project.Root, "lower", "Identities.Domain");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Lowering project 'Identities.Domain' (2 VSIR artifacts)", result.StandardOutput);
        Assert.Contains("Project lowering: 2 succeeded, 0 not lowered", result.StandardOutput);
        Assert.True(File.Exists(Path.Combine(project.Root, "ValueObjects", "StreetName.vsir.cs")));
        Assert.True(File.Exists(Path.Combine(project.Root, "EmailAddress.vsir.cs")));
    }

    [Fact]
    public async Task Lower_requires_extension_when_project_and_vsir_symbols_are_ambiguous()
    {
        using var project = CreateProject("Identities.Domain");
        WriteSupportedValueObject(Path.Combine(project.Root, "Identities.Domain.vsir"), "IdentitiesDomain");

        var ambiguous = await project.Run(project.Root, "lower", "Identities.Domain");

        Assert.NotEqual(0, ambiguous.ExitCode);
        Assert.Contains("CLI004", ambiguous.StandardError);
        Assert.Contains("Identities.Domain.vsir", ambiguous.StandardError);
        Assert.Contains("Identities.Domain.csproj", ambiguous.StandardError);

        var artifact = await project.Run(project.Root, "lower", "Identities.Domain.vsir");
        Assert.Equal(0, artifact.ExitCode);

        File.Delete(Path.Combine(project.Root, "Identities.Domain.vsir.cs"));
        var wholeProject = await project.Run(project.Root, "lower", "Identities.Domain.csproj");
        Assert.Equal(0, wholeProject.ExitCode);
    }

    [Fact]
    public async Task Project_lowering_reports_unsupported_artifacts_without_abandoning_supported_ones()
    {
        using var project = CreateProject("Identities.Domain");
        project.WriteStreetName();
        File.WriteAllText(Path.Combine(project.Root, "Future.vsir"), """
            vsir: 0.1
            kind: future-semantic-kind
            name: Future
            """);

        var result = await project.Run(project.Root, "lower", "Identities.Domain");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.Root, "StreetName.vsir.cs")));
        Assert.False(File.Exists(Path.Combine(project.Root, "Future.vsir.cs")));
        Assert.Contains("1 succeeded, 1 not lowered", result.StandardOutput);
        Assert.Contains("VSIR", result.StandardError);
    }

    private static ToolingTestProject CreateProject(string name)
    {
        var project = new ToolingTestProject();
        project.WriteConfiguration();
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);
        File.WriteAllText(Path.Combine(project.Root, name + ".csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>Identities.Domain</RootNamespace>
              </PropertyGroup>
            </Project>
            """);
        return project;
    }

    private static void WriteSupportedValueObject(string path, string name)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
            vsir: 0.1
            kind: domain-type
            name: {{name}}
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
                - ensure:
                    condition:
                      intrinsic: non-empty
                      value: input.Value
                    failure:
                      message: required
            """);
    }
}
