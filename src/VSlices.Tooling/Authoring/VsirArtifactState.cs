using VSlices.Vsir;
using YamlDotNet.RepresentationModel;

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
        YamlMappingNode root;
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
            {
                return new(
                    VsirProgressiveValidity.Invalid,
                    VsirConformanceState.Invalid,
                    [],
                    [new("VSIR001", "Expected one YAML mapping document.")]);
            }

            root = mapping;
        }
        catch (Exception ex)
        {
            return new(
                VsirProgressiveValidity.Invalid,
                VsirConformanceState.Invalid,
                [],
                [new("VSIR000", ex.Message)]);
        }

        var missing = frontier
            .Where(item => item.Status == VsirFrontierStatus.Required)
            .Select(item => item.Path)
            .Where(path => !RootAssertionExists(root, path))
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

    private static bool RootAssertionExists(YamlMappingNode root, string path)
    {
        var rootPath = path.Split('.', 2, StringSplitOptions.None)[0];
        return root.Children.ContainsKey(new YamlScalarNode(rootPath));
    }
}
