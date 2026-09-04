namespace VSlices.Tooling.Tests;

public sealed class LineageTests
{
    [Fact]
    public async Task New_materialization_records_deterministic_baseline()
    {
        using var project = ReadyProject();
        var vsir = project.WriteStreetName();
        var materialization = vsir + ".cs";

        var result = await project.Run(project.Root, "lower", vsir, "--namespace", "Tests.Domain");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(materialization));
        Assert.Equal(File.ReadAllText(materialization), File.ReadAllText(project.BaselineFor(materialization)));
    }

    [Fact]
    public async Task Exact_deterministic_materialization_establishes_lineage_without_rewrite()
    {
        using var project = ReadyProject();
        var vsir = project.WriteStreetName();
        var projection = await project.Run(project.Root, "transpile", vsir, "--namespace", "Tests.Domain", "--stdout");
        Assert.Equal(0, projection.ExitCode);
        var materialization = vsir + ".cs";
        File.WriteAllText(materialization, projection.StandardOutput);
        var before = File.ReadAllBytes(materialization);

        var result = await project.Run(project.Root, "lower", vsir, "--namespace", "Tests.Domain");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(materialization));
        Assert.True(File.Exists(project.BaselineFor(materialization)));
    }

    [Fact]
    public async Task Authorized_bootstrap_preserves_human_witness_byte_for_byte()
    {
        using var project = ReadyProject();
        var vsir = project.WriteStreetName();
        var projection = await project.Run(project.Root, "transpile", vsir, "--namespace", "Tests.Domain", "--stdout");
        Assert.Equal(0, projection.ExitCode);
        var materialization = vsir + ".cs";
        File.WriteAllText(materialization, projection.StandardOutput + Environment.NewLine + "// human detail" + Environment.NewLine);
        var before = File.ReadAllBytes(materialization);

        var result = await project.Run(project.Root, "lower", vsir, "--namespace", "Tests.Domain");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(materialization));
        Assert.Equal(projection.StandardOutput, File.ReadAllText(project.BaselineFor(materialization)));
    }

    [Fact]
    public async Task Subsequent_semantic_change_uses_stored_baseline_and_preserves_human_detail()
    {
        using var project = ReadyProject();
        var vsir = project.WriteStreetName(30);
        var materialization = vsir + ".cs";
        Assert.Equal(0, (await project.Run(project.Root, "lower", vsir, "--namespace", "Tests.Domain")).ExitCode);
        File.AppendAllText(materialization, Environment.NewLine + "// human detail" + Environment.NewLine);
        project.WriteStreetName(31);

        var result = await project.Run(project.Root, "lower", vsir, "--namespace", "Tests.Domain");

        Assert.Equal(0, result.ExitCode);
        var source = File.ReadAllText(materialization);
        Assert.Contains("<= 31", source, StringComparison.Ordinal);
        Assert.Contains("// human detail", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_namespace_insertion_reports_rebase_conflict_then_stops_at_target_context_before_writing()
    {
        using var project = ReadyProject();
        var vsir = project.WriteStreetName();
        var materialization = vsir + ".cs";

        var initial = await project.Run(project.Root,
            "lower", vsir, "--namespace", "Tests.Domain");
        Assert.Equal(0, initial.ExitCode);

        var human = File.ReadAllText(materialization)
            .Replace(
                "namespace Tests.Domain;",
                "namespace Tests.Domain.Aggregates;",
                StringComparison.Ordinal);
        File.WriteAllText(
            materialization,
            human + Environment.NewLine + "// human detail" + Environment.NewLine);
        var humanBeforeResolution = File.ReadAllBytes(materialization);
        var baselineBeforeResolution = File.ReadAllBytes(project.BaselineFor(materialization));

        var conflict = await project.Run(project.Root,
            "lower", vsir,
            "--namespace", "Tests.Domain.Aggregates.Tickets");

        Assert.Equal(1, conflict.ExitCode);
        var conflictText = conflict.StandardError + conflict.StandardOutput;
        Assert.Contains("REB004", conflictText, StringComparison.Ordinal);
        Assert.Contains("Baseline insertion: <empty>", conflictText, StringComparison.Ordinal);
        Assert.Contains("Human insertion: '.Aggregates'", conflictText, StringComparison.Ordinal);
        Assert.Contains("Next deterministic insertion: '.Aggregates.Tickets'", conflictText, StringComparison.Ordinal);
        Assert.Contains("--resolve deterministic", conflictText, StringComparison.Ordinal);

        var resolved = await project.Run(project.Root,
            "lower", vsir,
            "--namespace", "Tests.Domain.Aggregates.Tickets",
            "--resolve", "deterministic");

        Assert.Equal(1, resolved.ExitCode);
        var resolutionText = resolved.StandardError + resolved.StandardOutput;
        Assert.Contains("DOTNET020", resolutionText, StringComparison.Ordinal);
        Assert.Contains("no related .csproj", resolutionText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(humanBeforeResolution, File.ReadAllBytes(materialization));
        Assert.Equal(baselineBeforeResolution, File.ReadAllBytes(project.BaselineFor(materialization)));
    }

    [Fact]
    public async Task Source_override_does_not_receive_bootstrap_authority_without_ancestry()
    {
        using var project = ReadyProject();
        var vsir = project.WriteStreetName();
        var projection = await project.Run(project.Root, "transpile", vsir, "--namespace", "Tests.Domain", "--stdout");
        var custom = Path.Combine(project.Root, "custom.cs");
        File.WriteAllText(custom, projection.StandardOutput + Environment.NewLine + "// human detail" + Environment.NewLine);

        var result = await project.Run(project.Root,
            "lower", vsir, "--namespace", "Tests.Domain", "--source", custom);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("LOWER001", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(project.BaselineFor(custom)));
    }

    private static ToolingTestProject ReadyProject()
    {
        var project = new ToolingTestProject();
        project.WriteConfiguration();
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);
        return project;
    }
}
