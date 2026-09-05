using VSlices.Vsir.CSharp;

namespace VSlices.Tooling;

internal static class LoweringCoordinator
{
    public static async Task<int> Execute(
        string subject,
        string? target,
        string? from,
        string? source,
        string? output,
        bool stdout,
        string? namespaceOverride,
        CSharpRebaseResolution resolution,
        CancellationToken cancellationToken)
    {
        var next = await TranspilationOperation.Execute(
            subject,
            target,
            namespaceOverride,
            cancellationToken);
        if (!next.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(next.Diagnostics);
            return 1;
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

            return exitCode;
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

                    Console.WriteLine($"Established lowering lineage for '{existing}'.");
                    return 0;
                }

                if (!LoweringLineageBootstrap.IsConfiguredFor(
                        project,
                        conventional,
                        existing,
                        source))
                {
                    Console.Error.WriteLine(
                        "LOWER001: No trustworthy deterministic baseline could be inferred. Configure lineage.bootstrap.convention for the conventional materialization, or run once with --from <previous-vsir> to establish lineage explicitly.");
                    return 1;
                }

                await LoweringLineageStore.TryWrite(
                    project,
                    existing,
                    next.Target!,
                    next.Source!,
                    cancellationToken);

                TerminalOutput.Detail(
                    "Lineage bootstrap",
                    ProjectConfiguration.DefaultLineageBootstrapConvention);
                TerminalOutput.Success("✓ Lowering lineage established without modifying the existing materialization");
                return 0;
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
            CommandInfrastructure.WriteDiagnostics(rebased.Diagnostics);
            return 1;
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
            return semantic.ExitCode;

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

        return writeExitCode;
    }

    private static bool TryResolveWrittenPath(
        string defaultPath,
        string? output,
        bool stdout,
        out string? resolved)
    {
        if (stdout || output == "-")
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
