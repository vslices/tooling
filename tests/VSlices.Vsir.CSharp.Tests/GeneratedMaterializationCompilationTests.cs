using System.Diagnostics;
using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class GeneratedMaterializationCompilationTests
{
    [Fact]
    public async Task Product_and_sum_generated_pipelines_compile_against_pinned_framework()
    {
        const string productSource = """
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

        const string sumSource = """
            vsir: 0.1
            kind: domain-type
            name: ReviewChoice
            shape: sum
            classification: value-object

            state: {}
            representation: {}

            variants:
              NamedChoice:
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
                        message: A choice value is required
            """;

        var loaded = CSharpLoweringRuleSet.Load(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset"));
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        var parsedProduct = VsirParser.Parse(productSource);
        Assert.True(parsedProduct.IsSuccess, string.Join(Environment.NewLine, parsedProduct.Diagnostics));
        var loweredProduct = CSharpLanguageLowerer.Lower(
            parsedProduct.Document!,
            new CSharpLoweringContext("Generated.Review", loaded.RuleSet!));
        Assert.True(loweredProduct.IsSuccess, string.Join(Environment.NewLine, loweredProduct.Diagnostics));
        Assert.Contains("public int Length =>", loweredProduct.Source, StringComparison.Ordinal);
        Assert.Contains("new(Length)", loweredProduct.Source, StringComparison.Ordinal);

        // The exact YAML chomp result (whether the final line break is retained)
        // is not the target contract. What matters is that an embedded line break
        // is encoded inside a valid ordinary C# string literal rather than copied
        // into the generated source as a raw newline.
        Assert.Contains("First line\\nSecond line", loweredProduct.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("First line\nSecond line", loweredProduct.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("new(_length)", loweredProduct.Source, StringComparison.Ordinal);

        var parsedSum = VsirParser.Parse(sumSource);
        Assert.True(parsedSum.IsSuccess, string.Join(Environment.NewLine, parsedSum.Diagnostics));
        var loweredSum = CSharpSumDomainTypeLowerer.Lower(
            parsedSum.Document!,
            new CSharpLoweringContext("Generated.Review", loaded.RuleSet!));
        Assert.True(loweredSum.IsSuccess, string.Join(Environment.NewLine, loweredSum.Diagnostics));
        Assert.Contains("public sealed class NamedChoice", loweredSum.Source, StringComparison.Ordinal);

        var root = Path.Combine(Path.GetTempPath(), "vslices-generated-compile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repositoryRoot = FindRepositoryRoot();
            var frameworkProject = Path.Combine(
                repositoryRoot,
                "src", "framework", "src", "VSlices.Domain", "VSlices.Domain.csproj");
            Assert.True(File.Exists(frameworkProject), $"Expected pinned Framework project at '{frameworkProject}'.");

            await File.WriteAllTextAsync(Path.Combine(root, "GeneratedProduct.cs"), loweredProduct.Source!);
            await File.WriteAllTextAsync(Path.Combine(root, "GeneratedSum.cs"), loweredSum.Source!);
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
                    <Using Include="VSlices.Arrows" />
                    <Using Include="VSlices.Domain.Traits" />
                  </ItemGroup>
                </Project>
                """);

            // The outer solution build already produces the pinned Framework in
            // Release. Building the generated witness in the same configuration
            // preserves the real ProjectReference contract without paying for a
            // second Debug build of the entire Framework graph.
            var build = await RunDotNet(
                root,
                "build",
                "GeneratedWitness.csproj",
                "--configuration",
                "Release",
                "--nologo",
                "--verbosity",
                "minimal");
            Assert.True(
                build.ExitCode == 0,
                $"Generated materialization did not compile.{Environment.NewLine}{build.StandardOutput}{Environment.NewLine}{build.StandardError}{Environment.NewLine}PRODUCT:{Environment.NewLine}{loweredProduct.Source}{Environment.NewLine}SUM:{Environment.NewLine}{loweredSum.Source}");
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
