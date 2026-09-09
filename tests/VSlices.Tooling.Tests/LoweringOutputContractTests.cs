namespace VSlices.Tooling.Tests;

public sealed class LoweringOutputContractTests
{
    [Fact]
    public async Task Bootstrap_stdout_aliases_are_byte_equivalent_for_preserved_human_materialization()
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

        var outputDash = await project.Run(
            project.Root,
            "lower", vsir,
            "--namespace", "Review",
            "--output", "-");

        Assert.Equal(0, outputDash.ExitCode);
        Assert.Equal(humanBefore, outputDash.StandardOutput);
        Assert.Contains("human-preserved-detail", outputDash.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Lineage bootstrap", outputDash.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Lineage bootstrap", outputDash.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(humanBefore, await File.ReadAllTextAsync(materialization));
        Assert.True(File.Exists(baseline));

        File.Delete(baseline);
        var stdout = await project.Run(
            project.Root,
            "lower", vsir,
            "--namespace", "Review",
            "--stdout");

        Assert.Equal(0, stdout.ExitCode);
        Assert.Equal(outputDash.StandardOutput, stdout.StandardOutput);
        Assert.Equal(humanBefore, stdout.StandardOutput);
        Assert.Contains("Lineage bootstrap", stdout.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Lineage bootstrap", stdout.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(humanBefore, await File.ReadAllTextAsync(materialization));
        Assert.True(File.Exists(baseline));
    }

    [Fact]
    public async Task Bootstrap_stdout_aliases_are_byte_equivalent_for_exact_deterministic_materialization()
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
        var deterministic = await File.ReadAllTextAsync(materialization);
        Assert.True(File.Exists(baseline));

        File.Delete(baseline);
        var outputDash = await project.Run(
            project.Root,
            "lower", vsir,
            "--namespace", "Review",
            "--output", "-");

        Assert.Equal(0, outputDash.ExitCode);
        Assert.Equal(deterministic, outputDash.StandardOutput);
        Assert.Contains("Established lowering lineage", outputDash.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Established lowering lineage", outputDash.StandardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(baseline));

        File.Delete(baseline);
        var stdout = await project.Run(
            project.Root,
            "lower", vsir,
            "--namespace", "Review",
            "--stdout");

        Assert.Equal(0, stdout.ExitCode);
        Assert.Equal(outputDash.StandardOutput, stdout.StandardOutput);
        Assert.Equal(deterministic, stdout.StandardOutput);
        Assert.Contains("Established lowering lineage", stdout.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Established lowering lineage", stdout.StandardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(baseline));
    }
}
