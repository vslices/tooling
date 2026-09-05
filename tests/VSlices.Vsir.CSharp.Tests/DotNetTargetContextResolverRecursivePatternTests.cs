using VSlices.Targets.DotNet;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class DotNetTargetContextResolverRecursivePatternTests
{
    [Fact]
    public async Task Recursive_pattern_can_ignore_every_descendant_of_a_namespace_anchor()
    {
        await WithVsir(
            ["Aggregates", "Tickets", "Entities", "History"],
            ["Aggregates/**/*"],
            expectedNamespace: "Tickets.Domain.Aggregates");
    }

    [Fact]
    public async Task Recursive_context_can_target_a_specific_terminal_folder_name()
    {
        await WithVsir(
            ["Aggregates", "Orders", "Details", "Entities"],
            ["Aggregates/**/Entities"],
            expectedNamespace: "Tickets.Domain.Aggregates.Orders.Details");
    }

    [Fact]
    public async Task Exact_path_pattern_ignores_only_its_terminal_folder()
    {
        await WithVsir(
            ["Aggregates", "Tickets", "Entities"],
            ["Aggregates/Tickets/Entities"],
            expectedNamespace: "Tickets.Domain.Aggregates.Tickets");
    }

    [Fact]
    public async Task Recursive_pattern_does_not_remove_the_anchor_folder_itself()
    {
        await WithVsir(
            ["Aggregates", "Orders"],
            ["Aggregates/**/*"],
            expectedNamespace: "Tickets.Domain.Aggregates");
    }

    private static async Task WithVsir(
        IReadOnlyList<string> relativeFolders,
        IReadOnlyCollection<string> ignoredFolders,
        string expectedNamespace)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "vslices-target-context-recursive-" + Guid.NewGuid().ToString("N"));
        var nested = relativeFolders.Aggregate(root, (current, folder) => Path.Combine(current, folder));
        Directory.CreateDirectory(nested);

        try
        {
            var projectPath = Path.Combine(root, "Tickets.Domain.csproj");
            await File.WriteAllTextAsync(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var vsirPath = Path.Combine(nested, "Specimen.vsir");
            await File.WriteAllTextAsync(vsirPath, "vsir: 0.1");

            var result = await DotNetTargetContextResolver.Resolve(
                vsirPath,
                null,
                namespaceIgnoredFolders: ignoredFolders);

            Assert.Null(result.Diagnostic);
            Assert.NotNull(result.Context);
            Assert.Equal(projectPath, result.Context.ProjectPath);
            Assert.Equal(expectedNamespace, result.Context.Namespace);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
