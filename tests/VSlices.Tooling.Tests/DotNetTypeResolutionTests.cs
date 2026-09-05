using System.Diagnostics;

namespace VSlices.Tooling.Tests;

public sealed class DotNetTypeResolutionTests
{
    [Fact]
    public async Task Lowering_resolves_unique_nominal_type_as_using_and_short_name()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);

        var sharedDirectory = Path.Combine(project.Root, "Shared");
        Directory.CreateDirectory(sharedDirectory);
        var sharedProject = Path.Combine(sharedDirectory, "Shared.Domain.csproj");
        File.WriteAllText(sharedProject, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>Shared.Domain</RootNamespace>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(sharedDirectory, "Rut.cs"), """
            namespace Shared.Domain.ValueObjects;

            public readonly record struct Rut(string Value);
            """);

        var domainProject = Path.Combine(project.Root, "Demo.Domain.csproj");
        File.WriteAllText(domainProject, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>Demo.Domain</RootNamespace>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="Shared/Shared.Domain.csproj" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(project.Root, "WrappedRut.vsir"), """
            vsir: 0.1
            kind: domain-type
            name: WrappedRut
            classification: value-object
            shape: product
            traits: [transform]
            state:
              Value: Rut
            representation:
              Value: Rut
            construction:
              input:
                Value: Rut
            """);

        var restore = await RunDotNet(project.Root, "restore", domainProject);
        Assert.Equal(0, restore.ExitCode);

        var result = await project.Run(project.Root, "lower", "WrappedRut.vsir");

        Assert.Equal(0, result.ExitCode);
        var materialization = File.ReadAllText(Path.Combine(project.Root, "WrappedRut.vsir.cs"));
        Assert.Contains("using Shared.Domain.ValueObjects;", materialization, StringComparison.Ordinal);
        Assert.Contains("private readonly Rut _value;", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Shared.Domain.ValueObjects.Rut", materialization, StringComparison.Ordinal);
    }

    private static async Task<CliResult> RunDotNet(
        string workingDirectory,
        params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await stdout, await stderr);
    }
}
