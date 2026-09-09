namespace VSlices.Tooling;

internal sealed record SemanticNamespaceMoveCandidate(
    string PreviousNamespace,
    string NextNamespace);

internal static class SemanticNamespaceMovePreflight
{
    public static SemanticNamespaceMoveCandidate? TryDetect(
        string humanSource,
        string candidateSource)
    {
        if (!TryExtractFileScopedNamespace(humanSource, out var previousNamespace) ||
            !TryExtractFileScopedNamespace(candidateSource, out var nextNamespace) ||
            string.Equals(previousNamespace, nextNamespace, StringComparison.Ordinal))
        {
            return null;
        }

        return new(previousNamespace, nextNamespace);
    }

    private static bool TryExtractFileScopedNamespace(
        string source,
        out string namespaceName)
    {
        using var reader = new StringReader(source);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("namespace ", StringComparison.Ordinal) ||
                !trimmed.EndsWith(';'))
            {
                continue;
            }

            namespaceName = trimmed["namespace ".Length..^1].Trim();
            return namespaceName.Length > 0;
        }

        namespaceName = string.Empty;
        return false;
    }
}
