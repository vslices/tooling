namespace VSlices.Tooling.Tests;

public sealed class DocsStandardUpdateTests
{
    [Fact]
    public async Task Project_configuration_round_trips_legacy_docs_standard_source_and_ref()
    {
        using var project = new ToolingTestProject();
        var configuration = ProjectConfiguration.Default() with
        {
            DocsStandardSource = "https://github.com/vslices/docs-standard",
            DocsStandardRef = "feat/document-authoring-preview"
        };
        await ProjectConfiguration.WriteAsync(project.Root, configuration, CancellationToken.None);
        var loaded = ProjectConfiguration.LoadFromProjectRoot(project.Root);
        Assert.NotNull(loaded);
        Assert.Equal(configuration.DocsStandardSource, loaded!.DocsStandardSource);
        Assert.Equal(configuration.DocsStandardRef, loaded.DocsStandardRef);
    }

    [Fact]
    public async Task Local_new_creates_editable_named_standard_and_is_discoverable()
    {
        using var project = NewProject();
        var created = await project.Run(project.Root, "new", "docs-standard", "serviu");
        Assert.Equal(0, created.ExitCode);
        var root = Local(project, "serviu");
        Assert.Contains("name: serviu", File.ReadAllText(Path.Combine(root, "manifest.yaml")), StringComparison.Ordinal);
        Assert.Contains("serviu [local]", (await project.Run(project.Root, "list", "docs-standards")).StandardOutput);
        var found = await project.Run(project.Root, "discovery", "docs-standard", "--docs-standard", "serviu");
        Assert.Equal(0, found.ExitCode);
        Assert.Contains("No manifest-promoted structures", found.StandardOutput);
    }

    [Fact]
    public async Task Local_new_without_name_requires_name_in_noninteractive_context()
    {
        using var project = NewProject();
        var result = await project.Run(project.Root, "new", "docs-standard");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("STDNEW001", result.StandardError);
    }

    [Fact]
    public async Task Imported_source_uses_manifest_name_or_alias_without_modifying_source()
    {
        using var project = NewProject();
        var source = Path.Combine(project.Root, "upstream");
        WriteStandard(source, "vendor", "Original question");
        File.WriteAllText(Path.Combine(source, "not-declared.txt"), "unrelated");

        Assert.Equal(0, (await project.Run(project.Root,
            "new", "docs-standard", "--origin", source)).ExitCode);
        Assert.Equal(0, (await project.Run(project.Root,
            "new", "docs-standard", "custom", "--origin", source)).ExitCode);
        Assert.True(File.Exists(Path.Combine(Managed(project, "vendor"), "manifest.yaml")));
        Assert.True(File.Exists(Path.Combine(Managed(project, "custom"), "manifest.yaml")));
        Assert.False(File.Exists(Path.Combine(Managed(project, "vendor"), "not-declared.txt")));
        Assert.Contains("name: vendor", File.ReadAllText(Path.Combine(source, "manifest.yaml")));
        var list = await project.Run(project.Root, "list", "docs-standards");
        Assert.Contains("vendor [managed]", list.StandardOutput);
        Assert.Contains("custom [managed]", list.StandardOutput);
    }

    [Fact]
    public async Task Collisions_between_local_and_managed_standards_reject_without_overwriting()
    {
        using var project = NewProject();
        Assert.Equal(0, (await project.Run(project.Root, "new", "docs-standard", "serviu")).ExitCode);
        var original = File.ReadAllText(Path.Combine(Local(project, "serviu"), "manifest.yaml"));
        var source = Path.Combine(project.Root, "upstream");
        WriteStandard(source, "serviu", "Original question");
        var collided = await project.Run(project.Root, "new", "docs-standard", "--origin", source);
        Assert.NotEqual(0, collided.ExitCode);
        Assert.Contains("Specify a different [name]", collided.StandardError);
        Assert.Equal(original, File.ReadAllText(Path.Combine(Local(project, "serviu"), "manifest.yaml")));
        Assert.False(Directory.Exists(Managed(project, "serviu")));
        Assert.Equal(0, (await project.Run(project.Root, "new", "docs-standard", "serviu-remote", "--origin", source)).ExitCode);
    }

