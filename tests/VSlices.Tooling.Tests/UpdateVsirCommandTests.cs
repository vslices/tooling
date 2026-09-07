namespace VSlices.Tooling.Tests;

public sealed class UpdateVsirCommandTests
{
    [Fact]
    public async Task Repeated_set_options_are_all_applied_in_one_cli_invocation()
    {
        using var project = new ToolingTestProject();

        var created = await project.Run(project.Root, "new", "vsir", "StreetName2");
        Assert.Equal(0, created.ExitCode);

        var kind = await project.Run(
            project.Root,
            "update", "vsir", "StreetName2",
            "--set", "kind=domain-type");
        Assert.Equal(0, kind.ExitCode);

        var envelope = await project.Run(
            project.Root,
            "update", "vsir", "StreetName2",
            "--set", "shape=product",
            "--set", "classification=value-object");
        Assert.Equal(0, envelope.ExitCode);

        var source = File.ReadAllText(Path.Combine(project.Root, "StreetName2.vsir"));
        Assert.Contains("shape: product", source);
        Assert.Contains("classification: value-object", source);
    }

    [Fact]
    public async Task Repeated_set_and_add_options_preserve_every_mutation_in_one_cli_invocation()
    {
        using var project = new ToolingTestProject();
        var path = Path.Combine(project.Root, "StreetName2.vsir");
        File.WriteAllText(path, """
            vsir: 0.1
            name: StreetName2
            kind: domain-type
            shape: product
            classification: value-object
            """);

        var result = await project.Run(
            project.Root,
            "update", "vsir", "StreetName2",
            "--set", "state.Value=string",
            "--set", "representation.Value=string",
            "--add", "traits=transform");

        Assert.Equal(0, result.ExitCode);
        var source = File.ReadAllText(path);
        Assert.Contains("state:", source);
        Assert.Contains("Value: string", source);
        Assert.Contains("representation:", source);
        Assert.Contains("traits: [transform]", source);
    }

    [Fact]
    public async Task Repeated_metadata_add_options_are_preserved()
    {
        using var project = new ToolingTestProject();
        var path = Path.Combine(project.Root, "StreetName2.vsir");
        File.WriteAllText(path, """
            vsir: 0.1
            name: StreetName2
            """);

        var result = await project.Run(
            project.Root,
            "update", "vsir", "StreetName2",
            "--add", "tags=ticket",
            "--add", "tags=identity");

        Assert.Equal(0, result.ExitCode);
        var source = File.ReadAllText(path);
        Assert.Contains("ticket", source);
        Assert.Contains("identity", source);
    }
}
