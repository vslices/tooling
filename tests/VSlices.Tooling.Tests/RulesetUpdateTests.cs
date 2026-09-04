namespace VSlices.Tooling.Tests;

public sealed class RulesetUpdateTests
{
    [Fact]
    public async Task Valid_local_source_replaces_snapshot_and_cleans_staging()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "source-ruleset");
        ToolingTestProject.WriteValidRuleset(source, "new.marker");
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot, "old.marker");
        await project.WriteConfiguration(source, null);

        var exitCode = await RulesetUpdater.Update(project.Context(), CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(project.RulesetRoot, "new.marker")));
        Assert.False(File.Exists(Path.Combine(project.RulesetRoot, "old.marker")));
        Assert.Empty(Directory.EnumerateDirectories(project.VslicesRoot)
            .Where(path => Path.GetFileName(path).StartsWith(".ruleset-", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("missing-manifest")]
    [InlineData("missing-target")]
    [InlineData("missing-rule-file")]
    [InlineData("duplicate-rule")]
    [InlineData("invalid-renderer")]
    public async Task Invalid_source_never_replaces_current_snapshot(string invalidCase)
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "invalid-source");
        WriteInvalidRuleset(source, invalidCase);
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot, "old.marker");
        await project.WriteConfiguration(source, null);

        var exitCode = await RulesetUpdater.Update(project.Context(), CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.True(File.Exists(Path.Combine(project.RulesetRoot, "old.marker")));
        Assert.Empty(Directory.EnumerateDirectories(project.VslicesRoot)
            .Where(path => Path.GetFileName(path).StartsWith(".ruleset-", StringComparison.Ordinal)));
    }

    [Fact]
    public void GitHub_ref_resolution_represents_branch_tag_and_commit_candidates_explicitly()
    {
        var candidates = RulesetSourceMaterializer.GitHubArchiveCandidates(
            new Uri("https://github.com/vslices/ruleset"),
            "feat/example");

        Assert.Equal(3, candidates.Count);
        Assert.Equal("https://github.com/vslices/ruleset/archive/refs/heads/feat/example.zip", candidates[0]);
        Assert.Equal("https://github.com/vslices/ruleset/archive/refs/tags/feat/example.zip", candidates[1]);
        Assert.Contains("/archive/feat%2Fexample.zip", candidates[2], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Local_source_rejects_git_ref_instead_of_silently_treating_it_as_branch()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "source-ruleset");
        ToolingTestProject.WriteValidRuleset(source);

        var result = await RulesetSourceMaterializer.Materialize(
            new RulesetSource(source, "main"),
            Path.Combine(project.Root, "staging"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("RSM002", result.DiagnosticCode);
    }

    private static void WriteInvalidRuleset(string root, string invalidCase)
    {
        Directory.CreateDirectory(root);
        if (invalidCase == "missing-manifest")
            return;

        if (invalidCase == "missing-target")
        {
            File.WriteAllText(Path.Combine(root, "manifest.yaml"), "targets: {}\n");
            return;
        }

        Directory.CreateDirectory(Path.Combine(root, "csharp"));
        File.WriteAllText(Path.Combine(root, "manifest.yaml"), """
            targets:
              csharp:
                rules:
                  - csharp/intrinsics.yaml
            """);

        if (invalidCase == "missing-rule-file")
            return;

        if (invalidCase == "duplicate-rule")
        {
            File.WriteAllText(Path.Combine(root, "csharp", "intrinsics.yaml"), """
                rules:
                  - node: intrinsic.non-empty
                    mode: deterministic
                    renderer: expression
                    template: one
                  - node: intrinsic.non-empty
                    mode: deterministic
                    renderer: expression
                    template: two
                """);
            return;
        }

        File.WriteAllText(Path.Combine(root, "csharp", "intrinsics.yaml"), """
            rules:
              - node: intrinsic.non-empty
                mode: deterministic
                renderer: imaginary
                template: something
            """);
    }
}
