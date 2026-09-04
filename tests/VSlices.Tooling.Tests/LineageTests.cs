namespace VSlices.Tooling.Tests;

public sealed class LineageTests
{
    [Fact]
    public async Task New_materialization_records_deterministic_baseline()
    {
        using var project = await ReadyProject();
        var vsir = project.WriteStreetName();
        var materialization = vsir + ".cs";

        var exitCode = await LoweringCoordinator.Execute(
            vsir, "C#", null, null, null, false, "Tests.Domain", CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(materialization));
        var baseline = LoweringLineageStore.ResolveBaselinePath(project.Context(), materialization, "csharp");
        Assert.NotNull(baseline);
        Assert.True(File.Exists(baseline));
        Assert.Equal(File.ReadAllText(materialization), File.ReadAllText(baseline!));
    }

    [Fact]
    public async Task Exact_deterministic_materialization_establishes_lineage_without_rewrite()
    {
        using var project = await ReadyProject();
        var vsir = project.WriteStreetName();
        var deterministic = await TranspilationOperation.Execute(vsir, "C#", "Tests.Domain", CancellationToken.None);
        Assert.True(deterministic.IsSuccess);
        var materialization = vsir + ".cs";
        File.WriteAllText(materialization, deterministic.Source!);
        var before = File.ReadAllBytes(materialization);

        var exitCode = await LoweringCoordinator.Execute(
            vsir, "C#", null, null, null, false, "Tests.Domain", CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(before, File.ReadAllBytes(materialization));
        Assert.True(File.Exists(LoweringLineageStore.ResolveBaselinePath(project.Context(), materialization, "csharp")));
    }

    [Fact]
    public async Task Authorized_bootstrap_preserves_human_witness_byte_for_byte()
    {
        using var project = await ReadyProject();
        var vsir = project.WriteStreetName();
        var deterministic = await TranspilationOperation.Execute(vsir, "C#", "Tests.Domain", CancellationToken.None);
        Assert.True(deterministic.IsSuccess);
        var materialization = vsir + ".cs";
        File.WriteAllText(materialization, deterministic.Source + Environment.NewLine + "// human detail" + Environment.NewLine);
        var before = File.ReadAllBytes(materialization);

        var exitCode = await LoweringCoordinator.Execute(
            vsir, "C#", null, null, null, false, "Tests.Domain", CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(before, File.ReadAllBytes(materialization));
        var baseline = LoweringLineageStore.ResolveBaselinePath(project.Context(), materialization, "csharp");
        Assert.Equal(deterministic.Source, File.ReadAllText(baseline!));
    }

    [Fact]
    public async Task Subsequent_semantic_change_uses_stored_baseline_and_preserves_human_detail()
    {
        using var project = await ReadyProject();
        var vsir = project.WriteStreetName(30);
        var materialization = vsir + ".cs";
        Assert.Equal(0, await LoweringCoordinator.Execute(
            vsir, "C#", null, null, null, false, "Tests.Domain", CancellationToken.None));
        File.AppendAllText(materialization, Environment.NewLine + "// human detail" + Environment.NewLine);
        project.WriteStreetName(31);

        var exitCode = await LoweringCoordinator.Execute(
            vsir, "C#", null, null, null, false, "Tests.Domain", CancellationToken.None);

        Assert.Equal(0, exitCode);
        var source = File.ReadAllText(materialization);
        Assert.Contains("<= 31", source, StringComparison.Ordinal);
        Assert.Contains("// human detail", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_override_does_not_receive_bootstrap_authority_without_ancestry()
    {
        using var project = await ReadyProject();
        var vsir = project.WriteStreetName();
        var deterministic = await TranspilationOperation.Execute(vsir, "C#", "Tests.Domain", CancellationToken.None);
        Assert.True(deterministic.IsSuccess);
        var custom = Path.Combine(project.Root, "custom.cs");
        File.WriteAllText(custom, deterministic.Source + Environment.NewLine + "// human detail" + Environment.NewLine);

        var exitCode = await LoweringCoordinator.Execute(
            vsir, "C#", null, custom, null, false, "Tests.Domain", CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Null(LoweringLineageStore.ResolveBaselinePath(project.Context(), Path.Combine(Path.GetTempPath(), "outside.cs"), "csharp"));
        var customBaseline = LoweringLineageStore.ResolveBaselinePath(project.Context(), custom, "csharp");
        Assert.NotNull(customBaseline);
        Assert.False(File.Exists(customBaseline));
    }

    private static async Task<ToolingTestProject> ReadyProject()
    {
        var project = new ToolingTestProject();
        await project.WriteConfiguration(rulesetRef: null);
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);
        return project;
    }
}
