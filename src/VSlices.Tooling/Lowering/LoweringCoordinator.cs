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
        var semanticMove = SemanticNamespaceMovePreflight.TryDetect(
            humanBefore,
            rebased.Source!);

        if (semanticMove is not null && next.TargetContext?.ProjectPath is not null)
        {
            if (stdout || output == "-" || !string.IsNullOrWhiteSpace(output))
            {
                Console.Error.WriteLine(
                    "DOTNET035: A namespace move requires a transactional semantic refactoring over the real project files. --stdout and redirected --output are not supported for this operation.");
                return 1;
            }

            ShowSemanticAnalysisPreflight(semanticMove);
            if (!SemanticRefactoringAuthorization.ConfirmAnalysis(Console.In, Console.Out))
            {
                TerminalOutput.BlankLine();
                TerminalOutput.Muted("Semantic analysis was not started. No files were modified. Lowering lineage was not advanced.");
                return 1;
            }
        }

        using var semanticPlan = await DotNetSemanticRefactoringClient.TryPlanNamespaceMove(
            next,
            rebased.SourcePath!,
            humanBefore,
            rebased.Source!,
            cancellationToken);

        if (semanticPlan is not null)
        {
            if (!semanticPlan.IsSuccess)
            {
                CommandInfrastructure.WriteDiagnostics(semanticPlan.Diagnostics);
                return 1;
            }

            ShowBlastRadius(project, semanticPlan);

            if (semanticPlan.RequiresAuthorization &&
                !SemanticRefactoringAuthorization.Confirm(Console.In, Console.Out))
            {
                TerminalOutput.BlankLine();
                TerminalOutput.Muted("No files were modified. Lowering lineage was not advanced.");
                return 1;
            }

            var baselinePath = LoweringLineageStore.ResolveBaselinePath(
                project,
                rebased.SourcePath!,
                next.Target!);
            if (baselinePath is null || semanticPlan.TransactionRoot is null)
            {
                Console.Error.WriteLine(
                    "LOWER002: Could not establish the lineage destination for the semantic refactoring transaction. No files were modified.");
                return 1;
            }

            var stagedBaseline = Path.Combine(
                semanticPlan.TransactionRoot,
                "next-deterministic.baseline");
            await File.WriteAllTextAsync(
                stagedBaseline,
                rebased.DeterministicSource!,
                cancellationToken);

            var transaction = semanticPlan.Files
                .Select(x => new TransactionalFileChange(
                    x.Path,
                    x.StagedPath,
                    ExpectedExists: true,
                    x.OriginalSha256))
                .Append(new TransactionalFileChange(
                    baselinePath,
                    stagedBaseline,
                    File.Exists(baselinePath),
                    TransactionalFileWriter.TrySha256(baselinePath)))
                .ToArray();

            var applied = await TransactionalFileWriter.Apply(
                transaction,
                cancellationToken);
            if (!applied.Success)
            {
                Console.Error.WriteLine($"DOTNET036: {applied.Error}");
                return 1;
            }

            TerminalOutput.BlankLine();
            TerminalOutput.Success(
                $"✓ Semantic refactoring applied transactionally across {semanticPlan.Files.Count} file(s)");
            TerminalOutput.Success("✓ Lowering lineage advanced");
            return 0;
        }

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

    private static void ShowSemanticAnalysisPreflight(
        SemanticNamespaceMoveCandidate move)
    {
        TerminalOutput.BlankLine();
        TerminalOutput.Info("Semantic namespace change detected");
        TerminalOutput.Detail("From", move.PreviousNamespace);
        TerminalOutput.Detail("To", move.NextNamespace);
        TerminalOutput.BlankLine();
        TerminalOutput.Muted(
            "Discovering the blast radius requires loading the related .NET solution with Roslyn and may take some time.");
        TerminalOutput.Muted(
            "This analysis does not modify files; applying any discovered human-code refactoring requires a separate authorization.");
    }

    private static void ShowBlastRadius(
        VSlicesProjectContext project,
        DotNetSemanticRefactoringPlan plan)
    {
        TerminalOutput.BlankLine();
        TerminalOutput.Info("Semantic namespace refactoring");
        TerminalOutput.Detail("Symbol", $"{plan.PreviousSymbol} -> {plan.NextSymbol}");
        TerminalOutput.Detail("References", plan.ReferenceCount.ToString());
        TerminalOutput.Detail("Files", plan.Files.Count.ToString());
        TerminalOutput.BlankLine();

        foreach (var file in plan.Files)
        {
            var display = project.Contains(file.Path)
                ? Path.GetRelativePath(project.ProjectRoot, file.Path)
                : file.Path;
            var referenceText = file.ReferenceCount == 1
                ? "1 semantic reference"
                : $"{file.ReferenceCount} semantic references";
            TerminalOutput.Muted($"  {display} ({referenceText})");
        }

        if (plan.RequiresAuthorization)
        {
            TerminalOutput.BlankLine();
            TerminalOutput.Info("This operation will modify human-maintained code outside the deterministic rebase region.");
        }
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
