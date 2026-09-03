namespace VSlices.Tooling;

internal static class RulesetLocator
{
    public static string? FindFrom(string path)
    {
        var current = File.Exists(path)
            ? new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!)
            : new DirectoryInfo(Path.GetFullPath(path));

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".vslices", "ruleset");
            if (File.Exists(Path.Combine(candidate, "manifest.yaml")))
                return candidate;

            current = current.Parent;
        }

        return null;
    }
}
