namespace VSlices.Tooling.Tests;

public sealed class NewVsirCommandTests
{
    [Fact]
    public async Task New_vsir_creates_only_zero_knowledge_identity()
    {
        using var project = new ToolingTestProject();

        var result = await project.Run(project.Root, "new", "vsir", "StreetName2");

        Assert.Equal(0, result.ExitCode);
        var path = Path.Combine(project.Root, "StreetName2.vsir");
        Assert.True(File.Exists(path));
        Assert.Equal(
            "vsir: 0.1\nname: StreetName2\n",
            File.ReadAllText(path).Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task New_vsir_rejects_removed_semantic_flags()
    {
        using var project = new ToolingTestProject();

        var result = await project.Run(
            project.Root,
            "new",
            "vsir",
            "StreetName2",
            "--kind",
            "domain-type");

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(project.Root, "StreetName2.vsir")));
    }
}
