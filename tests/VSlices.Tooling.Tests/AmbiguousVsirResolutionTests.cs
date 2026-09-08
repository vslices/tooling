namespace VSlices.Tooling.Tests;

public sealed class AmbiguousVsirResolutionTests
{
    [Fact]
    public async Task Lower_lists_ambiguous_vsir_candidates_as_relative_paths()
    {
        using var project = new ToolingTestProject();

        var first = Path.Combine(project.Root, "Products", "A");
        var second = Path.Combine(project.Root, "Products", "B");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        File.WriteAllText(Path.Combine(first, "IncidentTypeReference.vsir"), "vsir: 0.1");
        File.WriteAllText(Path.Combine(second, "IncidentTypeReference.vsir"), "vsir: 0.1");

        var result = await project.Run(project.Root, "lower", "IncidentTypeReference");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("CLI002", result.StandardError, StringComparison.Ordinal);
        Assert.Contains(
            Path.Combine("Products", "A", "IncidentTypeReference.vsir"),
            result.StandardError,
            StringComparison.Ordinal);
        Assert.Contains(
            Path.Combine("Products", "B", "IncidentTypeReference.vsir"),
            result.StandardError,
            StringComparison.Ordinal);
    }
}
