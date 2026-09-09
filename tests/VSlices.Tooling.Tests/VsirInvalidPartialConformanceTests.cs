namespace VSlices.Tooling.Tests;

public sealed class VsirInvalidPartialConformanceTests
{
    [Fact]
    public async Task Discovery_preserves_present_invalid_assertions_even_when_other_required_knowledge_is_missing()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        var path = Path.Combine(project.Root, "BadPartial.vsir");
        File.WriteAllText(path, """
            vsir: 0.1
            kind: domain-type
            name: BadPartial
            shape: product
            classification: banana
            """);

        var result = await project.Run(project.Root, "discovery", "vsir", path);
        var output = result.StandardOutput + result.StandardError;

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("conformance: invalid", output, StringComparison.Ordinal);
        Assert.Contains("missing required:", output, StringComparison.Ordinal);
        Assert.Contains("state", output, StringComparison.Ordinal);
        Assert.Contains("representation", output, StringComparison.Ordinal);
        Assert.Contains("VSIR202", output, StringComparison.Ordinal);
        Assert.Contains("banana", output, StringComparison.Ordinal);
    }
}
