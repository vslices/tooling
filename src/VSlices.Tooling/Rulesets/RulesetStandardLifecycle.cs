namespace VSlices.Tooling;

/// <summary>Named Ruleset Standard lifecycle: project-owned originals and independently refreshed snapshots.</summary>
internal static class RulesetStandardLifecycle
{
    public static async Task<int> New(
        VSlicesProjectContext project, string? name, string? origin, CancellationToken cancellationToken)
    {
        var registry = RulesetStandardRegistry.Load(project);
        if (origin is null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                if (!CanPrompt)
                {
                    TerminalOutput.Error("RSNEW001: Specify a Ruleset Standard name in noninteractive mode.");
                    return 2;
                }
                Console.Write("New Ruleset Standard name: ");
                name = Console.ReadLine()?.Trim();
            }
            if (!RulesetStandardRegistry.IsValidName(name))
            {
                TerminalOutput.Error("RSNEW002: Invalid Ruleset Standard name. Use ASCII letters, digits, '-' or '_', starting with a letter.");
                return 2;
            }
            if (!Available(registry, name!)) return 2;
            var destination = registry.NewPath(name!, local: true);
            if (Directory.Exists(destination) || File.Exists(destination))
            {
                TerminalOutput.Error($"RSNEW003: '{destination}' already exists; no changes made.");
                return 2;
            }
            try
            {
                Directory.CreateDirectory(destination);
                await File.WriteAllTextAsync(Path.Combine(destination, "manifest.yaml"),
                    $"kind: vslices-ruleset\nversion: 0.1\nname: {name}\ntargets: {{}}\n", cancellationToken);
                if (RulesetStandardManifest.ReadName(destination) != name)
                    throw new InvalidDataException("RSNEW004: Generated standard identity is invalid.");
                registry.Add(new RulesetStandardInstallation(name!, name!, true, null, null));
                await registry.SaveAsync(cancellationToken);
                Console.WriteLine($"Created editable local Ruleset Standard '{name}' at '{destination}'.");
                return 0;
            }
            catch
            {
                Cleanup(destination);
                throw;
            }
        }

        var resolved = ProjectOriginResolver.Parse(origin, ProjectOrigin.OfficialRuleset, "RSNEW005");
        if (!resolved.IsSuccess)
        {
            TerminalOutput.Error(resolved.Error!);
            return 2;
        }
        var source = resolved.Origin!.Source;
        var reference = resolved.Origin.Reference;
        if (!RulesetSourceMaterializer.IsRemoteSource(source))
        {
            source = Path.GetFullPath(source, project.ProjectRoot);
            if (!Directory.Exists(source))
            {
                TerminalOutput.Warning($"RSNEW006: Source '{source}' does not exist. No Ruleset Standard installed.");
                return 0;
            }
        }

        var staging = Path.Combine(Path.GetTempPath(), "vslices-rs-new-" + Guid.NewGuid().ToString("N"));
        var prepared = Path.Combine(registry.Root, ".prepared-" + Guid.NewGuid().ToString("N"));
        try
        {
            var materialized = await RulesetSourceMaterializer.Materialize(
                new RulesetSource(source, reference), staging, cancellationToken);
            if (!materialized.IsSuccess)
            {
                TerminalOutput.Error($"{materialized.DiagnosticCode}: {materialized.Message}");
                return 1;
            }

            var declared = RulesetStandardManifest.ReadName(materialized.Root!);
            var identity = string.IsNullOrWhiteSpace(name) ? declared : name.Trim();
            if (!RulesetStandardRegistry.IsValidName(identity))
            {
                TerminalOutput.Error("RSNEW007: Source manifest lacks a valid name; supply an explicit [name].");
                return 2;
            }
            if (!Available(registry, identity!)) return 2;
            var destination = registry.NewPath(identity!, local: false);
            if (Directory.Exists(destination) || File.Exists(destination))
            {
                TerminalOutput.Error($"RSNEW008: Target '{destination}' already exists; no changes made.");
                return 2;
            }

            var target = CommandInfrastructure.NormalizeTarget(project.Configuration.DefaultTarget ?? "csharp");
            var validated = RulesetSnapshotInstaller.Prepare(materialized.Root!, target, prepared);
            if (!validated.IsSuccess)
            {
                CommandInfrastructure.WriteDiagnostics(validated.Diagnostics);
                return 2;
            }
            registry.MigrateLegacy();
            Directory.Move(prepared, destination);
            try
            {
                registry.Add(new RulesetStandardInstallation(identity!, declared ?? identity!, false, source, reference));
                await registry.SaveAsync(cancellationToken);
            }
            catch
            {
                Cleanup(destination);
                throw;
            }
            Console.WriteLine($"Installed Ruleset Standard '{identity}' from '{source}'.");
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
            TerminalOutput.Error("RSUPD001: --std-diff and --skip-diff are mutually exclusive.");
            return 2;
        }
        if (name is null && origin is not null)
        {
            TerminalOutput.Error("RSUPD002: --origin requires a named installation.");
            return 2;
        }

