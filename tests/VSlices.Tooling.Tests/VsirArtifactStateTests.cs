namespace VSlices.Tooling.Tests;

public sealed class VsirArtifactStateTests
{
    [Fact]
    public async Task Discovery_reports_progressive_validity_without_claiming_conformance_or_lowerability()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        File.WriteAllText(Path.Combine(project.Root, "Example.vsir"), """
            vsir: 0.1
            name: Example
            """);

        var result = await project.Run(project.Root, "discovery", "vsir", "Example.vsir");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("progressive validity: valid", result.StandardOutput);
        Assert.Contains("conformance: incomplete", result.StandardOutput);
        Assert.Contains("missing required: kind", result.StandardOutput);
        Assert.Contains("lowerability: not evaluated by discovery", result.StandardOutput);
    }

    [Fact]
    public async Task Discovery_reports_conforming_for_complete_canonical_VSIR()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        project.WriteStreetName();

        var result = await project.Run(project.Root, "discovery", "vsir", "StreetName.vsir");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("progressive validity: valid", result.StandardOutput);
        Assert.Contains("conformance: conforming", result.StandardOutput);
        Assert.DoesNotContain("missing required:", result.StandardOutput);
        Assert.Contains("lowerability: not evaluated by discovery", result.StandardOutput);
    }
}
