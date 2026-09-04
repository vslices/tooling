using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Tooling;

internal static class RebaseOperation
{
    public static async Task<RebaseResult> ExecuteFromVsir(
        string subject,
        string? target,
        string previousVsir,
        string? source,
        string? namespaceOverride,
        CancellationToken cancellationToken)
    {
        var next = await TranspilationOperation.Execute(
            subject,
            target,
            namespaceOverride,
            cancellationToken);
        if (!next.IsSuccess)
            return RebaseResult.Failure(next.Diagnostics);

        var previous = await TranspilationOperation.Execute(
            previousVsir,
            CommandInfrastructure.DisplayTarget(next.Target!),
            namespaceOverride,
            cancellationToken);
        if (!previous.IsSuccess)
            return RebaseResult.Failure(previous.Diagnostics);

        var conventional = CommandInfrastructure.ConventionalMaterializationPath(
            next.VsirPath!,
            next.Target!);
        var humanPath = string.IsNullOrWhiteSpace(source)
            ? conventional
            : Path.GetFullPath(source, Environment.CurrentDirectory);

        return await ExecuteDeterministic(next, previous.Source!, humanPath, cancellationToken);
    }

    public static async Task<RebaseResult> ExecuteDeterministic(
        TranspilationResult next,
        string previousDeterministic,
        string humanPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(humanPath))
        {
            return RebaseResult.Failure([new(
                "CLI003",
                $"Could not resolve human projection '{humanPath}'.")]);
        }

        var human = await File.ReadAllTextAsync(humanPath, cancellationToken);
        var rebased = CSharpRebaser.Rebase(previousDeterministic, human, next.Source!);

        return rebased.IsSuccess
            ? RebaseResult.Success(rebased.Source!, humanPath, next.Source!)
            : RebaseResult.Failure(rebased.Diagnostics);
    }
}

internal sealed record RebaseResult(
    string? Source,
    string? SourcePath,
    string? DeterministicSource,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess =>
        Source is not null &&
        SourcePath is not null &&
        DeterministicSource is not null &&
        Diagnostics.Count == 0;

    public static RebaseResult Success(
        string source,
        string sourcePath,
        string deterministicSource) =>
        new(source, sourcePath, deterministicSource, []);

    public static RebaseResult Failure(IEnumerable<VsirDiagnostic> diagnostics) =>
        new(null, null, null, diagnostics.ToArray());
}
