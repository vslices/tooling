namespace VSlices.Tooling;

internal static class LoweringLineageStore
{
    public static string? ResolveBaselinePath(
        VSlicesProjectContext project,
        string materializationPath,
        string target)
    {
        if (!project.Contains(materializationPath))
            return null;

        var relative = Path.GetRelativePath(
            project.ProjectRoot,
            Path.GetFullPath(materializationPath));

        return Path.Combine(
            project.LineageRoot,
            CommandInfrastructure.NormalizeTarget(target),
            relative + ".baseline");
    }

    public static async Task<string?> TryRead(
        VSlicesProjectContext project,
        string materializationPath,
        string target,
        CancellationToken cancellationToken)
    {
        var path = ResolveBaselinePath(project, materializationPath, target);
        if (path is null || !File.Exists(path))
            return null;

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public static async Task<bool> TryWrite(
        VSlicesProjectContext project,
        string materializationPath,
        string target,
        string deterministicSource,
        CancellationToken cancellationToken)
    {
        var path = ResolveBaselinePath(project, materializationPath, target);
        if (path is null)
            return false;

        await CommandInfrastructure.AtomicWrite(path, deterministicSource, cancellationToken);
        return true;
    }
}
