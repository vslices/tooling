namespace VSlices.Tooling.Tests;

public sealed class ProjectConfigurationTests
{
    [Fact]
    public void Loads_CSharp_namespace_ignored_folders()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "vslices-project-config-" + Guid.NewGuid().ToString("N"));
        var vslices = Path.Combine(root, ".vslices");
        Directory.CreateDirectory(vslices);

        try
        {
            File.WriteAllText(
                Path.Combine(vslices, "config.yaml"),
                """
                version: 0.1
                targets:
                  default: csharp
                  csharp:
                    namespace:
                      ignore-folders:
                        - Tickets
                        - Generated
                        - Tickets
                """);

            var configuration = ProjectConfiguration.LoadFromProjectRoot(root);

            Assert.NotNull(configuration);
            Assert.Equal(["Tickets", "Generated"], configuration.CSharpNamespaceIgnoredFolders);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Writes_CSharp_namespace_ignored_folders_only_when_configured()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "vslices-project-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var configuration = ProjectConfiguration.Default() with
            {
                CSharpNamespaceIgnoredFolders = ["Tickets"]
            };

            await ProjectConfiguration.WriteAsync(root, configuration, CancellationToken.None);

            var text = File.ReadAllText(Path.Combine(root, ".vslices", "config.yaml"));
            Assert.Contains("csharp:", text, StringComparison.Ordinal);
            Assert.Contains("namespace:", text, StringComparison.Ordinal);
            Assert.Contains("ignore-folders:", text, StringComparison.Ordinal);
            Assert.Contains("Tickets", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
