namespace VSlices.Tooling;

internal sealed record VSlicesProjectContext(
    string ProjectRoot,
    string VslicesRoot,
    string ConfigurationPath,
    string RulesetRoot,
    string LineageRoot,
    ProjectConfiguration Configuration)
{
    public static VSlicesProjectContext? FindFrom(string start)
    {
        var fullPath = Path.GetFullPath(start);
        var current = File.Exists(fullPath)
            ? new DirectoryInfo(Path.GetDirectoryName(fullPath)!)
            : new DirectoryInfo(fullPath);

        while (current is not null)
        {
            var vslicesRoot = Path.Combine(current.FullName, ".vslices");
            var configurationPath = Path.Combine(vslicesRoot, "config.yaml");
            if (File.Exists(configurationPath))
            {
                var configuration = ProjectConfiguration.LoadFromProjectRoot(current.FullName);
                if (configuration is null)
                    return null;

                return new VSlicesProjectContext(
                    current.FullName,
                    vslicesRoot,
                    configurationPath,
                    Path.Combine(vslicesRoot, "ruleset"),
                    Path.Combine(vslicesRoot, "lineage"),
                    configuration);
            }

            current = current.Parent;
        }

        return null;
    }

    public bool Contains(string path)
    {
        var relative = Path.GetRelativePath(ProjectRoot, Path.GetFullPath(path));
        return !Path.IsPathRooted(relative) &&
               !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }
}