    [Fact]
    public async Task Missing_physical_origin_warns_without_creating_installation()
    {
        using var project = NewProject();
        var result = await project.Run(project.Root, "new", "docs-standard",
            "--origin", Path.Combine(project.Root, "does-not-exist"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("STDNEW005", result.StandardOutput + result.StandardError);
        var list = await project.Run(project.Root, "list", "docs-standards");
        Assert.Contains("No Docs Standards installed", list.StandardOutput);
    }

    [Fact]
    public async Task Existing_invalid_origin_fails_without_mutating_registry()
    {
        using var project = NewProject();
        var source = Path.Combine(project.Root, "invalid");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "manifest.yaml"),
            "kind: vslices-docs-standard\nversion: 0.1\nname: invalid\ndocuments: [documents/missing.yml]\n");
        var result = await project.Run(project.Root, "new", "docs-standard", "--origin", source);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCS009", result.StandardError);
        Assert.False(Directory.Exists(Managed(project, "invalid")));
    }

    [Fact]
    public async Task Update_all_refreshes_external_snapshots_preserves_local_and_creates_markdown_reports()
    {
        using var project = NewProject();
        var a = Path.Combine(project.Root, "source-a");
        var b = Path.Combine(project.Root, "source-b");
        WriteStandard(a, "alpha", "First question");
        WriteStandard(b, "beta", "Initial question");
        await Install(project, a);
        await Install(project, b);
        Assert.Equal(0, (await project.Run(project.Root, "new", "docs-standard", "serviu")).ExitCode);
        var localBefore = File.ReadAllBytes(Path.Combine(Local(project, "serviu"), "manifest.yaml"));

        WriteStandard(a, "alpha", "Revised question");
        WriteStandard(b, "beta", "Updated question");
        var result = await project.Run(project.Root, "update", "docs-standard", "--skip-confirm");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Skipping local Docs Standard 'serviu'", result.StandardOutput);
        Assert.Contains("Revised question", Definition(project, "alpha"));
        Assert.Contains("Updated question", Definition(project, "beta"));
        Assert.Equal(localBefore, File.ReadAllBytes(Path.Combine(Local(project, "serviu"), "manifest.yaml")));
        var reportRoot = Path.Combine(project.VslicesRoot, "reports", "docs-standard-updates");
        Assert.Equal(2, Directory.GetFiles(reportRoot, "*.md").Length);
        Assert.Contains("Revised question", File.ReadAllText(Directory.GetFiles(reportRoot, "alpha-*.md")[0]));
    }

    [Fact]
    public async Task Named_update_has_independent_diff_and_confirmation_controls()
    {
        using var project = NewProject();
        var source = Path.Combine(project.Root, "source");
        WriteStandard(source, "alpha", "First question");
        await Install(project, source);
        WriteStandard(source, "alpha", "Next question");

        var confirmation = await project.Run(project.Root, "update", "docs-standard", "alpha");
        Assert.NotEqual(0, confirmation.ExitCode);
        Assert.Contains("STDCONF001", confirmation.StandardError);
        Assert.Contains("First question", Definition(project, "alpha"));

        var incompatibleFlags = await project.Run(project.Root, "update", "docs-standard", "alpha",
            "--std-diff", "--skip-diff", "--skip-confirm");
        Assert.NotEqual(0, incompatibleFlags.ExitCode);
        Assert.Contains("STDUPD001", incompatibleFlags.StandardError);

        var changed = await project.Run(project.Root, "update", "docs-standard", "alpha",
            "--std-diff", "--skip-confirm");
        Assert.Equal(0, changed.ExitCode);
        Assert.Contains("Next question", changed.StandardOutput);
        Assert.Contains("First question", changed.StandardOutput);
        Assert.Contains("Next question", Definition(project, "alpha"));

        WriteStandard(source, "alpha", "Third question");
        var noDiff = await project.Run(project.Root, "update", "docs-standard", "alpha",
            "--skip-diff", "--skip-confirm");
        Assert.Equal(0, noDiff.ExitCode);
        Assert.DoesNotContain("# Docs Standard update", noDiff.StandardOutput);
        Assert.Contains("Third question", Definition(project, "alpha"));
    }

