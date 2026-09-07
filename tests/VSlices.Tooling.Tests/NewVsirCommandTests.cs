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

    [Theory]
    [InlineData("--kind", "domain-type")]
    [InlineData("--shape", "product")]
    [InlineData("--classification", "value-object")]
    [InlineData("--output", "Other.vsir")]
    [InlineData("--stdout", null)]
    [InlineData("--force", null)]
    public async Task New_vsir_rejects_every_removed_flag(string flag, string? value)
    {
        using var project = new ToolingTestProject();

        var arguments = new List<string> { "new", "vsir", "StreetName2", flag };
        if (value is not null)
            arguments.Add(value);

        var result = await project.Run(project.Root, [.. arguments]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(project.Root, "StreetName2.vsir")));
        Assert.False(File.Exists(Path.Combine(project.Root, "Other.vsir")));
    }

    [Fact]
    public async Task New_vsir_does_not_overwrite_existing_artifact()
    {
        using var project = new ToolingTestProject();
        var path = Path.Combine(project.Root, "StreetName2.vsir");
        File.WriteAllText(path, "sentinel");

        var result = await project.Run(project.Root, "new", "vsir", "StreetName2");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("sentinel", File.ReadAllText(path));
    }
}
