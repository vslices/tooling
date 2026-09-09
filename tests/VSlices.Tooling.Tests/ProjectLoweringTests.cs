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
        Assert.Contains("Project lowering: 2 succeeded, 0 unsupported, 0 failures", result.StandardOutput);
        Assert.True(File.Exists(Path.Combine(project.Root, "ValueObjects", "StreetName.vsir.cs")));
        Assert.True(File.Exists(Path.Combine(project.Root, "EmailAddress.vsir.cs")));
    }

    [Fact]
    public async Task Lower_accepts_searchable_tags_metadata_without_giving_it_semantic_authority()
    {
        using var project = CreateProject("Identities.Domain");
        var path = Path.Combine(project.Root, "TaggedValue.vsir");
        WriteSupportedValueObject(path, "TaggedValue");

        var source = File.ReadAllText(path);
        source = source.Replace(
            "name: TaggedValue\n",
            "name: TaggedValue\ntags: [ticket, serviu]\n",
            StringComparison.Ordinal);
        File.WriteAllText(path, source);

        var result = await project.Run(project.Root, "lower", "TaggedValue.vsir");

        Assert.Equal(0, result.ExitCode);
        var materialization = Path.Combine(project.Root, "TaggedValue.vsir.cs");
        Assert.True(File.Exists(materialization));
        var lowered = File.ReadAllText(materialization);
        Assert.Contains("TaggedValue", lowered);
        Assert.DoesNotContain("ticket", lowered, StringComparison.Ordinal);
        Assert.DoesNotContain("serviu", lowered, StringComparison.Ordinal);
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
        Assert.Contains("1 succeeded, 1 unsupported, 0 failures", result.StandardOutput);
        Assert.Contains("VSIR", result.StandardError);
    }

    [Fact]
    public async Task Project_lowering_returns_failure_when_target_toolchain_rejects_project_even_if_processing_can_continue()
    {
        using var project = CreateProject("Identities.Domain");
        project.WriteStreetName();

        // Exercise the real child-process boundary instead of mocking the
        // coordinator. A malformed project makes the real `dotnet msbuild`
        // invocation fail while the VSIR artifact itself remains supported.
        File.WriteAllText(Path.Combine(project.Root, "Identities.Domain.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>Identities.Domain</RootNamespace>
              <!-- deliberately malformed: PropertyGroup is never closed -->
            </Project>
            """);

        var result = await project.Run(project.Root, "lower", "Identities.Domain");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOTNET002", result.StandardError);
        Assert.Contains("Project lowering: 0 succeeded, 0 unsupported, 1 failures", result.StandardOutput);
        Assert.False(File.Exists(Path.Combine(project.Root, "StreetName.vsir.cs")));
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
            input:
              Value: string
            construction:
              - ensure:
                  condition:
                    intrinsic: non-empty
                    args:
                      value: input.Value
                  failure:
                    message: required
            """);
    }
}
