namespace VSlices.Tooling;

/// <summary>Operational lifecycle of Docs Standard installations, independent of documentary definition semantics.</summary>
internal static class DocsStandardLifecycle
{
    public static async Task<int> New(
        VSlicesProjectContext project, string? name, string? origin, CancellationToken cancellationToken)
    {
        var registry = DocsStandardRegistry.Load(project);
        if (origin is null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                if (!CanPrompt)
                {
                    TerminalOutput.Error("STDNEW001: Supply a name when creating a local Docs Standard noninteractively.");
                    return 2;
                }
                Console.Write("New Docs Standard name: ");
                name = Console.ReadLine()?.Trim();
            }
            if (!DocsStandardRegistry.IsValidName(name))
            {
                TerminalOutput.Error("STDNEW002: Name must start with a letter and contain only ASCII letters, digits, '-' or '_'; 'local' is reserved.");
                return 2;
            }
            if (!CheckAvailable(registry, name!)) return 2;

            var root = registry.NewPath(name!, local: true);
            if (Directory.Exists(root) || File.Exists(root))
            {
                TerminalOutput.Error($"STDNEW003: '{root}' already exists. No changes were made.");
                return 2;
            }
            var existed = registry.Installations.Count > 0;
            try
            {
                Directory.CreateDirectory(root);
                await File.WriteAllTextAsync(Path.Combine(root, "manifest.yaml"),
                    $"kind: vslices-docs-standard\nversion: 0.1\nname: {name}\ndocuments: []\nsupport-notes: []\nnexus: []\ncontinuity-paths: []\n", cancellationToken);
                var check = DocsStandardSnapshotInstaller.Prepare(root,
                    Path.Combine(registry.Root, ".validate-" + Guid.NewGuid().ToString("N")));
                if (!check.IsSuccess)
                    throw new InvalidDataException(check.Error);
                registry.Add(new DocsStandardInstallation(name!, name!, true, null, null));
                await registry.SaveAsync(cancellationToken);
                if (!existed) await BootstrapPolicy(project, cancellationToken);
                Console.WriteLine($"Created local Docs Standard '{name}' at '{root}'.");
                return 0;
            }
            catch
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                throw;
            }
            finally
            {
                foreach (var directory in Directory.Exists(registry.Root)
                    ? Directory.EnumerateDirectories(registry.Root, ".validate-*").ToArray()
                    : [])
                    Directory.Delete(directory, recursive: true);
            }
        }

        var resolved = ProjectOriginResolver.Parse(origin, ProjectOrigin.OfficialDocsStandard, "STDNEW004");
        if (!resolved.IsSuccess)
        {
            TerminalOutput.Error(resolved.Error!);
            return 2;
        }
        var source = resolved.Origin!.Source;
        var reference = resolved.Origin.Reference;
        if (!DocsStandardSourceMaterializer.IsRemoteSource(source))
        {
            source = Path.GetFullPath(source, project.ProjectRoot);
            if (!Directory.Exists(source))
            {
                TerminalOutput.Warning($"STDNEW005: Source directory '{source}' does not exist. No Docs Standard was installed.");
                return 0;
            }
        }

        var staging = Path.Combine(Path.GetTempPath(), "vslices-docs-new-" + Guid.NewGuid().ToString("N"));
        var prepared = Path.Combine(registry.Root, ".prepared-" + Guid.NewGuid().ToString("N"));
        try
        {
            var materialized = await DocsStandardSourceMaterializer.Materialize(
                new DocsStandardSource(source, reference), staging, cancellationToken);
            if (!materialized.IsSuccess)
            {
                TerminalOutput.Error($"{materialized.DiagnosticCode}: {materialized.Message}");
                return 1;
            }

            var declaredName = DocsStandardManifest.ReadName(materialized.Root!);
            var installationName = string.IsNullOrWhiteSpace(name) ? declaredName : name.Trim();
            if (!DocsStandardRegistry.IsValidName(installationName))
            {
                TerminalOutput.Error("STDNEW006: Origin has no valid manifest.name. Provide an explicit installation name or fix the manifest.");
                return 2;
            }
            if (!CheckAvailable(registry, installationName!)) return 2;

            var target = registry.NewPath(installationName!, local: false);
            if (Directory.Exists(target) || File.Exists(target))
            {
                TerminalOutput.Error($"STDNEW007: '{target}' already exists. No files were replaced.");
                return 2;
            }
            var validated = DocsStandardSnapshotInstaller.Prepare(materialized.Root!, prepared);
            if (!validated.IsSuccess)
            {
                TerminalOutput.Error(validated.Error!);
                return 2;
            }

            registry.MigrateLegacy();
            // Installation is staged and validated before it is made visible.
            Directory.Move(prepared, target);
            try
            {
                registry.Add(new DocsStandardInstallation(
                    installationName!, declaredName ?? installationName!, false, source, reference));
                await registry.SaveAsync(cancellationToken);
            }
            catch
            {
                Directory.Delete(target, recursive: true);
                throw;
            }
            if (registry.Installations.Count == 1) await BootstrapPolicy(project, cancellationToken);
            Console.WriteLine($"Installed Docs Standard '{installationName}' from '{source}'.");
            return 0;
        }
        finally
        {
            Cleanup(staging);
            Cleanup(prepared);
        }
    }

    public static async Task<int> Update(
        VSlicesProjectContext project, string? name, string? origin, bool skipConfirm,
        bool stdDiff, bool skipDiff, CancellationToken cancellationToken)
    {
        if (stdDiff && skipDiff)
        {
            TerminalOutput.Error("STDUPD001: --std-diff and --skip-diff cannot be combined.");
            return 2;
        }
        if (name is null && origin is not null)
        {
            TerminalOutput.Error("STDUPD002: --origin requires the installation name to avoid replacing every source.");
            return 2;
        }

        var registry = DocsStandardRegistry.Load(project);
        if (registry.Installations.Count == 0)
        {
            Console.WriteLine("No Docs Standards installed. Use 'vslices new docs-standard'.");
            return 0;
        }

        DocsStandardInstallation[] targets;
        if (name is null)
        {
            foreach (var local in registry.Installations.Where(i => i.IsLocal))
                Console.WriteLine($"Skipping local Docs Standard '{local.Name}' (editable source).");
            targets = registry.Installations.Where(i => !i.IsLocal).ToArray();
        }
        else
        {
            var selected = registry.Find(name);
            if (selected is null)
            {
                TerminalOutput.Error($"STDUPD003: Docs Standard '{name}' is not installed.");
                return 2;
            }
            if (selected.IsLocal)
            {
                Console.WriteLine($"Docs Standard '{selected.Name}' is local; edit its definitions in '{registry.PathFor(selected)}'.");
                return 0;
            }
            targets = [selected];
        }

        if (targets.Length == 0)
        {
            Console.WriteLine("No externally acquired Docs Standards to update.");
            return 0;
        }

        var pending = new List<PendingUpdate>();
        var failed = false;
        try
        {
            foreach (var installed in targets)
            {
                var parsed = origin is null ? null :
                    ProjectOriginResolver.Parse(origin, ProjectOrigin.OfficialDocsStandard, "STDUPD004");
                if (parsed is { IsSuccess: false })
                {
                    TerminalOutput.Error(parsed.Error!);
                    failed = true;
                    continue;
                }
                var selected = parsed?.Origin ??
                    new ProjectOrigin(installed.Source ?? string.Empty, installed.Reference);
                if (selected is null || string.IsNullOrWhiteSpace(selected.Source))
                {
                    TerminalOutput.Error($"STDUPD005: '{installed.Name}' has no recorded origin; choose --origin.");
                    failed = true;
                    continue;
                }
                var source = DocsStandardSourceMaterializer.IsRemoteSource(selected.Source)
                    ? selected.Source : Path.GetFullPath(selected.Source, project.ProjectRoot);
                var staging = Path.Combine(Path.GetTempPath(), "vslices-docs-upd-" + Guid.NewGuid().ToString("N"));
                var prepared = Path.Combine(registry.Root, ".prepared-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(registry.Root);
                try
                {
                    var materialized = await DocsStandardSourceMaterializer.Materialize(
                        new DocsStandardSource(source, selected.Reference), staging, cancellationToken);
                    if (!materialized.IsSuccess)
                    {
                        TerminalOutput.Error($"{installed.Name}: {materialized.DiagnosticCode}: {materialized.Message}");
                        failed = true;
                        continue;
                    }
                    var validated = DocsStandardSnapshotInstaller.Prepare(materialized.Root!, prepared);
                    if (!validated.IsSuccess)
                    {
                        TerminalOutput.Error($"{installed.Name}: {validated.Error}");
                        failed = true;
                        continue;
                    }
                    var report = skipDiff ? null : DocsStandardDiff.Create(
                        installed.Name, registry.PathFor(installed), prepared, source);
                    if (stdDiff && report is not null)
                        Console.WriteLine(report);
                    else if (report is not null)
                    {
                        var reportRoot = Path.Combine(project.VslicesRoot, "reports", "docs-standard-updates");
                        Directory.CreateDirectory(reportRoot);
                        var reportPath = Path.Combine(reportRoot,
                            installed.Name + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") +
                            "-" + Guid.NewGuid().ToString("N")[..8] + ".md");
                        await File.WriteAllTextAsync(reportPath, report, cancellationToken);
                        Console.WriteLine($"Review report for '{installed.Name}': {reportPath}");
                    }
                    pending.Add(new PendingUpdate(installed, source, selected.Reference,
                        DocsStandardManifest.ReadName(materialized.Root!) ?? installed.DeclaredName, prepared));
                }
                finally
                {
                    Cleanup(staging);
                    if (!pending.Any(item => item.Prepared == prepared)) Cleanup(prepared);
                }
            }

            if (pending.Count == 0) return failed ? 1 : 0;
            Console.WriteLine("Docs Standards to update: " +
                string.Join(", ", pending.Select(item => item.Installation.Name)));
            if (!Approve(skipConfirm, "Apply these Docs Standard updates? [y/N]: "))
            {
                Console.WriteLine("Update cancelled; installed snapshots were not modified.");
                return !skipConfirm && !CanPrompt ? 2 : failed ? 1 : 0;
            }
            foreach (var item in pending)
            {
                try
                {
                    registry.MigrateLegacy();
                    var current = registry.Find(item.Installation.Name)!;
                    var target = registry.PathFor(current);
                    var backup = Path.Combine(registry.Root, ".backup-" + Guid.NewGuid().ToString("N"));
                    Directory.Move(target, backup);
                    try
                    {
                        Directory.Move(item.Prepared, target);
                        registry.Remove(current.Name);
                        registry.Add(current with
                        {
                            Source = item.Source,
                            Reference = item.Reference,
                            DeclaredName = item.DeclaredName,
                            Legacy = false
                        });
                        await registry.SaveAsync(cancellationToken);
                        Cleanup(backup);
                        Console.WriteLine($"Updated Docs Standard '{current.Name}'.");
                    }
                    catch
                    {
                        Cleanup(target);
                        Directory.Move(backup, target);
                        registry.Remove(current.Name);
                        registry.Add(current);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    TerminalOutput.Error($"STDUPD006: '{item.Installation.Name}': {ex.Message}");
                    failed = true;
                }
            }
            return failed ? 1 : 0;
        }
        finally
        {
            foreach (var item in pending) Cleanup(item.Prepared);
        }
    }

    public static async Task<int> Delete(
        VSlicesProjectContext project, string? name, bool skipConfirm, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TerminalOutput.Error("STDDEL001: Specify the Docs Standard name to delete.");
            return 2;
        }
        var registry = DocsStandardRegistry.Load(project);
        var selected = registry.Find(name);
        if (selected is null)
        {
            TerminalOutput.Error($"STDDEL002: Docs Standard '{name}' is not installed.");
            return 2;
        }
        if (HasActiveReferences(project.ProjectRoot, selected.Name))
        {
            TerminalOutput.Error($"STDDEL003: Authored artifacts refer to '{selected.Name}'. Deletion is blocked.");
            return 2;
        }
        Console.WriteLine(selected.IsLocal
            ? $"WARNING: Deleting '{selected.Name}' removes its ORIGINAL locally edited definitions."
            : $"Deleting '{selected.Name}' removes only its managed snapshot, not the original source.");
        if (!Approve(skipConfirm, "Delete this Docs Standard? [y/N]: "))
        {
            Console.WriteLine("Deletion cancelled.");
            return !skipConfirm && !CanPrompt ? 2 : 0;
        }
        registry.MigrateLegacy();
        selected = registry.Find(name)!;
        var path = registry.PathFor(selected);
        var backup = Path.Combine(registry.Root, ".delete-" + Guid.NewGuid().ToString("N"));
        Directory.Move(path, backup);
        try
        {
            registry.Remove(selected.Name);
            await registry.SaveAsync(cancellationToken);
            Cleanup(backup);
            Console.WriteLine($"Deleted Docs Standard '{selected.Name}'.");
            return 0;
        }
        catch
        {
            Directory.Move(backup, path);
            throw;
        }
    }

    public static int List(VSlicesProjectContext project)
    {
        var registry = DocsStandardRegistry.Load(project);
        if (registry.Installations.Count == 0)
        {
            Console.WriteLine("No Docs Standards installed.");
            return 0;
        }
        Console.WriteLine("Docs Standards:");
        foreach (var item in registry.Installations.OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {item.Name} [{(item.IsLocal ? "local" : "managed")}] " +
                $"declared={item.DeclaredName} source={(item.IsLocal ? registry.PathFor(item) : item.Source ?? "<legacy>")}" +
                (item.Reference is null ? "" : $" ref={item.Reference}"));
        }
        return 0;
    }

    private static bool CheckAvailable(DocsStandardRegistry registry, string name)
    {
        if (registry.Find(name) is null) return true;
        TerminalOutput.Error(
            $"STDNEW008: Docs Standard '{name}' already exists. Specify a different [name]. No changes were made.");
        return false;
    }

    private static bool CanPrompt => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    private static bool Approve(bool skipConfirm, string question)
    {
        if (skipConfirm) return true;
        if (!CanPrompt)
        {
            TerminalOutput.Error("STDCONF001: Noninteractive use requires --skip-confirm.");
            return false;
        }
        Console.Write(question);
        return string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task BootstrapPolicy(VSlicesProjectContext project, CancellationToken cancellationToken)
    {
        var configuration = DocumentPolicyBootstrap.ConfigureFirstInstall(project.Configuration, project.ProjectRoot);
        await ProjectConfiguration.WriteAsync(project.ProjectRoot, configuration, cancellationToken);
    }

    private static bool HasActiveReferences(string projectRoot, string name)
    {
        var prefix = name + ".";
        var dirs = new Stack<string>();
        dirs.Push(projectRoot);
        while (dirs.Count > 0)
        {
            var current = dirs.Pop();
            foreach (var path in Directory.EnumerateDirectories(current))
            {
                var folder = Path.GetFileName(path);
                if (folder is ".git" or ".vslices" or "bin" or "obj") continue;
                dirs.Push(path);
            }
            foreach (var file in Directory.EnumerateFiles(current, "*.md"))
            {
                var front = File.ReadAllText(file);
                if (front.Contains("type: " + prefix, StringComparison.Ordinal) ||
                    front.Contains("type: \"" + prefix, StringComparison.Ordinal))
                    return true;
            }
        }
        return false;
    }

    private static void Cleanup(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private sealed record PendingUpdate(
        DocsStandardInstallation Installation, string Source, string? Reference,
        string DeclaredName, string Prepared);
}
