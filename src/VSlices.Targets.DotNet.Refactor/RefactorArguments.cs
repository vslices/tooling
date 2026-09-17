namespace VSlices.Targets.DotNet.Refactor;

internal sealed record RefactorArguments(
    string ProjectPath,
    string DocumentPath,
    string CandidatePath,
    string SymbolName,
    string StagingPath,
    string ManifestPath)
{
    public static RefactorArguments? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                return null;
            values[args[index][2..]] = args[++index];
        }

        return Try("project", out var project) &&
               Try("document", out var document) &&
               Try("candidate", out var candidate) &&
               Try("symbol", out var symbol) &&
               Try("staging", out var staging) &&
               Try("manifest", out var manifest)
            ? new(
                Path.GetFullPath(project!),
                Path.GetFullPath(document!),
                Path.GetFullPath(candidate!),
                symbol!,
                Path.GetFullPath(staging!),
                Path.GetFullPath(manifest!))
            : null;

        bool Try(string key, out string? value) =>
            values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
    }
}
