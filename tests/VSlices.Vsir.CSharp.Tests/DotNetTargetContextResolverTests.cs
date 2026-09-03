using VSlices.Targets.DotNet;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class DotNetTargetContextResolverTests
{
    [Fact]
    public async Task Explicit_namespace_does_not_require_a_csproj()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-target-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var vsirPath = Path.Combine(root, "StreetName.vsir");
            await File.WriteAllTextAsync(vsirPath, "vsir: 0.1");

            var result = await DotNetTargetContextResolver.Resolve(
                vsirPath,
                "Identities.Domain.ValueObjects");

            Assert.Null(result.Diagnostic);
            Assert.NotNull(result.Context);
            Assert.Null(result.Context.ProjectPath);
            Assert.Equal("Identities.Domain.ValueObjects", result.Context.Namespace);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
