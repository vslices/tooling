using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Tooling;

internal static class LoweringCoordinator
{
    private enum ArtifactOutcome
    {
        Lowered,
        Unsupported,
        Failed
    }

    private sealed record ArtifactExecution(int ExitCode, ArtifactOutcome Outcome)
    {
        public static ArtifactExecution Lowered() => new(0, ArtifactOutcome.Lowered);
        public static ArtifactExecution Unsupported(int exitCode = 1) => new(exitCode, ArtifactOutcome.Unsupported);
        public static ArtifactExecution Failed(int exitCode = 1) => new(exitCode, ArtifactOutcome.Failed);
    }

    public static async Task<int> Execute(
        string subject,
        string? target,
        string? from,
        string? source,
        string? output,
        bool stdout,
        string? namespaceOverride,
        CSharpRebaseResolution resolution,
        DiagnosticVerbosity diagnosticVerbosity,
        CancellationToken cancellationToken)
    {
        var resolved = LoweringSubjectResolver.Resolve(subject, Environment.CurrentDirectory);
        if (resolved.Diagnostic is not null)
        {
            CommandInfrastructure.WriteDiagnostics([resolved.Diagnostic], diagnosticVerbosity);
            return 1;
        }

        if (resolved.Subject!.Kind == LoweringSubjectKind.DotNetProject)
        {
            return await ExecuteProject(
                resolved.Subject.Path,
                target,
                from,
                source,
                output,
                stdout,
                namespaceOverride,
                resolution,
                diagnosticVerbosity,
                cancellationToken);
        }

        var execution = await ExecuteArtifact(
            resolved.Subject.Path,
            target,
            from,
            source,
            output,
            stdout,
            namespaceOverride,
            resolution,
            diagnosticVerbosity,
            cancellationToken);
        return execution.ExitCode;
    }

    private static async Task<int> ExecuteProject(
        string projectPath,
        string? target,
        string? from,
        string? source,
        string? output,
        bool stdout,
        string? namespaceOverride,
        CSharpRebaseResolution resolution,
        DiagnosticVerbosity diagnosticVerbosity,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(from) ||
            !string.IsNullOrWhiteSpace(source) ||
            !string.IsNullOrWhiteSpace(output) ||
            stdout ||
            !string.IsNullOrWhiteSpace(namespaceOverride))
        {
            Console.Error.WriteLine(
                "CLI006: Project lowering does not yet support --from, --source, --output, --stdout, or --namespace. Lower an individual .vsir when an artifact-specific override is required.");
            return 2;
        }

        var prepared = TranspilationOperation.Prepare(projectPath, target);
        if (!prepared.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(prepared.Diagnostics, diagnosticVerbosity);
            return 1;
        }

