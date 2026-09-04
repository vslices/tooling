namespace VSlices.Tooling;

internal static class LoweringLineageBootstrap
{
    public static async Task<string?> TryResolveConfiguredBaseline(
        string rulesetRoot,
        string conventionalMaterializationPath,
        string existingMaterializationPath,
        string? sourceOverride,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sourceOverride))
            return null;

        if (!PathsEqual(conventionalMaterializationPath, existingMaterializationPath))
            return null;

        var configuration = ProjectConfiguration.LoadFromRulesetRoot(rulesetRoot);
        if (!string.Equals(
                configuration?.LineageBootstrapConvention,
                ProjectConfiguration.DefaultLineageBootstrapConvention,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!File.Exists(existingMaterializationPath))
            return null;

        return await File.ReadAllTextAsync(existingMaterializationPath, cancellationToken);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}