    [Fact]
    public async Task Failed_update_preserves_snapshot_and_origin_and_updates_others_independently()
    {
        using var project = NewProject();
        var a = Path.Combine(project.Root, "a");
        var b = Path.Combine(project.Root, "b");
        WriteStandard(a, "alpha", "Original A");
        WriteStandard(b, "beta", "Original B");
        await Install(project, a);
        await Install(project, b);
        WriteStandard(b, "beta", "New B");
        File.WriteAllText(Path.Combine(a, "documents", "context.yml"),
            "kind: vslices-document-definition\nversion: 0.1\ndocument:\n  type: context\n  scopes: not-a-sequence\n  question:\n    id: context\n    text: Broken\n");

        var result = await project.Run(project.Root, "update", "docs-standard", "--skip-confirm");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DOCS018", result.StandardError);
        Assert.Contains("Original A", Definition(project, "alpha"));
        Assert.Contains("New B", Definition(project, "beta"));
        var registry = File.ReadAllText(Path.Combine(project.VslicesRoot, "docs-standards", "registry.yaml"));
        Assert.Contains(a, registry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discovery_resolves_unique_names_and_rejects_ambiguous_ones()
    {
        using var project = NewProject();
        var a = Path.Combine(project.Root, "source-a");
        var b = Path.Combine(project.Root, "source-b");
        WriteStandard(a, "alpha", "Alpha question", includeOtherFamilies: true);
        WriteStandard(b, "beta", "Beta question");
        await Install(project, a);
        await Install(project, b);
        var ambiguous = await project.Run(project.Root,
            "discovery", "docs-standard", "--structure", "context");
        Assert.NotEqual(0, ambiguous.ExitCode);
        Assert.Contains("STDDISC006", ambiguous.StandardError);
        var explicitName = await project.Run(project.Root,
            "discovery", "docs-standard", "--structure", "alpha.context");
        Assert.Equal(0, explicitName.ExitCode);
        Assert.Contains("Alpha question", explicitName.StandardOutput);
        var uniqueNexus = await project.Run(project.Root,
            "discovery", "docs-standard", "--structure", "capability");
        Assert.Equal(0, uniqueNexus.ExitCode);
        Assert.Contains("compose independently", uniqueNexus.StandardOutput);
        var path = await project.Run(project.Root,
            "discovery", "docs-standard", "--docs-standard", "alpha", "--structure", "business-driver");
        Assert.Equal(0, path.ExitCode);
        Assert.Contains("recommended traversal", path.StandardOutput);
        var note = await project.Run(project.Root,
            "discovery", "docs-standard", "--structure", "draft");
        Assert.Equal(0, note.ExitCode);
        Assert.Contains("Sketch question", note.StandardOutput);
    }

    [Fact]
    public async Task Delete_requires_confirmation_and_preserves_external_sources()
    {
        using var project = NewProject();
        var source = Path.Combine(project.Root, "source");
        WriteStandard(source, "alpha", "Original");
        await Install(project, source);
        var denied = await project.Run(project.Root, "delete", "docs-standard", "alpha");
        Assert.NotEqual(0, denied.ExitCode);
        Assert.True(Directory.Exists(Managed(project, "alpha")));
        var result = await project.Run(project.Root, "delete", "docs-standard", "alpha", "--skip-confirm");
        Assert.Equal(0, result.ExitCode);
        Assert.False(Directory.Exists(Managed(project, "alpha")));
        Assert.True(Directory.Exists(source));
        Assert.Equal(0, (await project.Run(project.Root, "new", "docs-standard", "serviu")).ExitCode);
        var local = await project.Run(project.Root, "delete", "docs-standard", "serviu", "--skip-confirm");
        Assert.Equal(0, local.ExitCode);
        Assert.Contains("ORIGINAL", local.StandardOutput);
        Assert.False(Directory.Exists(Local(project, "serviu")));
    }

    [Fact]
    public async Task Deletion_rejects_active_qualified_references()
    {
        using var project = NewProject();
        var source = Path.Combine(project.Root, "source");
        WriteStandard(source, "alpha", "Original");
        await Install(project, source);
        File.WriteAllText(Path.Combine(project.Root, "reference.md"),
            "---\nartifact:\n  kind: document\n  type: alpha.context\n---\n");
        var blocked = await project.Run(project.Root,
            "delete", "docs-standard", "alpha", "--skip-confirm");
        Assert.NotEqual(0, blocked.ExitCode);
        Assert.Contains("STDDEL003", blocked.StandardError);
        Assert.True(Directory.Exists(Managed(project, "alpha")));
    }

    [Fact]
    public async Task Existing_legacy_snapshot_migrates_without_losing_data_when_new_standard_is_created()
    {
        using var project = NewProject();
        var legacy = Path.Combine(project.VslicesRoot, "docs-standard");
        WriteStandard(legacy, "legacy-vocabulary", "Preserve original question");
        Assert.Equal(0, (await project.Run(project.Root, "new", "docs-standard", "serviu")).ExitCode);
        // Local creation does not mutate a legacy installation; it remains recognized.
        var list = await project.Run(project.Root, "list", "docs-standards");
        Assert.Contains("legacy-vocabulary", list.StandardOutput);
        Assert.True(File.Exists(Path.Combine(legacy, "manifest.yaml")));
    }

    [Fact]
    public async Task Updating_uninstalled_standard_does_not_implicitly_install_anything()
    {
        using var project = NewProject();
        var result = await project.Run(project.Root, "update", "docs-standard");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No Docs Standards installed", result.StandardOutput);
    }

    [Fact]
    public void First_install_prompt_preserves_existing_document_policy()
    {
        using var project = NewProject();
        var output = new StringWriter();
        var configuration = DocumentPolicyBootstrap.PromptMissing(
            ProjectConfiguration.Default() with
            {
                DocumentsRoute = "existing-docs",
                DocumentsTemplate = "markdown.existing"
            }, project.Root, new StringReader(string.Empty), output);
        Assert.Equal("existing-docs", configuration.DocumentsRoute);
        Assert.Equal("markdown.existing", configuration.DocumentsTemplate);
    }

    private static ToolingTestProject NewProject()
    {
        var project = new ToolingTestProject();
        project.WriteConfiguration();
        return project;
    }

    private static string Managed(ToolingTestProject project, string name) =>
        Path.Combine(project.VslicesRoot, "docs-standards", name);

    private static string Local(ToolingTestProject project, string name) =>
        Path.Combine(project.VslicesRoot, "docs-standards", "local", name);

    private static string Definition(ToolingTestProject project, string name) =>
        File.ReadAllText(Path.Combine(Managed(project, name), "documents", "context.yml"));

    private static async Task Install(ToolingTestProject project, string source)
    {
        var result = await project.Run(project.Root, "new", "docs-standard", "--origin", source);
        Assert.Equal(0, result.ExitCode);
    }

    private static void WriteStandard(string root, string name, string question, bool includeOtherFamilies = false)
    {
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        var extraManifest = includeOtherFamilies
            ? "support-notes:\n  - support-notes/draft.yml\nnexus:\n  - nexus/capability.yml\ncontinuity-paths:\n  - continuity-paths/business-driver.yml\n"
            : "";
        File.WriteAllText(Path.Combine(root, "manifest.yaml"),
            $"kind: vslices-docs-standard\nversion: 0.1\nname: {name}\ndocuments:\n  - documents/context.yml\n{extraManifest}");
        File.WriteAllText(Path.Combine(root, "documents", "context.yml"),
            $"kind: vslices-document-definition\nversion: 0.1\ndocument:\n  type: context\n  question:\n    id: context\n    text: {question}\n");
        if (!includeOtherFamilies) return;
        Directory.CreateDirectory(Path.Combine(root, "support-notes"));
        Directory.CreateDirectory(Path.Combine(root, "nexus"));
        Directory.CreateDirectory(Path.Combine(root, "continuity-paths"));
        File.WriteAllText(Path.Combine(root, "support-notes", "draft.yml"),
            "kind: vslices-support-note-definition\nversion: 0.1\nsupport-note:\n  type: draft\n  question:\n    id: draft\n    text: Sketch question\n");
        File.WriteAllText(Path.Combine(root, "nexus", "capability.yml"),
            "kind: vslices-nexus-definition\nversion: 0.1\nnexus:\n  type: capability\n  recommendations:\n    - document: context\n      role: Describes context\n");
        File.WriteAllText(Path.Combine(root, "continuity-paths", "business-driver.yml"),
            "kind: vslices-continuity-path-definition\nversion: 0.1\ncontinuity-path:\n  type: business-driver\n  purpose: Preserve drivers\n  recommended-traversal: Begin with drivers\n  question:\n    id: driver\n    text: What originated it?\n");
    }
}
