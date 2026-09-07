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
            .Where(path => !AssertionExists(root, path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var authoredAssertionDiagnostics = ValidatePresentAuthoringAssertions(root);
        if (authoredAssertionDiagnostics.Count > 0)
        {
            return new(
                VsirProgressiveValidity.Valid,
                VsirConformanceState.Invalid,
                missing,
                authoredAssertionDiagnostics);
        }

        if (missing.Length > 0)
        {
            return new(
                VsirProgressiveValidity.Valid,
                VsirConformanceState.Incomplete,
                missing,
                []);
        }

        var parsed = VsirParser.Parse(source);
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

    private static IReadOnlyList<VsirDiagnostic> ValidatePresentAuthoringAssertions(YamlMappingNode root)
    {
        var diagnostics = new List<VsirDiagnostic>();
        var kind = Scalar(root, "kind");

        ValidateScalar("kind", kind, kind, diagnostics);
        ValidateScalar("shape", Scalar(root, "shape"), kind, diagnostics);
        ValidateScalar("classification", Scalar(root, "classification"), kind, diagnostics);

        if (root.Children.TryGetValue(new YamlScalarNode("traits"), out var traitsNode))
        {
            if (traitsNode is not YamlSequenceNode traits)
            {
                diagnostics.Add(new("VSIR-AUTH002", "Present semantic assertion 'traits' must be a sequence."));
            }
            else
            {
                foreach (var traitNode in traits.Children)
                {
                    if (traitNode is not YamlScalarNode trait || string.IsNullOrWhiteSpace(trait.Value))
                    {
                        diagnostics.Add(new("VSIR-AUTH002", "Present semantic assertion 'traits' requires non-empty scalar members."));
                        continue;
                    }

                    if (!VsirAuthoringContract.ExplicitDomainTypeTraits.Contains(trait.Value, StringComparer.Ordinal))
                    {
                        diagnostics.Add(new(
                            "VSIR-AUTH002",
                            $"Trait '{trait.Value}' is outside the current explicit authoring vocabulary."));
                    }
                }
            }
        }

        return diagnostics;
    }

    private static void ValidateScalar(
        string path,
        string? value,
        string? currentKind,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var error = VsirAuthoringContract.ValidateScalar(path, value, currentKind);
        if (error is null)
            return;

        var separator = error.IndexOf(':');
        var message = separator >= 0 ? error[(separator + 1)..].TrimStart() : error;
        diagnostics.Add(new("VSIR-AUTH001", message));
    }

    private static string? Scalar(YamlMappingNode root, string key) =>
        root.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar
            ? scalar.Value
            : null;

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