        var artifacts = LoweringSubjectResolver.EnumerateProjectVsirFiles(projectPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (artifacts.Length == 0)
        {
            Console.WriteLine($"No VSIR artifacts found in project '{Path.GetFileNameWithoutExtension(projectPath)}'.");
            return 0;
        }

        var succeeded = 0;
        var unsupported = 0;
        var failed = 0;

        Console.WriteLine($"Lowering project '{Path.GetFileNameWithoutExtension(projectPath)}' ({artifacts.Length} VSIR artifacts)...");

        foreach (var artifact in artifacts)
        {
            var relative = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, artifact);
            var execution = await ExecuteArtifact(
                artifact,
                target,
                from: null,
                source: null,
                output: null,
                stdout: false,
                namespaceOverride: null,
                resolution,
                diagnosticVerbosity,
                cancellationToken,
                prepared.Environment);

            switch (execution.Outcome)
            {
                case ArtifactOutcome.Lowered:
                    succeeded++;
                    Console.WriteLine($"  ✓ {relative}");
                    break;
                case ArtifactOutcome.Unsupported:
                    unsupported++;
                    Console.WriteLine($"  - {relative} (not lowered: unsupported semantic/target surface)");
                    break;
                case ArtifactOutcome.Failed:
                    failed++;
                    Console.WriteLine($"  ! {relative} (lowering failed)");
                    break;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Project lowering: {succeeded} succeeded, {unsupported} unsupported, {failed} failures.");

        return failed > 0 ? 1 : 0;
    }

    private static async Task<ArtifactExecution> ExecuteArtifact(
        string subject,
        string? target,
        string? from,
        string? source,
        string? output,
        bool stdout,
        string? namespaceOverride,
        CSharpRebaseResolution resolution,
        DiagnosticVerbosity diagnosticVerbosity,
        CancellationToken cancellationToken,
        TranspilationEnvironment? environment = null)
    {
        var next = environment is null
            ? await TranspilationOperation.Execute(subject, target, namespaceOverride, cancellationToken)
            : await TranspilationOperation.ExecuteResolved(subject, environment, namespaceOverride, cancellationToken);
        if (!next.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(next.Diagnostics, diagnosticVerbosity);
            return FromDiagnostics(next.Diagnostics);
        }

        var project = next.Project!;
        var conventional = CommandInfrastructure.ConventionalMaterializationPath(
            next.VsirPath!,
            next.Target!);
        var existing = string.IsNullOrWhiteSpace(source)
            ? conventional
            : Path.GetFullPath(source, Environment.CurrentDirectory);

        if (!File.Exists(existing))
        {
            var exitCode = await CommandInfrastructure.WriteResult(
                next.Source!,
                conventional,
                output,
                stdout,
                overwrite: false,
                cancellationToken);

            if (exitCode == 0 && TryResolveWrittenPath(conventional, output, stdout, out var writtenPath))
            {
                await LoweringLineageStore.TryWrite(
                    project,
                    writtenPath!,
                    next.Target!,
                    next.Source!,
                    cancellationToken);
            }

            return exitCode == 0 ? ArtifactExecution.Lowered() : ArtifactExecution.Failed(exitCode);
        }

        RebaseResult rebased;
        if (!string.IsNullOrWhiteSpace(from))
        {
            rebased = await RebaseOperation.ExecuteFromVsir(
                subject,
                CommandInfrastructure.DisplayTarget(next.Target!),
                from,
                existing,
                namespaceOverride,
                resolution,
                cancellationToken);
        }
        else
        {
            var previousDeterministic = await LoweringLineageStore.TryRead(
                project,
                existing,
                next.Target!,
                cancellationToken);

            if (previousDeterministic is null)
            {
                var human = await File.ReadAllTextAsync(existing, cancellationToken);
                if (string.Equals(human, next.Source, StringComparison.Ordinal))
                {
                    await LoweringLineageStore.TryWrite(
                        project,
                        existing,
                        next.Target!,
                        next.Source!,
                        cancellationToken);

                    return await CompleteBootstrapOutput(
                        human,
                        existing,
                        output,
                        stdout,
                        exactDeterministic: true,
                        cancellationToken);
                }

                if (!LoweringLineageBootstrap.IsConfiguredFor(
                        project,
                        conventional,
                        existing,
                        source))
                {
                    Console.Error.WriteLine(
                        "LOWER001: No trustworthy deterministic baseline could be inferred. Configure lineage.bootstrap.convention for the conventional materialization, or run once with --from <previous-vsir> to establish lineage explicitly.");
                    return ArtifactExecution.Failed();
                }

                await LoweringLineageStore.TryWrite(
                    project,
                    existing,
                    next.Target!,
                    next.Source!,
                    cancellationToken);

                return await CompleteBootstrapOutput(
                    human,
                    existing,
                    output,
                    stdout,
                    exactDeterministic: false,
                    cancellationToken);
            }

            rebased = await RebaseOperation.ExecuteDeterministic(
                next,
                previousDeterministic,
                existing,
                resolution,
                cancellationToken);
        }

        if (!rebased.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(rebased.Diagnostics, diagnosticVerbosity);
            return ArtifactExecution.Failed();
        }

        var humanBefore = await File.ReadAllTextAsync(rebased.SourcePath!, cancellationToken);
        var semantic = await SemanticRefactoringCoordinator.TryExecute(
            project,
            next,
            rebased,
            humanBefore,
            output,
            stdout,
            cancellationToken);
        if (semantic.Handled)
            return semantic.ExitCode == 0 ? ArtifactExecution.Lowered() : ArtifactExecution.Failed(semantic.ExitCode);

        var writeExitCode = await CommandInfrastructure.WriteResult(
            rebased.Source!,
            rebased.SourcePath!,
            output,
            stdout,
            overwrite: true,
            cancellationToken);

        if (writeExitCode == 0 &&
            TryResolveWrittenPath(rebased.SourcePath!, output, stdout, out var rebasedPath))
        {
            await LoweringLineageStore.TryWrite(
                project,
                rebasedPath!,
                next.Target!,
                rebased.DeterministicSource!,
                cancellationToken);
        }

        return writeExitCode == 0 ? ArtifactExecution.Lowered() : ArtifactExecution.Failed(writeExitCode);
    }

    private static ArtifactExecution FromDiagnostics(IReadOnlyList<VsirDiagnostic> diagnostics)
    {
        // Project lowering intentionally continues past forms/semantic target
        // capabilities the current release does not lower. Environment, toolchain,
        // IO and orchestration failures are different: they must make the project
        // invocation fail even though subsequent artifacts may still be attempted.
        var unsupported = diagnostics.Count > 0 && diagnostics.All(diagnostic =>
            diagnostic.Code.StartsWith("VSIR", StringComparison.Ordinal) ||
            diagnostic.Code.StartsWith("CSL", StringComparison.Ordinal) ||
            diagnostic.Code == "CLI020" ||
            diagnostic.Code == "CLI024");

        return unsupported ? ArtifactExecution.Unsupported() : ArtifactExecution.Failed();
    }

    private static async Task<ArtifactExecution> CompleteBootstrapOutput(
        string materialization,
        string existing,
        string? output,
        bool stdout,
        bool exactDeterministic,
        CancellationToken cancellationToken)
    {
        var writesToStdout = CommandInfrastructure.IsStdoutDestination(output, stdout);
        if (writesToStdout)
        {
            Console.Error.WriteLine(exactDeterministic
                ? $"Established lowering lineage for '{existing}'."
                : $"Lineage bootstrap: {ProjectConfiguration.DefaultLineageBootstrapConvention}. Existing materialization preserved.");
        }
        else if (exactDeterministic)
        {
            Console.WriteLine($"Established lowering lineage for '{existing}'.");
        }
        else
        {
            TerminalOutput.Detail(
                "Lineage bootstrap",
                ProjectConfiguration.DefaultLineageBootstrapConvention);
            TerminalOutput.Success("✓ Lowering lineage established without modifying the existing materialization");
        }

        // Bootstrap establishes operational ancestry even in stdout mode, but the
        // requested content destination is still honored. The returned content is
        // the human materialization that bootstrap deliberately preserved.
        if (!writesToStdout && string.IsNullOrWhiteSpace(output))
            return ArtifactExecution.Lowered();

        var exitCode = await CommandInfrastructure.WriteResult(
            materialization,
            existing,
            output,
            stdout,
            overwrite: true,
            cancellationToken);
        return exitCode == 0 ? ArtifactExecution.Lowered() : ArtifactExecution.Failed(exitCode);
    }

    private static bool TryResolveWrittenPath(
        string defaultPath,
        string? output,
        bool stdout,
        out string? resolved)
    {
        if (CommandInfrastructure.IsStdoutDestination(output, stdout))
        {
            resolved = null;
            return false;
        }

        resolved = string.IsNullOrWhiteSpace(output)
            ? defaultPath
            : Path.GetFullPath(output, Environment.CurrentDirectory);
        return true;
    }
}
