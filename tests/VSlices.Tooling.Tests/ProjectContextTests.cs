namespace VSlices.Tooling.Tests;

public sealed class ProjectContextTests
{
    [Fact]
    public async Task Nested_command_uses_nearest_project_context_and_root_lineage()
    {
        using var project = ReadyProject();
        var nested = Path.Combine(project.Root, "a", "b");
        var vsir = project.WriteStreetName(directory: nested);

        var result = await project.Run(nested, "lower", "StreetName.vsir", "--namespace", "Tests.Domain");

        Assert.Equal(0, result.ExitCode);
        var materialization = vsir + ".cs";
        Assert.True(File.Exists(materialization));
        Assert.True(File.Exists(project.BaselineFor(materialization)));
    }

    [Fact]
    public async Task Nested_project_boundary_wins_over_outer_project()
    {
        using var project = ReadyProject();
        var nested = Path.Combine(project.Root, "nested");
        Directory.CreateDirectory(Path.Combine(nested, ".vslices", "ruleset"));
        File.WriteAllText(Path.Combine(nested, ".vslices", "config.yaml"), """
            version: 0.1
            targets:
              default: csharp
            ruleset:
              source: local
            """);
        File.WriteAllText(Path.Combine(nested, ".vslices", "ruleset", "manifest.yaml"), "targets: {}\n");
        project.WriteStreetName(directory: nested);

        var result = await project.Run(nested, "lower", "StreetName.vsir", "--namespace", "Tests.Domain");

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(nested, "StreetName.vsir.cs")));
    }

    [Fact]
    public async Task Lineage_does_not_escape_project_when_output_is_outside()
    {
        using var project = ReadyProject();
        var vsir = project.WriteStreetName();
        var outside = Path.Combine(Path.GetTempPath(), "vslices-outside-" + Guid.NewGuid().ToString("N") + ".cs");
        try
        {
            var result = await project.Run(project.Root,
                "lower", vsir, "--namespace", "Tests.Domain", "-o", outside);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outside));
            Assert.False(Directory.Exists(project.LineageRoot));
        }
        finally
        {
            if (File.Exists(outside))
                File.Delete(outside);
        }
    }

    private static ToolingTestProject ReadyProject()
    {
        var project = new ToolingTestProject();
        project.WriteConfiguration();
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);
        return project;
    }
}
