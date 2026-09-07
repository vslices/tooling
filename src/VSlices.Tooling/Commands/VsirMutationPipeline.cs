using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal static class VsirMutationPipeline
{
    public static VsirMutationResult Apply(
        string source,
        IReadOnlyList<VsirMutation> mutations)
    {
        var mappingMutations = mutations
            .Where(IsRepresentationMappingMutation)
            .ToArray();
        var remainingMutations = mutations
            .Where(mutation => !IsRepresentationMappingMutation(mutation))
            .ToArray();

        var current = source;
        if (remainingMutations.Length > 0)
        {
            var baseResult = VsirMutationEngine.Apply(current, remainingMutations);
            if (!baseResult.IsSuccess)
                return baseResult;

            current = baseResult.Source!;
        }
        else if (mappingMutations.Length == 0)
        {
            return VsirMutationResult.Failure("UPDATE001: At least one semantic mutation is required.");
        }

        foreach (var mutation in mappingMutations)
        {
            var result = ApplyRepresentationMapping(current, mutation);
            if (!result.IsSuccess)
                return result;

            current = result.Source!;
        }

        return VsirMutationResult.Success(current);
    }

    public static IReadOnlyList<VsirPathContract> Discover(string source, out string? error)
    {
        var frontier = VsirMutationEngine.Discover(source, out error).ToList();
        if (error is not null)
            return frontier;

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return frontier;

            AddStateSourceContracts(root, frontier);
            AddRepresentationSourceContracts(root, frontier);
            return frontier;
        }
        catch (Exception ex)
        {
            error = $"DISC002: Could not parse VSIR artifact: {ex.Message}";
            return [];
        }
    }

    private static void AddStateSourceContracts(
        YamlMappingNode root,
        ICollection<VsirPathContract> frontier)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode("state"), out var stateNode) ||
            stateNode is not YamlMappingNode state)
            return;

        foreach (var (fieldNode, declarationNode) in state.Children)
        {
            if (fieldNode is not YamlScalarNode field || string.IsNullOrWhiteSpace(field.Value))
                continue;

            var hasFrom = declarationNode is YamlMappingNode declaration &&
                declaration.Children.ContainsKey(new YamlScalarNode("from"));

            frontier.Add(new(
                $"state.{field.Value}.from",
                "state-reference",
                VsirFrontierStatus.Optional,
                "Declares semantic provenance for a derived state coordinate. The source must be a direct state.* reference.",
                hasFrom
                    ? new HashSet<VsirMutationKind>
                    {
                        VsirMutationKind.Remove,
                        VsirMutationKind.Set
                    }
                    : new HashSet<VsirMutationKind>
                    {
                        VsirMutationKind.Add,
                        VsirMutationKind.Set
                    }));
        }
    }

    private static void AddRepresentationSourceContracts(
        YamlMappingNode root,
        ICollection<VsirPathContract> frontier)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode("representation"), out var representationNode) ||
            representationNode is not YamlMappingNode representation)
            return;

        foreach (var (fieldNode, declarationNode) in representation.Children)
        {
            if (fieldNode is not YamlScalarNode field || string.IsNullOrWhiteSpace(field.Value))
                continue;

            var declaration = declarationNode as YamlMappingNode;
            var hasFrom = declaration?.Children.ContainsKey(new YamlScalarNode("from")) == true;
            var hasMapping = declaration?.Children.ContainsKey(new YamlScalarNode("mapping")) == true;

            if (!hasMapping)
            {
                frontier.Add(new(
                    $"representation.{field.Value}.from",
                    "state-reference",
                    VsirFrontierStatus.Optional,
                    "Declares a direct state source for a representation field when no semantic transformation is required.",
                    hasFrom
                        ? new HashSet<VsirMutationKind>
                        {
                            VsirMutationKind.Remove,
                            VsirMutationKind.Set
                        }
                        : new HashSet<VsirMutationKind>
                        {
                            VsirMutationKind.Add,
                            VsirMutationKind.Set
                        }));
            }

            if (!hasFrom)
            {
                frontier.Add(new(
                    $"representation.{field.Value}.mapping",
                    "mapping",
                    VsirFrontierStatus.Optional,
                    "Declares the semantic projection or transformation used to expose this representation field. It is mutually exclusive with a direct from source.",
                    new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
            }
        }
    }

    private static bool IsRepresentationMappingMutation(VsirMutation mutation) =>
        TryRepresentationMappingPath(mutation.Path, out _);

    private static VsirMutationResult ApplyRepresentationMapping(
        string source,
        VsirMutation mutation)
    {
        if (!TryRepresentationMappingPath(mutation.Path, out var fieldName))
            return VsirMutationResult.Failure($"UPDATE004: Semantic path '{mutation.Path}' is not writable by the current authoring contract.");

        if (mutation.Kind != VsirMutationKind.Set)
            return VsirMutationResult.Failure($"UPDATE012: Semantic path 'representation.{fieldName}.mapping' supports only 'set'.");

        if (string.IsNullOrWhiteSpace(mutation.Value))
            return VsirMutationResult.Failure($"UPDATE013: Semantic path 'representation.{fieldName}.mapping' requires a mapping declaration.");

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

        if (!root.Children.TryGetValue(new YamlScalarNode("representation"), out var representationNode) ||
            representationNode is not YamlMappingNode representation)
        {
            return VsirMutationResult.Failure($"UPDATE022: Semantic property 'representation.{fieldName}' does not exist; establish it before declaring 'mapping'.");
        }

        var fieldKey = new YamlScalarNode(fieldName);
        if (!representation.Children.TryGetValue(fieldKey, out var fieldNode))
            return VsirMutationResult.Failure($"UPDATE022: Semantic property 'representation.{fieldName}' does not exist; establish it before declaring 'mapping'.");

        YamlMappingNode declaration;
        if (fieldNode is YamlScalarNode scalar)
        {
            if (string.IsNullOrWhiteSpace(scalar.Value))
                return VsirMutationResult.Failure($"UPDATE025: Semantic property 'representation.{fieldName}' has no type declaration.");

            declaration = new YamlMappingNode
            {
                { "type", scalar.Value }
            };
        }
        else if (fieldNode is YamlMappingNode mapping)
        {
            declaration = mapping;
        }
        else
        {
            return VsirMutationResult.Failure($"UPDATE025: Semantic property 'representation.{fieldName}' has an unsupported declaration shape.");
        }

        if (declaration.Children.ContainsKey(new YamlScalarNode("from")))
        {
            return VsirMutationResult.Failure(
                $"UPDATE030: Semantic property 'representation.{fieldName}' already has a 'from' source; 'from' and 'mapping' are mutually exclusive.");
        }

        YamlMappingNode mappingDeclaration;
        try
        {
            var mappingYaml = new YamlStream();
            mappingYaml.Load(new StringReader(mutation.Value));
            if (mappingYaml.Documents.Count != 1 ||
                mappingYaml.Documents[0].RootNode is not YamlMappingNode parsed ||
                parsed.Children.Count == 0)
            {
                return VsirMutationResult.Failure(
                    $"UPDATE042: Semantic property 'representation.{fieldName}.mapping' must be a non-empty mapping declaration.");
            }

            mappingDeclaration = parsed;
        }
        catch (Exception ex)
        {
            return VsirMutationResult.Failure(
                $"UPDATE042: Could not parse representation mapping for '{fieldName}': {ex.Message}");
        }

        declaration.Children[new YamlScalarNode("mapping")] = mappingDeclaration;
        representation.Children[fieldKey] = declaration;

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return VsirMutationResult.Success(writer.ToString());
    }

    private static bool TryRepresentationMappingPath(string path, out string fieldName)
    {
        const string prefix = "representation.";
        const string suffix = ".mapping";

        fieldName = string.Empty;
        if (!path.StartsWith(prefix, StringComparison.Ordinal) ||
            !path.EndsWith(suffix, StringComparison.Ordinal))
            return false;

        var candidate = path[prefix.Length..^suffix.Length];
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains(".", StringComparison.Ordinal))
            return false;

        fieldName = candidate;
        return true;
    }
}
