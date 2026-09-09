using System.Diagnostics;
using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class GeneratedMaterializationCompilationTests
{
    [Fact]
    public async Task Derived_state_projection_and_multiline_failure_compile_as_one_generated_witness()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: ReviewProbe
            shape: product
            classification: value-object
            traits: [transform]

            state:
              Value: string
              Length:
                type: int
                from: state.Value.Length

            representation:
              Length: int

            input:
              Value: string

            construction:
              - ensure:
                  condition:
                    intrinsic: non-empty
                    args:
                      value: input.Value
                  failure:
                    message: |
                      First line
                      Second line
            """;

        var parsed = VsirParser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var loaded = CSharpLoweringRuleSet.Load(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset"));
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var lowered = CSharpLanguageLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Generated.Review", loaded.RuleSet!));
        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("public int Length =>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("new(Length)", lowered.Source, StringComparison.Ordinal);

        // The exact YAML chomp result (whether the final line break is retained)
        // is not the target contract. What matters is that an embedded line break
        // is encoded inside a valid ordinary C# string literal rather than copied
        // into the generated source as a raw newline.
        Assert.Contains("First line\\nSecond line", lowered.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("First line\nSecond line", lowered.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("new(_length)", lowered.Source, StringComparison.Ordinal);

        var root = Path.Combine(Path.GetTempPath(), "vslices-generated-compile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repositoryRoot = FindRepositoryRoot();
            var frameworkProject = Path.Combine(
                repositoryRoot,
                "src", "framework", "src", "VSlices.Domain", "VSlices.Domain.csproj");
            Assert.True(File.Exists(frameworkProject), $"Expected pinned Framework project at '{frameworkProject}'.");

            await File.WriteAllTextAsync(Path.Combine(root, "Generated.cs"), lowered.Source!);
            await File.WriteAllTextAsync(
                Path.Combine(root, "GeneratedWitness.csproj"),
                $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{{frameworkProject}}" />
                    <Using Include="VSlices.Domain.Traits" />
                  </ItemGroup>
                </Project>
                """);

            var build = await RunDotNet(root, "build", "GeneratedWitness.csproj", "--nologo", "--verbosity", "minimal");
            Assert.True(
                build.ExitCode == 0,
                $"Generated materialization did not compile.{Environment.NewLine}{build.StandardOutput}{Environment.NewLine}{build.StandardError}{Environment.NewLine}{lowered.Source}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<ProcessResult> RunDotNet(string workingDirectory, params string[] arguments)
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

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet for generated witness compilation.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await stdout, await stderr);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "tooling.slnx")))
                return current.FullName;
        }

        throw new InvalidOperationException("Could not locate tooling.slnx from test output.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
