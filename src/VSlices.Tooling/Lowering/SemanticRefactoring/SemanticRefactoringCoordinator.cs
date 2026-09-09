namespace VSlices.Tooling;

internal static class SemanticRefactoringCoordinator
{
    public static async Task<SemanticRefactoringOutcome> TryExecute(
        VSlicesProjectContext project,
        TranspilationResult next,
        RebaseResult rebased,
        string humanBefore,
        string? output,
        bool stdout,
        CancellationToken cancellationToken)
    {
        var semanticMove = SemanticNamespaceMovePreflight.TryDetect(
            humanBefore,
            rebased.Source!);

        if (semanticMove is not null && next.TargetContext?.ProjectPath is not null)
        {
            if (stdout || output == "-" || !string.IsNullOrWhiteSpace(output))
            {
                Console.Error.WriteLine(
                    "DOTNET035: A namespace move requires a transactional semantic refactoring over the real project files. --stdout and redirected --output are not supported for this operation.");
                return SemanticRefactoringOutcome.HandledWith(1);
            }

            ShowSemanticAnalysisPreflight(semanticMove);
            if (!SemanticRefactoringAuthorization.ConfirmAnalysis(Console.In, Console.Out))
            {
                TerminalOutput.BlankLine();
                TerminalOutput.Muted("Semantic analysis was not started. No files were modified. Lowering lineage was not advanced.");
                return SemanticRefactoringOutcome.HandledWith(1);
            }
        }

        using var semanticPlan = await DotNetSemanticRefactoringClient.TryPlanNamespaceMove(
            next,
            rebased.SourcePath!,
            humanBefore,
            rebased.Source!,
            cancellationToken);

        if (semanticPlan is null)
            return SemanticRefactoringOutcome.NotHandled;

        if (!semanticPlan.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(semanticPlan.Diagnostics);
            return SemanticRefactoringOutcome.HandledWith(1);
        }

        ShowBlastRadius(project, semanticPlan);

        if (semanticPlan.RequiresAuthorization &&
            !SemanticRefactoringAuthorization.Confirm(Console.In, Console.Out))
        {
            TerminalOutput.BlankLine();
            TerminalOutput.Muted("No files were modified. Lowering lineage was not advanced.");
            return SemanticRefactoringOutcome.HandledWith(1);
        }

        var baselinePath = LoweringLineageStore.ResolveBaselinePath(
            project,
            rebased.SourcePath!,
            next.Target!);
        if (baselinePath is null || semanticPlan.TransactionRoot is null)
        {
            Console.Error.WriteLine(
                "LOWER002: Could not establish the lineage destination for the semantic refactoring transaction. No files were modified.");
            return SemanticRefactoringOutcome.HandledWith(1);
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
            return SemanticRefactoringOutcome.HandledWith(1);
        }

        TerminalOutput.BlankLine();
        TerminalOutput.Success(
            $"✓ Semantic refactoring applied transactionally across {semanticPlan.Files.Count} file(s)");
        TerminalOutput.Success("✓ Lowering lineage advanced");
        return SemanticRefactoringOutcome.HandledWith(0);
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
}

internal sealed record SemanticRefactoringOutcome(bool Handled, int ExitCode)
{
    public static SemanticRefactoringOutcome NotHandled { get; } = new(false, 0);

    public static SemanticRefactoringOutcome HandledWith(int exitCode) =>
        new(true, exitCode);
}
