using VSlices.Tooling;

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
        project.WriteConfiguration(source);

        var result = await project.Run(project.Root, "update", "--ruleset");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.RulesetRoot, "new.marker")));
        Assert.False(File.Exists(Path.Combine(project.RulesetRoot, "old.marker")));
        Assert.DoesNotContain(Directory.EnumerateDirectories(project.VslicesRoot),
            path => Path.GetFileName(path).StartsWith(".ruleset-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ruleset_update_preserves_project_owned_extension_overlay()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "source-ruleset");
        ToolingTestProject.WriteValidRuleset(source, "new.marker");
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot, "old.marker");
        project.WriteConfiguration(source);
        WriteProjectExtensionMarker(project, "keep-me");

        var result = await project.Run(project.Root, "update", "--ruleset");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.ExtensionsRoot, "keep-me")));
        Assert.True(File.Exists(Path.Combine(project.ExtensionsRoot, "manifest.yaml")));
    }

    [Fact]
    public async Task Force_init_preserves_project_owned_extension_overlay()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "source-ruleset");
        ToolingTestProject.WriteValidRuleset(source, "new.marker");
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot, "old.marker");
        project.WriteConfiguration(source);
        WriteProjectExtensionMarker(project, "keep-me");

        var result = await project.Run(
            project.Root,
            "init", "--force", "--from", source, "--target", "C#");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.ExtensionsRoot, "keep-me")));
        Assert.True(File.Exists(Path.Combine(project.ExtensionsRoot, "manifest.yaml")));
    }

    [Fact]
    public async Task Force_init_with_local_source_clears_stale_git_ref_and_remains_updatable()
    {
        using var project = new ToolingTestProject();
        var localSource = Path.Combine(project.Root, "local-ruleset");
        ToolingTestProject.WriteValidRuleset(localSource, "local.marker");
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot, "old.marker");
        project.WriteConfiguration("https://github.com/vslices/ruleset", "main");

        var initialized = await project.Run(
            project.Root,
            "init", "--force", "--from", localSource, "--target", "C#");

        Assert.Equal(0, initialized.ExitCode);
        var configuration = File.ReadAllText(Path.Combine(project.VslicesRoot, "config.yaml"));
        Assert.Contains($"source: {localSource}", configuration, StringComparison.Ordinal);
        Assert.DoesNotContain("ref:", configuration, StringComparison.Ordinal);

        var updated = await project.Run(project.Root, "update", "--ruleset");

        Assert.Equal(0, updated.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.RulesetRoot, "local.marker")));
    }

    [Theory]
    [InlineData("missing-manifest")]
    [InlineData("missing-target")]
    [InlineData("missing-rule-file")]
    [InlineData("duplicate-rule")]
    [InlineData("invalid-renderer")]
    [InlineData("source-owned-extensions")]
    [InlineData("non-scalar-rule-file")]
    [InlineData("non-mapping-rule")]
    public async Task Invalid_source_never_replaces_current_snapshot(string invalidCase)
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "invalid-source");
        WriteInvalidRuleset(source, invalidCase);
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot, "old.marker");
        project.WriteConfiguration(source);

        var result = await project.Run(project.Root, "update", "--ruleset");

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.RulesetRoot, "old.marker")));
        Assert.DoesNotContain(Directory.EnumerateDirectories(project.VslicesRoot),
            path => Path.GetFileName(path).StartsWith(".ruleset-", StringComparison.Ordinal));
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
    public async Task Local_source_with_ref_fails_instead_of_silently_treating_ref_as_branch()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "source-ruleset");
        ToolingTestProject.WriteValidRuleset(source);
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot, "old.marker");
        project.WriteConfiguration(source, "main");

        var result = await project.Run(project.Root, "update", "--ruleset");

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(project.RulesetRoot, "old.marker")));
    }

    private static void WriteProjectExtensionMarker(ToolingTestProject project, string marker)
    {
        Directory.CreateDirectory(project.ExtensionsRoot);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "manifest.yaml"), """
            version: 0.1
            catalogs: []
            """);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, marker), marker);
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

        if (invalidCase == "source-owned-extensions")
        {
            File.WriteAllText(Path.Combine(root, "manifest.yaml"), """
                extensions:
                  - extensions/normalize.yaml
                targets:
                  csharp:
                    rules:
                      - csharp/intrinsics.yaml
                """);
            File.WriteAllText(Path.Combine(root, "csharp", "intrinsics.yaml"), "rules: []\n");
            Directory.CreateDirectory(Path.Combine(root, "extensions"));
            File.WriteAllText(Path.Combine(root, "extensions", "normalize.yaml"), "extensions: []\n");
            return;
        }

        if (invalidCase == "non-scalar-rule-file")
        {
            File.WriteAllText(Path.Combine(root, "manifest.yaml"), """
                targets:
                  csharp:
                    rules:
                      - path: csharp/intrinsics.yaml
                """);
            return;
        }

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

        if (invalidCase == "non-mapping-rule")
        {
            File.WriteAllText(Path.Combine(root, "csharp", "intrinsics.yaml"), """
                rules:
                  - intrinsic.non-empty
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
