using Microsoft.CodeAnalysis;

namespace VSlices.Targets.DotNet.Refactor;

internal static class CompilationValidator
{
    public static async Task<bool> ValidateBaseline(
        Solution solution,
        IReadOnlyCollection<ProjectId> projectIds,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        foreach (var projectId in projectIds)
        {
            var project = solution.GetProject(projectId);
            if (project is null)
            {
                await RefactorManifest.WriteFailure(
                    manifestPath,
                    "DOTNET033",
                    $"Semantic refactoring cannot be validated transactionally because affected project '{projectId}' is unavailable.");
                return false;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                await RefactorManifest.WriteFailure(
                    manifestPath,
                    "DOTNET033",
                    $"Semantic refactoring cannot be validated transactionally because Roslyn could not produce a baseline compilation for '{project.Name}'.");
                return false;
            }

            var errors = compilation.GetDiagnostics(cancellationToken)
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .Take(5)
                .ToArray();
            if (errors.Length == 0)
                continue;

            await RefactorManifest.WriteFailure(
                manifestPath,
                "DOTNET033",
                "Semantic refactoring cannot be validated transactionally because an affected project already has compiler errors: " +
                string.Join(" | ", errors.Select(FormatDiagnostic)));
            return false;
        }

        return true;
    }

    public static async Task<bool> ValidateProposal(
        Solution solution,
        IReadOnlyCollection<ProjectId> projectIds,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        foreach (var projectId in projectIds)
        {
            var project = solution.GetProject(projectId);
            if (project is null)
            {
                await RefactorManifest.WriteFailure(
                    manifestPath,
                    "DOTNET034",
                    $"The proposed namespace refactoring cannot be validated because affected project '{projectId}' is unavailable. No files were modified.");
                return false;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                await RefactorManifest.WriteFailure(
                    manifestPath,
                    "DOTNET034",
                    $"The proposed namespace refactoring cannot be validated because Roslyn could not produce a compilation for '{project.Name}'. No files were modified.");
                return false;
            }

            var errors = compilation.GetDiagnostics(cancellationToken)
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .Take(10)
                .ToArray();
            if (errors.Length == 0)
                continue;

            await RefactorManifest.WriteFailure(
                manifestPath,
                "DOTNET034",
                "The proposed namespace refactoring does not compile. No files were modified: " +
                string.Join(" | ", errors.Select(FormatDiagnostic)));
            return false;
        }

        return true;
    }

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location.IsInSource
            ? $"{diagnostic.Location.GetLineSpan().Path}:{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}"
            : "<project>";
        return $"{diagnostic.Id} {location} {diagnostic.GetMessage()}";
    }
}
