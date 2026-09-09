using System.Text.RegularExpressions;

namespace VSlices.Tooling;

internal sealed class ArtifactDiscoveryPolicy
{
    private static readonly HashSet<string> BuiltInDirectoryExclusions = new(
        [".git", ".vslices", "bin", "obj"],
        StringComparer.OrdinalIgnoreCase);

    private readonly string _projectRoot;
    private readonly IReadOnlyList<IgnorePattern> _patterns;

    private ArtifactDiscoveryPolicy(string projectRoot, IReadOnlyList<IgnorePattern> patterns)
    {
        _projectRoot = projectRoot;
        _patterns = patterns;
    }

    public static ArtifactDiscoveryPolicy Load(string searchRoot)
    {
        var projectRoot = FindProjectRoot(searchRoot) ?? Path.GetFullPath(searchRoot);
        var ignorePath = Path.Combine(projectRoot, ".vslices", ".ignore");
        if (!File.Exists(ignorePath))
            return new(projectRoot, []);

        var patterns = File.ReadAllLines(ignorePath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(IgnorePattern.Parse)
            .ToArray();

        return new(projectRoot, patterns);
    }

    public bool IgnoreDirectory(string directory)
    {
        var name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (BuiltInDirectoryExclusions.Contains(name))
            return true;

        return MatchesProjectPattern(directory, isDirectory: true);
    }

    public bool IgnoreFile(string file) => MatchesProjectPattern(file, isDirectory: false);

    private bool MatchesProjectPattern(string path, bool isDirectory)
    {
        var relative = Path.GetRelativePath(_projectRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return _patterns.Any(pattern => pattern.Matches(relative, isDirectory));
    }

    private static string? FindProjectRoot(string start)
    {
        var current = new DirectoryInfo(Path.GetFullPath(start));
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".vslices")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private sealed record IgnorePattern(Regex Regex, bool DirectoryOnly)
    {
        public static IgnorePattern Parse(string pattern)
        {
            var normalized = pattern.Replace('\\', '/').TrimStart('/');
            var directoryOnly = normalized.EndsWith('/');
            normalized = normalized.TrimEnd('/');

            var anchored = pattern.StartsWith('/');
            var expression = Regex.Escape(normalized)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*");

            var prefix = anchored ? "^" : "(^|.*/)";
            var suffix = directoryOnly ? "(/.*)?$" : "$";

            return new(
                new Regex(prefix + expression + suffix, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                directoryOnly);
        }

        public bool Matches(string relativePath, bool isDirectory) =>
            (!DirectoryOnly || isDirectory) && Regex.IsMatch(relativePath);
    }
}
