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

    [Fact]
    public async Task Evaluated_RootNamespace_is_combined_with_the_full_relative_VSIR_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-target-context-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "Aggregates", "Tickets");
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

            var vsirPath = Path.Combine(nested, "TicketCode.vsir");
            await File.WriteAllTextAsync(vsirPath, "vsir: 0.1");

            var result = await DotNetTargetContextResolver.Resolve(vsirPath, null);

            Assert.Null(result.Diagnostic);
            Assert.NotNull(result.Context);
            Assert.Equal(projectPath, result.Context.ProjectPath);
            Assert.Equal("Tickets.Domain.Aggregates.Tickets", result.Context.Namespace);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Configured_folder_names_do_not_contribute_namespace_segments()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-target-context-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "Aggregates", "Tickets");
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

            var vsirPath = Path.Combine(nested, "TicketCode.vsir");
            await File.WriteAllTextAsync(vsirPath, "vsir: 0.1");

            var result = await DotNetTargetContextResolver.Resolve(
                vsirPath,
                null,
                namespaceIgnoredFolders: ["Tickets"]);

            Assert.Null(result.Diagnostic);
            Assert.NotNull(result.Context);
            Assert.Equal(projectPath, result.Context.ProjectPath);
            Assert.Equal("Tickets.Domain.Aggregates", result.Context.Namespace);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Folder_exclusions_match_exact_path_segments_only()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-target-context-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "Aggregates", "TicketSupport");
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

            var vsirPath = Path.Combine(nested, "TicketCode.vsir");
            await File.WriteAllTextAsync(vsirPath, "vsir: 0.1");

            var result = await DotNetTargetContextResolver.Resolve(
                vsirPath,
                null,
                namespaceIgnoredFolders: ["Ticket"]);

            Assert.Null(result.Diagnostic);
            Assert.NotNull(result.Context);
            Assert.Equal("Tickets.Domain.Aggregates.TicketSupport", result.Context.Namespace);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Explicit_RootNamespace_is_used_instead_of_the_project_file_name()
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-target-context-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "Features", "Orders");
        Directory.CreateDirectory(nested);

        try
        {
            var projectPath = Path.Combine(root, "Arbitrary.Project.csproj");
            await File.WriteAllTextAsync(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <RootNamespace>Company.Product</RootNamespace>
                  </PropertyGroup>
                </Project>
                """);

            var vsirPath = Path.Combine(nested, "OrderId.vsir");
            await File.WriteAllTextAsync(vsirPath, "vsir: 0.1");

            var result = await DotNetTargetContextResolver.Resolve(vsirPath, null);

            Assert.Null(result.Diagnostic);
            Assert.NotNull(result.Context);
            Assert.Equal(projectPath, result.Context.ProjectPath);
            Assert.Equal("Company.Product.Features.Orders", result.Context.Namespace);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
