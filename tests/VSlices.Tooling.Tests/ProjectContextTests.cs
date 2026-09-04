namespace VSlices.Tooling.Tests;

public sealed class ProjectContextTests
{
    [Fact]
    public async Task Finds_nearest_project_and_resolves_canonical_roots()
    {
        using var project = new ToolingTestProject();
        await project.WriteConfiguration();
        var nested = Path.Combine(project.Root, "a", "b", "c");
        Directory.CreateDirectory(nested);

        var context = VSlicesProjectContext.FindFrom(nested);

        Assert.NotNull(context);
        Assert.Equal(project.Root, context!.ProjectRoot);
        Assert.Equal(project.VslicesRoot, context.VslicesRoot);
        Assert.Equal(Path.Combine(project.VslicesRoot, "config.yaml"), context.ConfigurationPath);
        Assert.Equal(project.RulesetRoot, context.RulesetRoot);
        Assert.Equal(project.LineageRoot, context.LineageRoot);
    }

    [Fact]
    public async Task Nearest_nested_project_wins()
    {
        using var project = new ToolingTestProject();
        await project.WriteConfiguration();
        var nestedRoot = Path.Combine(project.Root, "nested");
        Directory.CreateDirectory(nestedRoot);
        await ProjectConfiguration.WriteAsync(
            nestedRoot,
            ProjectConfiguration.Default(),
            CancellationToken.None);
        var child = Path.Combine(nestedRoot, "child");
        Directory.CreateDirectory(child);

        var context = VSlicesProjectContext.FindFrom(child);

        Assert.NotNull(context);
        Assert.Equal(nestedRoot, context!.ProjectRoot);
    }

    [Fact]
    public async Task Lineage_paths_cannot_escape_project_boundary()
    {
        using var project = new ToolingTestProject();
        await project.WriteConfiguration();
        var context = project.Context();
        var outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N") + ".cs");

        Assert.False(context.Contains(outside));
        Assert.Null(LoweringLineageStore.ResolveBaselinePath(context, outside, "csharp"));
    }
}
