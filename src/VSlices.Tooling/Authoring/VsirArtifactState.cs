using VSlices.Vsir;

namespace VSlices.Tooling;

internal enum VsirProgressiveValidity
{
    Valid,
    Invalid
}

internal enum VsirConformanceState
{
    Incomplete,
    Conforming,
    Invalid
}

internal sealed record VsirArtifactState(
    VsirProgressiveValidity ProgressiveValidity,
    VsirConformanceState Conformance,
    IReadOnlyList<string> MissingRequiredPaths,
    IReadOnlyList<VsirDiagnostic> ConformanceDiagnostics)
{
    public static VsirArtifactState Assess(
        string source,
        IReadOnlyList<VsirPathContract> frontier)
    {
        var missing = frontier
            .Where(item => item.Status == VsirFrontierStatus.Required)
            .Select(item => item.Path)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0)
        {
            return new(
                VsirProgressiveValidity.Valid,
                VsirConformanceState.Incomplete,
                missing,
                []);
        }

        var parsed = VsirLanguageParser.Parse(source);
        return parsed.IsSuccess
            ? new(
                VsirProgressiveValidity.Valid,
                VsirConformanceState.Conforming,
                [],
                [])
            : new(
                VsirProgressiveValidity.Valid,
                VsirConformanceState.Invalid,
                [],
                parsed.Diagnostics);
    }
}
