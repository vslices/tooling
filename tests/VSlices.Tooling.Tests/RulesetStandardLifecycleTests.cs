namespace VSlices.Tooling.Tests;

public sealed class RulesetStandardLifecycleTests
{
    [Fact]
    public async Task New_creates_minimal_editable_local_ruleset_standard()
    {
        using var project = Project();
        var created = await project.Run(project.Root, "new", "ruleset-standard", "serviu");
        Assert.Equal(0, created.ExitCode);
        var manifest = Path.Combine(Local(project, "serviu"), "manifest.yaml");
        Assert.Contains("name: serviu", File.ReadAllText(manifest), StringComparison.Ordinal);
        Assert.Contains("targets: {}", File.ReadAllText(manifest), StringComparison.Ordinal);
        var list = await project.Run(project.Root, "list", "rulesets");
        Assert.Equal(0, list.ExitCode);
        Assert.Contains("serviu [local]", list.StandardOutput);
        var discovery = await project.Run(project.Root, "discovery", "ruleset-standard",
            "--ruleset-standard", "serviu");
        Assert.Equal(0, discovery.ExitCode);
        Assert.Contains("No target lowering rules", discovery.StandardOutput);
    }

    [Fact]
    public async Task New_without_name_requires_name_noninteractively()
    {
        using var project = Project();
        var result = await project.Run(project.Root, "new", "ruleset-standard");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("RSNEW001", result.StandardError);
    }

    [Fact]
    public async Task Import_uses_declared_name_or_explicit_alias_without_rewriting_origin()
    {
        using var project = Project();
        var source = Path.Combine(project.Root, "source");
        WriteRules(source, "alpha", "intrinsic.non-empty");
        Assert.Equal(0, (await project.Run(project.Root, "new", "ruleset-standard", "--origin", source)).ExitCode);
        Assert.Equal(0, (await project.Run(project.Root, "new", "ruleset-standard",
            "alternate", "--origin", source)).ExitCode);
        Assert.True(File.Exists(Path.Combine(Managed(project, "alpha"), "manifest.yaml")));
        Assert.True(File.Exists(Path.Combine(Managed(project, "alternate"), "manifest.yaml")));
        Assert.Contains("name: alpha", File.ReadAllText(Path.Combine(source, "manifest.yaml")));
        var listed = await project.Run(project.Root, "list", "rulesets");
        Assert.Contains("alternate [managed]", listed.StandardOutput);
        Assert.Contains(source, listed.StandardOutput);
    }

    [Fact]
    public async Task Conflicting_names_between_local_and_imported_installs_are_rejected()
    {
        using var project = Project();
        var source = Path.Combine(project.Root, "source");
        WriteRules(source, "serviu", "intrinsic.non-empty");
        Assert.Equal(0, (await project.Run(project.Root,
            "new", "ruleset-standard", "serviu")).ExitCode);
        var result = await project.Run(project.Root,
            "new", "ruleset-standard", "--origin", source);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Specify a different [name]", result.StandardError);
        Assert.False(Directory.Exists(Managed(project, "serviu")));
        Assert.True(Directory.Exists(Local(project, "serviu")));
        Assert.Equal(0, (await project.Run(project.Root,
            "new", "ruleset-standard", "serviu-imported", "--origin", source)).ExitCode);
    }

    [Fact]
    public async Task Missing_origin_warns_without_installation_and_existing_invalid_origin_fails()
    {
        using var project = Project();
        var missing = await project.Run(project.Root,
            "new", "ruleset-standard", "--origin", Path.Combine(project.Root, "missing"));
        Assert.Equal(0, missing.ExitCode);
        Assert.Contains("RSNEW006", missing.StandardOutput);
        Assert.Contains("No Ruleset Standards installed",
            (await project.Run(project.Root, "list", "rulesets")).StandardOutput);

        var invalid = Path.Combine(project.Root, "invalid");
        Directory.CreateDirectory(invalid);
        File.WriteAllText(Path.Combine(invalid, "manifest.yaml"), "name: invalid\ntargets: {}\n");
        var failure = await project.Run(project.Root,
            "new", "ruleset-standard", "--origin", invalid);
        Assert.NotEqual(0, failure.ExitCode);
        Assert.Contains("RSI002", failure.StandardError);
    }