        var registry = RulesetStandardRegistry.Load(project);
        if (registry.Installations.Count == 0)
        {
            Console.WriteLine("No Ruleset Standards installed. Use 'vslices new ruleset-standard'.");
            return 0;
        }

        RulesetStandardInstallation[] selected;
        if (name is null)
        {
            foreach (var local in registry.Installations.Where(i => i.IsLocal))
                Console.WriteLine($"Skipping local Ruleset Standard '{local.Name}' (editable source).");
            selected = registry.Installations.Where(i => !i.IsLocal).ToArray();
        }
        else
        {
            var match = registry.Find(name);
            if (match is null)
            {
                TerminalOutput.Error($"RSUPD003: Ruleset Standard '{name}' is not installed.");
                return 2;
            }
            if (match.IsLocal)
            {
                Console.WriteLine($"Ruleset Standard '{match.Name}' is local; edit its source at '{registry.PathFor(match)}'.");
                return 0;
            }
            selected = [match];
        }
        if (selected.Length == 0)
        {
            Console.WriteLine("No imported Ruleset Standards to update.");
            return 0;
        }

        var pending = new List<PreparedUpdate>();
        var failures = false;
        try
        {
            foreach (var installed in selected)
            {
                var overrideSource = origin is null ? null : ProjectOriginResolver.Parse(
                    origin, ProjectOrigin.OfficialRuleset, "RSUPD004");
                if (overrideSource is { IsSuccess: false })
                {
                    TerminalOutput.Error(overrideSource.Error!);
                    failures = true;
                    continue;
                }
                var proposal = overrideSource?.Origin ??
                    new ProjectOrigin(installed.Source ?? string.Empty, installed.Reference);
                if (string.IsNullOrWhiteSpace(proposal.Source))
                {
                    TerminalOutput.Error($"RSUPD005: '{installed.Name}' has no recorded source. Provide --origin.");
                    failures = true;
                    continue;
                }
                var source = RulesetSourceMaterializer.IsRemoteSource(proposal.Source)
                    ? proposal.Source : Path.GetFullPath(proposal.Source, project.ProjectRoot);
                var staging = Path.Combine(Path.GetTempPath(), "vslices-rs-upd-" + Guid.NewGuid().ToString("N"));
                var prepared = Path.Combine(registry.Root, ".prepared-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(registry.Root);
                try
                {
                    var materialized = await RulesetSourceMaterializer.Materialize(
                        new RulesetSource(source, proposal.Reference), staging, cancellationToken);
                    if (!materialized.IsSuccess)
                    {
                        TerminalOutput.Error($"{installed.Name}: {materialized.DiagnosticCode}: {materialized.Message}");
                        failures = true;
                        continue;
                    }
                    var target = CommandInfrastructure.NormalizeTarget(project.Configuration.DefaultTarget ?? "csharp");
                    var valid = RulesetSnapshotInstaller.Prepare(materialized.Root!, target, prepared);
                    if (!valid.IsSuccess)
                    {
                        CommandInfrastructure.WriteDiagnostics(valid.Diagnostics);
                        failures = true;
                        continue;
                    }
                    var report = skipDiff ? null : RulesetStandardDiff.Create(
                        installed.Name, registry.PathFor(installed), prepared, source);
                    if (stdDiff && report is not null) Console.WriteLine(report);
                    else if (report is not null)
                    {
                        var reports = Path.Combine(project.VslicesRoot, "reports", "ruleset-standard-updates");
                        Directory.CreateDirectory(reports);
                        var reportPath = Path.Combine(reports,
                            installed.Name + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") +
                            "-" + Guid.NewGuid().ToString("N")[..8] + ".md");
                        await File.WriteAllTextAsync(reportPath, report, cancellationToken);
                        Console.WriteLine($"Review report for '{installed.Name}': {reportPath}");
                    }
                    var declared = RulesetStandardManifest.ReadName(materialized.Root!) ?? installed.DeclaredName;
                    pending.Add(new PreparedUpdate(installed, source, proposal.Reference, declared, prepared));
                }
                finally
                {
                    Cleanup(staging);
                    if (!pending.Any(item => item.Prepared == prepared)) Cleanup(prepared);
                }
            }

            if (pending.Count == 0) return failures ? 1 : 0;
            Console.WriteLine("Ruleset Standards to update: " + string.Join(", ", pending.Select(i => i.Installation.Name)));
            if (!Approve(skipConfirm, "Apply Ruleset Standard updates? [y/N]: "))
            {
                Console.WriteLine("Update cancelled; installed snapshots are unchanged.");
                return !skipConfirm && !CanPrompt ? 2 : failures ? 1 : 0;
            }

            foreach (var candidate in pending)
            {
                try
                {
                    registry.MigrateLegacy();
                    var existing = registry.Find(candidate.Installation.Name)!;
                    var destination = registry.PathFor(existing);
                    var backup = Path.Combine(registry.Root, ".backup-" + Guid.NewGuid().ToString("N"));
                    Directory.Move(destination, backup);
                    try
                    {
                        Directory.Move(candidate.Prepared, destination);
                        registry.Remove(existing.Name);
                        registry.Add(existing with
                        {
                            Source = candidate.Source,
                            Reference = candidate.Reference,
                            DeclaredName = candidate.DeclaredName,
                            Legacy = false
                        });
                        await registry.SaveAsync(cancellationToken);
                        Cleanup(backup);
                        Console.WriteLine($"Updated Ruleset Standard '{existing.Name}'.");
                    }
                    catch
                    {
                        Cleanup(destination);
                        Directory.Move(backup, destination);
                        registry.Remove(existing.Name);
                        registry.Add(existing);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    TerminalOutput.Error($"RSUPD006: {candidate.Installation.Name}: {ex.Message}");
                    failures = true;
                }
            }
            return failures ? 1 : 0;
        }
        finally
        {
            foreach (var item in pending) Cleanup(item.Prepared);
        }
    }

    public static int List(VSlicesProjectContext project)
    {
        var registry = RulesetStandardRegistry.Load(project);
        if (registry.Installations.Count == 0)
        {
            Console.WriteLine("No Ruleset Standards installed.");
            return 0;
        }
        Console.WriteLine("Ruleset Standards:");
        foreach (var entry in registry.Installations.OrderBy(i => i.Name, StringComparer.Ordinal))
            Console.WriteLine($"  {entry.Name} [{(entry.IsLocal ? "local" : "managed")}] " +
                $"declared={entry.DeclaredName} source={(entry.IsLocal ? registry.PathFor(entry) : entry.Source ?? "<legacy>")}" +
                (entry.Reference is null ? "" : $" ref={entry.Reference}"));
        return 0;
    }

    public static async Task<int> Delete(
        VSlicesProjectContext project, string? name, bool skipConfirm, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TerminalOutput.Error("RSDEL001: Specify a Ruleset Standard installation name.");
            return 2;
        }
        var registry = RulesetStandardRegistry.Load(project);
        var installed = registry.Find(name);
        if (installed is null)
        {
            TerminalOutput.Error($"RSDEL002: Ruleset Standard '{name}' is not installed.");
            return 2;
        }
        if (registry.HasTarget("csharp") && Directory.Exists(Path.Combine(registry.PathFor(installed), "csharp"))
            && HasVsirArtifacts(project.ProjectRoot))
        {
            TerminalOutput.Error("RSDEL003: Project has VSIR artifacts that may depend on this Ruleset Standard. " +
                "Deletion is blocked until dependency impact can be determined.");
            return 2;
        }

        Console.WriteLine(installed.IsLocal
            ? $"WARNING: Deleting '{installed.Name}' destroys its ORIGINAL editable local definitions."
            : $"Deleting '{installed.Name}' removes only its managed snapshot, not the source repository or directory.");
        if (!Approve(skipConfirm, "Delete this Ruleset Standard? [y/N]: "))
        {
            Console.WriteLine("Deletion cancelled.");
            return !skipConfirm && !CanPrompt ? 2 : 0;
        }
        registry.MigrateLegacy();
        installed = registry.Find(name)!;
        var destination = registry.PathFor(installed);
        var backup = Path.Combine(registry.Root, ".delete-" + Guid.NewGuid().ToString("N"));
        Directory.Move(destination, backup);
        try
        {
            registry.Remove(installed.Name);
            await registry.SaveAsync(cancellationToken);
            Cleanup(backup);
            Console.WriteLine($"Deleted Ruleset Standard '{installed.Name}'.");
            return 0;
        }
        catch
        {
            Directory.Move(backup, destination);
            throw;
        }
    }

