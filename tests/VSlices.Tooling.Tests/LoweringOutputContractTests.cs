namespace VSlices.Tooling.Tests;

public sealed class LoweringOutputContractTests
{
    [Fact]
    public async Task Bootstrap_stdout_returns_the_preserved_human_materialization_and_keeps_status_on_stderr()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);
        var vsir = project.WriteStreetName();

        var initial = await project.Run(
            project.Root,
            "lower", vsir,
            "--namespace", "Review");
        Assert.Equal(0, initial.ExitCode);

        var materialization = vsir + ".cs";
        var baseline = project.BaselineFor(materialization);
        Assert.True(File.Exists(baseline));

        File.Delete(baseline);
        await File.AppendAllTextAsync(materialization, "// human-preserved-detail\n");
        var humanBefore = await File.ReadAllTextAsync(materialization);

        var stdout = await project.Run(
            project.Root,
            "lower", vsir,
            "--namespace", "Review",
            "--stdout");

        Assert.Equal(0, stdout.ExitCode);
        Assert.Equal(humanBefore, stdout.StandardOutput);
        Assert.Contains("human-preserved-detail", stdout.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Lineage bootstrap", stdout.StandardError, StringComparison.Ordinal);
        Assert.Equal(humanBefore, await File.ReadAllTextAsync(materialization));
        Assert.True(File.Exists(baseline));
    }
}