    [Fact]
    public async Task Update_all_generates_markdown_diff_and_skips_local_standards()
    {
        using var project = Project();
        var a = Path.Combine(project.Root, "source-a");
        var b = Path.Combine(project.Root, "source-b");
        WriteRules(a, "alpha", "intrinsic.non-empty", "beforeA");
        WriteRules(b, "beta", "intrinsic.other", "beforeB");
        await Install(project, a);
        await Install(project, b);
        Assert.Equal(0, (await project.Run(project.Root, "new", "ruleset-standard", "local-team")).ExitCode);
        WriteRules(a, "alpha", "intrinsic.non-empty", "afterA");
        WriteRules(b, "beta", "intrinsic.other", "afterB");
        var updated = await project.Run(project.Root, "update", "ruleset-standard", "--skip-confirm");
        Assert.Equal(0, updated.ExitCode);
        Assert.Contains("Skipping local Ruleset Standard 'local-team'", updated.StandardOutput);
        Assert.Contains("afterA", File.ReadAllText(Rules(project, "alpha")));
        Assert.Contains("afterB", File.ReadAllText(Rules(project, "beta")));
        var reports = Path.Combine(project.VslicesRoot, "reports", "ruleset-standard-updates");
        Assert.Equal(2, Directory.GetFiles(reports, "*.md").Length);
        Assert.Contains("afterA", File.ReadAllText(Directory.GetFiles(reports, "alpha-*.md")[0]));
        Assert.True(File.Exists(Path.Combine(Local(project, "local-team"), "manifest.yaml")));
    }

    [Fact]
    public async Task Update_diff_and_confirmation_flags_are_independent()
    {
        using var project = Project();
        var source = Path.Combine(project.Root, "source");
        WriteRules(source, "alpha", "intrinsic.non-empty", "first");
        await Install(project, source);
        WriteRules(source, "alpha", "intrinsic.non-empty", "second");

        var needsConfirmation = await project.Run(project.Root, "update", "ruleset-standard", "alpha");
        Assert.NotEqual(0, needsConfirmation.ExitCode);
        Assert.Contains("RSCONF001", needsConfirmation.StandardError);
        Assert.Contains("first", File.ReadAllText(Rules(project, "alpha")));

        var exclusive = await project.Run(project.Root, "update", "ruleset-standard", "alpha",
            "--std-diff", "--skip-diff", "--skip-confirm");
        Assert.NotEqual(0, exclusive.ExitCode);
        Assert.Contains("RSUPD001", exclusive.StandardError);

        var standard = await project.Run(project.Root, "update", "ruleset-standard", "alpha",
            "--std-diff", "--skip-confirm");
        Assert.Equal(0, standard.ExitCode);
        Assert.Contains("second", standard.StandardOutput);
        Assert.Contains("second", File.ReadAllText(Rules(project, "alpha")));

        WriteRules(source, "alpha", "intrinsic.non-empty", "third");
        var noDiff = await project.Run(project.Root, "update", "ruleset-standard", "alpha",
            "--skip-diff", "--skip-confirm");
        Assert.Equal(0, noDiff.ExitCode);
        Assert.DoesNotContain("# Ruleset Standard update", noDiff.StandardOutput);
        Assert.Contains("third", File.ReadAllText(Rules(project, "alpha")));
    }

    [Fact]
    public async Task Invalid_update_preserves_existing_snapshot_and_other_standards_continue()
    {
        using var project = Project();
        var a = Path.Combine(project.Root, "source-a");
        var b = Path.Combine(project.Root, "source-b");
        WriteRules(a, "alpha", "intrinsic.non-empty", "first");
        WriteRules(b, "beta", "intrinsic.other", "before");
        await Install(project, a);
        await Install(project, b);
        File.WriteAllText(RulesInSource(a), "rules: [invalid]\n");
        WriteRules(b, "beta", "intrinsic.other", "after");
        var update = await project.Run(project.Root, "update", "ruleset-standard", "--skip-confirm");
        Assert.NotEqual(0, update.ExitCode);
        Assert.Contains("CSR013", update.StandardError);
        Assert.Contains("first", File.ReadAllText(Rules(project, "alpha")));
        Assert.Contains("after", File.ReadAllText(Rules(project, "beta")));
    }

