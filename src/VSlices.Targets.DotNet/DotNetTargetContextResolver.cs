using System.Diagnostics;
using System.IO.Enumeration;
using VSlices.Vsir;

namespace VSlices.Targets.DotNet;

public sealed record DotNetTargetContext(string? ProjectPath, string Namespace);

public static class DotNetTargetContextResolver
{
    public static async Task<(DotNetTargetContext? Context, VsirDiagnostic? Diagnostic)> Resolve(
        string vsirPath,
        string? namespaceOverride,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? namespaceIgnoredFolders = null)
    {
        if (!string.IsNullOrWhiteSpace(namespaceOverride))
            return (new(null, namespaceOverride), null);

        var directory = Path.GetDirectoryName(vsirPath)!;
        var project = FindProject(directory);
        if (project is null)
        {
            return (null, new(
                "DOTNET001",
                $"Could not find a unique .csproj for '{vsirPath}'. Pass --namespace to override target context explicitly."));
        }

        var result = await RunDotNet(
            directory,
            cancellationToken,
            "msbuild",
            project,
            "-nologo",
            "-getProperty:RootNamespace");

        if (result.ExitCode != 0)
        {
            return (null, new(
                "DOTNET002",
                "dotnet msbuild could not resolve the evaluated RootNamespace for this VSIR. " +
                "Pass --namespace to override it explicitly. " + result.StandardError.Trim()));
        }

        var rootNamespace = result.StandardOutput.Trim();
        if (rootNamespace.Length == 0)
        {
            return (null, new(
                "DOTNET003",
                "The related .csproj evaluated an empty RootNamespace. Pass --namespace explicitly."));
        }

        var projectDirectory = Path.GetDirectoryName(project)!;
        var relativeDirectory = Path.GetRelativePath(projectDirectory, directory);
        var relativeSegments = relativeDirectory == "."
            ? []
            : relativeDirectory.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

        var ignorePatterns = namespaceIgnoredFolders?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizePattern)
            .ToArray() ?? [];

        var namespaceSegments = ignorePatterns.Length == 0
            ? relativeSegments
            : relativeSegments
                .Where((segment, index) => !ShouldIgnoreSegment(relativeSegments, index, segment, ignorePatterns))
                .ToArray();

        var namespaceName = namespaceSegments.Length == 0
            ? rootNamespace
            : rootNamespace + "." + string.Join('.', namespaceSegments);

        return (new(project, namespaceName), null);
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

    private static string? FindProject(string startDirectory)
    {
        for (var current = new DirectoryInfo(startDirectory); current is not null; current = current.Parent)
        {
            var projects = current.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
            if (projects.Length == 1)
                return projects[0].FullName;
            if (projects.Length > 1)
                return null;
        }

        return null;
    }

    private static async Task<ProcessResult> RunDotNet(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        if (process is null)
            return new(-1, string.Empty, "Could not start the dotnet CLI.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new(process.ExitCode, await stdout, await stderr);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
