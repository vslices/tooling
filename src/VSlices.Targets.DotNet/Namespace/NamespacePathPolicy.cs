using System.IO.Enumeration;

namespace VSlices.Targets.DotNet;

internal static class NamespacePathPolicy
{
    public static string[] Apply(
        IReadOnlyList<string> relativeSegments,
        IReadOnlyCollection<string>? ignoredFolders)
    {
        var patterns = ignoredFolders?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizePattern)
            .ToArray() ?? [];

        if (patterns.Length == 0)
            return relativeSegments.ToArray();

        return relativeSegments
            .Where((segment, index) => !ShouldIgnoreSegment(relativeSegments, index, segment, patterns))
            .ToArray();
    }

    private static bool ShouldIgnoreSegment(
        IReadOnlyList<string> relativeSegments,
        int segmentIndex,
        string segment,
        IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (!pattern.Contains('/'))
            {
                if (MatchesSimplePattern(pattern, segment))
                    return true;

                continue;
            }

            var patternSegments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (patternSegments.Length == 0 || patternSegments[^1] == "**")
                continue;

            var candidatePath = relativeSegments.Take(segmentIndex + 1).ToArray();
            if (MatchesPathPattern(patternSegments, candidatePath))
                return true;
        }

        return false;
    }

    private static bool MatchesPathPattern(
        IReadOnlyList<string> patternSegments,
        IReadOnlyList<string> pathSegments)
    {
        var memo = new Dictionary<(int PatternIndex, int PathIndex), bool>();

        bool Match(int patternIndex, int pathIndex)
        {
            var key = (patternIndex, pathIndex);
            if (memo.TryGetValue(key, out var cached))
                return cached;

            bool result;
            if (patternIndex == patternSegments.Count)
            {
                result = pathIndex == pathSegments.Count;
            }
            else if (patternSegments[patternIndex] == "**")
            {
                result = Match(patternIndex + 1, pathIndex) ||
                         pathIndex < pathSegments.Count && Match(patternIndex, pathIndex + 1);
            }
            else
            {
                result = pathIndex < pathSegments.Count &&
                         MatchesSimplePattern(patternSegments[patternIndex], pathSegments[pathIndex]) &&
                         Match(patternIndex + 1, pathIndex + 1);
            }

            memo[key] = result;
            return result;
        }

        return Match(0, 0);
    }

    private static bool MatchesSimplePattern(string pattern, string value) =>
        pattern.IndexOfAny(['*', '?']) < 0
            ? value.Equals(pattern, StringComparison.Ordinal)
            : FileSystemName.MatchesSimpleExpression(
                pattern,
                value,
                ignoreCase: false);

    private static string NormalizePattern(string pattern) =>
        pattern.Trim().Replace('\\', '/').Trim('/');
}
