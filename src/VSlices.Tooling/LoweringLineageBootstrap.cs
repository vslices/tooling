namespace VSlices.Tooling;

internal static class LoweringLineageBootstrap
{
    public static bool IsConfiguredFor(
        string rulesetRoot,
        string conventionalMaterializationPath,
        string existingMaterializationPath,
        string? sourceOverride)
    {
        if (!string.IsNullOrWhiteSpace(sourceOverride))
            return false;

        if (!PathsEqual(conventionalMaterializationPath, existingMaterializationPath))
            return false;

        var configuration = ProjectConfiguration.LoadFromRulesetRoot(rulesetRoot);
        return string.Equals(
            configuration?.LineageBootstrapConvention,
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