    [Fact]
    public async Task Discovery_supports_qualified_nodes_and_rejects_ambiguous_short_names()
    {
        using var project = Project();
        var a = Path.Combine(project.Root, "source-a");
        var b = Path.Combine(project.Root, "source-b");
        WriteRules(a, "alpha", "intrinsic.non-empty");
        WriteRules(b, "beta", "intrinsic.non-empty");
        await Install(project, a);
        await Install(project, b);

        var ambiguous = await project.Run(project.Root, "discovery", "ruleset-standard",
            "--structure", "intrinsic.non-empty");
        Assert.NotEqual(0, ambiguous.ExitCode);
        Assert.Contains("RSDISC003", ambiguous.StandardError);

        var explicitSelection = await project.Run(project.Root, "discovery", "ruleset-standard",
            "--structure", "alpha.intrinsic.non-empty");
        Assert.Equal(0, explicitSelection.ExitCode);
        Assert.Contains("Structure: alpha.intrinsic.non-empty", explicitSelection.StandardOutput);
        Assert.Contains("renderer: expression", explicitSelection.StandardOutput);
        Assert.Contains("bindings: value", explicitSelection.StandardOutput);
    }

    [Fact]
    public async Task Lowering_combines_disjoint_standards_and_refuses_ambiguous_node_overrides()
    {
        using var project = Project();
        var a = Path.Combine(project.Root, "source-a");
        var b = Path.Combine(project.Root, "source-b");
        ToolingTestProject.WriteValidRuleset(a);
        WriteManifestName(a, "alpha");
        WriteRules(b, "beta", "intrinsic.new-unique");
        await Install(project, a);
        await Install(project, b);
        project.WriteStreetName();
        File.WriteAllText(Path.Combine(project.Root, "Test.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        var lowered = await project.Run(project.Root, "transpile", "StreetName.vsir", "--stdout");
        Assert.Equal(0, lowered.ExitCode);
        Assert.Contains("StreetName", lowered.StandardOutput);

        WriteRules(b, "beta", "intrinsic.non-empty");
        var changed = await project.Run(project.Root, "update", "ruleset-standard", "beta", "--skip-confirm");
        Assert.Equal(0, changed.ExitCode);
        var conflicting = await project.Run(project.Root, "transpile", "StreetName.vsir", "--stdout");
        Assert.NotEqual(0, conflicting.ExitCode);
        Assert.Contains("CSR020", conflicting.StandardError);
        Assert.Contains("alpha", conflicting.StandardError);
        Assert.Contains("beta", conflicting.StandardError);
    }

    [Fact]
    public async Task Project_extension_overlay_survives_ruleset_standard_update_and_delete()
    {
        using var project = Project();
        var source = Path.Combine(project.Root, "source");
        WriteRules(source, "alpha", "intrinsic.non-empty");
        await Install(project, source);
        Directory.CreateDirectory(project.ExtensionsRoot);
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "manifest.yaml"), "version: 0.1\ncatalogs: []\n");
        File.WriteAllText(Path.Combine(project.ExtensionsRoot, "keep.marker"), "project-owned");
        WriteRules(source, "alpha", "intrinsic.non-empty", "next");
        Assert.Equal(0, (await project.Run(project.Root,
            "update", "ruleset-standard", "alpha", "--skip-confirm")).ExitCode);
        Assert.Equal(0, (await project.Run(project.Root,
            "delete", "ruleset-standard", "alpha", "--skip-confirm")).ExitCode);
        Assert.True(File.Exists(Path.Combine(project.ExtensionsRoot, "keep.marker")));
        Assert.True(File.Exists(Path.Combine(project.ExtensionsRoot, "manifest.yaml")));
        Assert.True(File.Exists(RulesInSource(source)));
    }

