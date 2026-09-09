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

        if (missing.Length > 0)
        {
            // Missing knowledge and invalid knowledge are independent facts.
            // Canonical parsing is intentionally stricter than progressive
            // authoring, so diagnostics caused only by absent assertions are not
            // evidence that a present assertion is invalid. Conversely, any
            // diagnostic about knowledge already present must survive alongside
            // the list of missing obligations.
            var blocking = parsed.Diagnostics
                .Where(diagnostic => !CanBeExplainedByMissingKnowledge(diagnostic, root))
                .ToArray();

            if (blocking.Length == 0)
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
                missing,
                blocking);
        }

        return new(
            VsirProgressiveValidity.Valid,
            VsirConformanceState.Invalid,
            [],
            parsed.Diagnostics);
    }

    private static bool CanBeExplainedByMissingKnowledge(
        VsirDiagnostic diagnostic,
        YamlMappingNode root) =>
        diagnostic.Code switch
        {
            // Parser-level absence diagnostics must participate in the same
            // progressive classification as validator-level absence diagnostics.
            // VSIR111 is emitted before the validator's VSIR207 when root input
            // has not been authored yet; treating it as blocking would make every
            // legitimate new -> update progression invalid until input exists.
            "VSIR111" => !HasKey(root, "input"),

            "VSIR201" => !HasKey(root, "kind"),
            "VSIR202" => !HasKey(root, "classification"),
            "VSIR203" => !HasKey(root, "shape"),
            "VSIR204" => !HasTrait(root, "transform"),
            "VSIR205" => !HasKey(root, "state"),
            "VSIR206" => !HasKey(root, "representation"),
            "VSIR207" => !HasKey(root, "input"),
            "VSIR209" => !HasKey(root, "input") || !HasKey(root, "construction"),
            "VSIR210" => true, // representation source/mapping can be authored progressively
            "VSIR216" => !HasKey(root, "equality"),
            "VSIR223" => !HasKey(root, "refined-from"),
            "VSIR224" => !HasKey(root, "input"),
            "VSIR225" => !HasKey(root, "input"),
            "VSIR229" => true, // refined state establishment can still be incomplete
            _ => false
        };

    private static bool HasTrait(YamlMappingNode root, string trait) =>
        root.Children.TryGetValue(new YamlScalarNode("traits"), out var traitsNode) &&
        traitsNode is YamlSequenceNode traits &&
        traits.Children
            .OfType<YamlScalarNode>()
            .Any(value => string.Equals(value.Value, trait, StringComparison.Ordinal));

    private static bool HasKey(YamlMappingNode root, string key) =>
        root.Children.ContainsKey(new YamlScalarNode(key));

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
