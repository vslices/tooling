using VSlices.Vsir;
using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

/// <summary>
/// Common pre-persistence boundary for progressive VSIR mutations.
/// It preserves legitimate incompleteness while refusing to persist assertions
/// that the canonical parser already knows are invalid.
/// </summary>
internal static class VsirMutationCandidate
{
    public static VsirMutationResult Prepare(
        string source,
        IReadOnlyList<VsirMutation> semanticMutations)
    {
        var declarationTargets = semanticMutations
            .Where(mutation => mutation.Kind == VsirMutationKind.Set)
            .Select(mutation => DeclarationTargetFor(mutation.Path))
            .Where(target => target is not null)
            .Select(target => target!)
            .Distinct()
            .ToArray();

        if (declarationTargets.Length == 0)
            return VsirMutationResult.Success(source);

        YamlStream yaml;
        YamlMappingNode root;
        try
        {
            yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
                return VsirMutationResult.Failure("UPDATE002: Expected one YAML mapping VSIR document.");
            root = mapping;
        }
        catch (Exception ex)
        {
            return VsirMutationResult.Failure($"UPDATE003: Could not parse VSIR artifact: {ex.Message}");
        }

        var changed = false;
        foreach (var target in declarationTargets)
        {
            if (!root.Children.TryGetValue(new YamlScalarNode(target.MapPath), out var mapNode) ||
                mapNode is not YamlMappingNode map)
                continue;

            var fieldKey = new YamlScalarNode(target.FieldName);
            if (!map.Children.TryGetValue(fieldKey, out var fieldNode) ||
                fieldNode is not YamlMappingNode shorthand)
                continue;

            if (shorthand.Children.ContainsKey(new YamlScalarNode("type")) ||
                shorthand.Children.ContainsKey(new YamlScalarNode("from")) ||
                shorthand.Children.ContainsKey(new YamlScalarNode("mapping")))
                continue;

            // A structural semantic type shorthand such as
            //   Value:
            //     sequence: string
            // becomes an expanded declaration before adding local source metadata:
            //   Value:
            //     type:
            //       sequence: string
            //     from|mapping: ...
            //
            // The transformation is syntax-preserving: the semantic type itself
            // is moved intact under `type` before the requested assertion is
            // applied by the normal mutation pipeline.
            var type = new YamlMappingNode();
            foreach (var pair in shorthand.Children)
                type.Children.Add(pair.Key, pair.Value);

            map.Children[fieldKey] = new YamlMappingNode
            {
                { "type", type }
            };
            changed = true;
        }

        if (!changed)
            return VsirMutationResult.Success(source);

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return VsirMutationResult.Success(writer.ToString());
    }

    public static string? Validate(
        string source,
        VsirValidationContext validationContext)
    {
        var frontier = VsirMutationPipeline.Discover(source, out var discoveryError);
        if (discoveryError is not null)
            return discoveryError.Replace("DISC", "UPDATE", StringComparison.Ordinal);

        var state = VsirArtifactState.Assess(source, frontier, validationContext);
        if (state.ProgressiveValidity != VsirProgressiveValidity.Invalid &&
            state.Conformance != VsirConformanceState.Invalid)
        {
            return null;
        }

        var diagnostic = state.ConformanceDiagnostics.FirstOrDefault();
        return diagnostic is null
            ? "UPDATE050: Candidate VSIR is structurally invalid."
            : $"UPDATE050: Candidate VSIR assertion is invalid ({diagnostic.Code}): {diagnostic.Message}";
    }

    private static DeclarationTarget? DeclarationTargetFor(string path)
    {
        if (TryTarget(path, "state", ".from", out var stateField))
            return new("state", stateField!);

        if (TryTarget(path, "representation", ".from", out var representationFromField))
            return new("representation", representationFromField!);

        if (TryTarget(path, "representation", ".mapping", out var representationMappingField))
            return new("representation", representationMappingField!);

        return null;
    }

    private static bool TryTarget(
        string path,
        string mapPath,
        string suffix,
        out string? fieldName)
    {
        var prefix = mapPath + ".";
        fieldName = null;
        if (!path.StartsWith(prefix, StringComparison.Ordinal) ||
            !path.EndsWith(suffix, StringComparison.Ordinal))
            return false;

        var candidate = path[prefix.Length..^suffix.Length];
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('.', StringComparison.Ordinal))
            return false;

        fieldName = candidate;
        return true;
    }

    private sealed record DeclarationTarget(string MapPath, string FieldName);
}