    [Fact]
    public async Task Delete_refuses_to_destroy_original_local_source_when_vsir_may_depend_on_rules()
    {
        using var project = Project();
        var source = Path.Combine(project.Root, "source");
        WriteRules(source, "alpha", "intrinsic.non-empty");
        await Install(project, source);
        project.WriteStreetName();
        var blocked = await project.Run(project.Root,
            "delete", "ruleset-standard", "alpha", "--skip-confirm");
        Assert.NotEqual(0, blocked.ExitCode);
        Assert.Contains("RSDEL003", blocked.StandardError);
        Assert.True(Directory.Exists(Managed(project, "alpha")));
        File.Delete(Path.Combine(project.Root, "StreetName.vsir"));
        var allowed = await project.Run(project.Root,
            "delete", "ruleset-standard", "alpha", "--skip-confirm");
        Assert.Equal(0, allowed.ExitCode);
        Assert.True(File.Exists(RulesInSource(source)));
    }

    [Fact]
    public async Task Init_can_import_named_ruleset_standard_and_preserve_legacy_config()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "origin");
        WriteRules(source, "alpha", "intrinsic.non-empty");
        var init = await project.Run(project.Root, "init", "--ruleset-standard-origin", source);
        Assert.Equal(0, init.ExitCode);
        Assert.True(File.Exists(Path.Combine(Managed(project, "alpha"), "manifest.yaml")));
        var config = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.Null(config!.RulesetSource);
    }

    [Fact]
    public void Official_source_and_git_shorthand_use_renamed_repository()
    {
        Assert.Equal("https://github.com/vslices/ruleset-standard", ProjectConfiguration.OfficialRulesetSource);
        var resolved = ProjectOriginResolver.Parse("vslices/ruleset-standard:main", ProjectOrigin.OfficialRuleset, "TEST");
        Assert.True(resolved.IsSuccess);
        Assert.Equal(ProjectConfiguration.OfficialRulesetSource, resolved.Origin!.Source);
        Assert.Equal("main", resolved.Origin.Reference);
    }

    private static ToolingTestProject Project()
    {
        var project = new ToolingTestProject();
        project.WriteConfiguration();
        return project;
    }

    private static async Task Install(ToolingTestProject project, string origin)
    {
        var result = await project.Run(project.Root, "new", "ruleset-standard", "--origin", origin);
        Assert.True(result.ExitCode == 0, $"Installation failed: exit={result.ExitCode}\\nstdout={result.StandardOutput}\\nstderr={result.StandardError}");
    }

    private static string Managed(ToolingTestProject project, string name) =>
        Path.Combine(project.VslicesRoot, "rulesets", name);
    private static string Local(ToolingTestProject project, string name) =>
        Path.Combine(project.VslicesRoot, "rulesets", "local", name);
    private static string Rules(ToolingTestProject project, string name) =>
        Path.Combine(Managed(project, name), "csharp", "rules.yaml");
    private static string RulesInSource(string root) => Path.Combine(root, "csharp", "rules.yaml");

    private static void WriteRules(string root, string name, string node, string marker = "value")
    {
        Directory.CreateDirectory(Path.Combine(root, "csharp"));
        File.WriteAllText(Path.Combine(root, "manifest.yaml"),
            $"kind: vslices-ruleset\nversion: 0.1\nname: {name}\ntargets:\n  csharp:\n    rules:\n      - csharp/rules.yaml\n");
        File.WriteAllText(RulesInSource(root),
            $"rules:\n  - node: {node}\n    mode: deterministic\n    renderer: expression\n    bindings: [value]\n    template: \"{marker}{{value}}\"\n");
    }

    private static void WriteManifestName(string root, string name)
    {
        var path = Path.Combine(root, "manifest.yaml");
        File.WriteAllText(path, File.ReadAllText(path).TrimEnd() + Environment.NewLine + $"name: {name}\n");
    }
}
