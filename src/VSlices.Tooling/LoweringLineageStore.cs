namespace VSlices.Tooling;

internal static class LoweringLineageStore
{
    public static string? ResolveBaselinePath(
        string rulesetRoot,
        string materializationPath,
        string target)
    {
        var rulesetDirectory = new DirectoryInfo(Path.GetFullPath(rulesetRoot));
        var vslicesDirectory = rulesetDirectory.Parent;
        var projectDirectory = vslicesDirectory?.Parent;

        if (vslicesDirectory is null || projectDirectory is null)
            return null;

        var relative = Path.GetRelativePath(
            projectDirectory.FullName,
            Path.GetFullPath(materializationPath));

        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return Path.Combine(
            vslicesDirectory.FullName,
            "lineage",
            CommandInfrastructure.NormalizeTarget(target),
            relative + ".baseline");
    }

    public static async Task<string?> TryRead(
        string rulesetRoot,
        string materializationPath,
        string target,
        CancellationToken cancellationToken)
    {
        var path = ResolveBaselinePath(rulesetRoot, materializationPath, target);
        if (path is null || !File.Exists(path))
            return null;

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public static async Task<bool> TryWrite(
        string rulesetRoot,
        string materializationPath,
        string target,
        string deterministicSource,
        CancellationToken cancellationToken)
    {
        var path = ResolveBaselinePath(rulesetRoot, materializationPath, target);
        if (path is null)
            return false;

        await CommandInfrastructure.AtomicWrite(path, deterministicSource, cancellationToken);
        return true;
    }
}
