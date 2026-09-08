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
        IReadOnlyList<VsirPathContract> frontier,
        VsirValidationContext? validationContext = null)
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
            .Where(path => !AssertionExists(root, path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        // Conformance is owned by the canonical VSIR parser/validator plus the
        // active semantic validation environment. The intentionally narrower
        // public authoring contract must never turn an already-conforming form
        // (for example sum or maintained) into a conformance failure.
        var parsed = VsirParser.Parse(source, validationContext);
        if (parsed.IsSuccess)
        {
            return new(
                VsirProgressiveValidity.Valid,
                VsirConformanceState.Conforming,
                [],
                []);
        }

        // A progressively-authored document may fail canonical parsing because
        // required decisions have not been made yet. Public discovery remains
        // the authority for identifying those missing transitions; once no
        // required transition is missing, parser diagnostics mean invalidity.
        if (missing.Length > 0)
        {
            return new(
                VsirProgressiveValidity.Valid,
                VsirConformanceState.Incomplete,
                missing,
                []);
        }

        return new(
            VsirProgressiveValidity.Valid,
            VsirConformanceState.Invalid,
            [],
            parsed.Diagnostics);
    }

    private static bool AssertionExists(YamlMappingNode root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        YamlNode current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is not YamlMappingNode mapping ||
                !mapping.Children.TryGetValue(new YamlScalarNode(segment), out current!))
            {
                return false;
            }
        }

        return true;
    }
}
