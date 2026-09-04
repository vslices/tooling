using System.Reflection;

namespace VSlices.Tooling;

internal static class CliVersion
{
    public static string Display => ToDisplayVersion(InformationalVersion());

    internal static string ToDisplayVersion(string version)
    {
        var normalized = version.Trim().TrimStart('v', 'V').Split('+')[0];
        const string buildPrefix = "0.0.0-build.";

        return normalized.StartsWith(buildPrefix, StringComparison.OrdinalIgnoreCase)
            ? "build" + normalized[buildPrefix.Length..]
            : normalized;
    }

    private static string InformationalVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