    private static bool Available(RulesetStandardRegistry registry, string name)
    {
        if (registry.Find(name) is null) return true;
        TerminalOutput.Error($"RSNEW009: Ruleset Standard '{name}' already exists. " +
            "Specify a different [name]. No changes were made.");
        return false;
    }

    private static bool CanPrompt => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    private static bool Approve(bool skipConfirm, string question)
    {
        if (skipConfirm) return true;
        if (!CanPrompt)
        {
            TerminalOutput.Error("RSCONF001: Noninteractive use requires --skip-confirm.");
            return false;
        }
        Console.Write(question);
        return string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasVsirArtifacts(string projectRoot)
    {
        var queue = new Stack<string>();
        queue.Push(projectRoot);
        while (queue.Count > 0)
        {
            var directory = queue.Pop();
            if (Directory.EnumerateFiles(directory, "*.vsir").Any()) return true;
            foreach (var child in Directory.EnumerateDirectories(directory))
                if (Path.GetFileName(child) is not (".git" or ".vslices" or "obj" or "bin"))
                    queue.Push(child);
        }
        return false;
    }

    private static void Cleanup(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private sealed record PreparedUpdate(
        RulesetStandardInstallation Installation, string Source, string? Reference,
        string DeclaredName, string Prepared);
}
