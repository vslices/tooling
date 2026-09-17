using VSlices.Targets.DotNet;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class DotNetTargetContextOverrideTests
{
    [Fact]
    public async Task Explicit_namespace_overrides_only_namespace_when_a_project_is_available()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-target-context-override-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var projectPath = Path.Combine(root, "Review.Domain.csproj");
            await File.WriteAllTextAsync(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            var vsirPath = Path.Combine(root, "Probe.vsir");
            await File.WriteAllTextAsync(vsirPath, "vsir: 0.1");

            var result = await DotNetTargetContextResolver.Resolve(vsirPath, "Review.Override");

            Assert.Null(result.Diagnostic);
            Assert.NotNull(result.Context);
            Assert.Equal(projectPath, result.Context.ProjectPath);
            Assert.Equal("Review.Override", result.Context.Namespace);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
