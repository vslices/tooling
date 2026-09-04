namespace VSlices.Tooling;

internal static class LoweringLineageBootstrap
{
    public static bool IsConfiguredFor(
        VSlicesProjectContext project,
        string conventionalMaterializationPath,
        string existingMaterializationPath,
        string? sourceOverride)
    {
        if (!string.IsNullOrWhiteSpace(sourceOverride))
            return false;

        if (!PathsEqual(conventionalMaterializationPath, existingMaterializationPath))
            return false;

        return string.Equals(
            project.Configuration.LineageBootstrapConvention,
            ProjectConfiguration.DefaultLineageBootstrapConvention,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}
