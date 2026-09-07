using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal static class VsirMutationPipeline
{
    public static VsirMutationResult Apply(
        string source,
        IReadOnlyList<VsirMutation> mutations)
    {
        var structuredFieldMutations = mutations
            .Where(IsStructuredFieldMutation)
            .ToArray();
        var mappingMutations = mutations
            .Where(IsRepresentationMappingMutation)
            .ToArray();
        var remainingMutations = mutations
            .Where(mutation =>
                !IsStructuredFieldMutation(mutation) &&
                !IsRepresentationMappingMutation(mutation))
            .ToArray();

        var current = source;
        if (remainingMutations.Length > 0)
        {
            var baseResult = VsirMutationEngine.Apply(current, remainingMutations);
            if (!baseResult.IsSuccess)
                return baseResult;

            current = baseResult.Source!;
        }
        else if (structuredFieldMutations.Length == 0 && mappingMutations.Length == 0)
        {
            return VsirMutationResult.Failure("UPDATE001: At least one semantic mutation is required.");
        }

        foreach (var mutation in structuredFieldMutations)
        {
            var result = ApplyStructuredFieldMutation(current, mutation);
            if (!result.IsSuccess)
                return result;

            current = result.Source!;
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

    private static bool IsStructuredFieldMutation(VsirMutation mutation)
    {
        if (mutation.Kind == VsirMutationKind.Remove ||
            !TrySemanticFieldPath(mutation.Path, out _, out _) ||
            string.IsNullOrWhiteSpace(mutation.Value))
            return false;

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(mutation.Value));
            return yaml.Documents.Count == 1 &&
                   yaml.Documents[0].RootNode is YamlMappingNode;
        }
        catch
        {
            return false;
        }
    }

    private static VsirMutationResult ApplyStructuredFieldMutation(
        string source,
        VsirMutation mutation)
    {
        if (!TrySemanticFieldPath(mutation.Path, out var mapPath, out var fieldName))
            return VsirMutationResult.Failure($"UPDATE004: Semantic path '{mutation.Path}' is not writable by the current authoring contract.");

        if (mutation.Kind is not (VsirMutationKind.Add or VsirMutationKind.Set))
            return VsirMutationResult.Failure($"UPDATE012: Structured semantic field '{mutation.Path}' supports only 'add' or 'set'.");

        if (string.IsNullOrWhiteSpace(mutation.Value))
            return VsirMutationResult.Failure($"UPDATE013: Semantic property '{mutation.Path}' requires a semantic field declaration.");

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

        if (mapPath == "input" && !HasTransformTrait(root))
        {
            return VsirMutationResult.Failure(
                $"UPDATE039: Semantic path '{mutation.Path}' is writable only when explicit trait 'transform' is established.");
        }

        YamlMappingNode declaration;
        try
        {
            var declarationYaml = new YamlStream();
            declarationYaml.Load(new StringReader(mutation.Value));
            if (declarationYaml.Documents.Count != 1 ||
                declarationYaml.Documents[0].RootNode is not YamlMappingNode parsed)
            {
                return VsirMutationResult.Failure(
                    $"UPDATE043: Semantic property '{mutation.Path}' must use a structured semantic field declaration.");
            }

            var declarationError = ValidateStructuredFieldDeclaration(parsed, mutation.Path);
            if (declarationError is not null)
                return VsirMutationResult.Failure(declarationError);

            declaration = parsed;
        }
        catch (Exception ex)
        {
            return VsirMutationResult.Failure(
                $"UPDATE043: Could not parse structured semantic field '{mutation.Path}': {ex.Message}");
        }

        var mapKey = new YamlScalarNode(mapPath);
        YamlMappingNode map;
        if (root.Children.TryGetValue(mapKey, out var mapNode))
        {
            if (mapNode is not YamlMappingNode existingMap)
                return VsirMutationResult.Failure($"UPDATE025: Semantic path '{mapPath}' must be a mapping before its properties can be mutated.");
            map = existingMap;
        }
        else
        {
            if (mutation.Kind != VsirMutationKind.Add)
                return VsirMutationResult.Failure($"UPDATE022: Semantic property '{mutation.Path}' does not exist; use 'add' to establish it.");
            map = new YamlMappingNode();
            root.Children[mapKey] = map;
        }

        var fieldKey = new YamlScalarNode(fieldName);
        var exists = map.Children.ContainsKey(fieldKey);
        if (mutation.Kind == VsirMutationKind.Add && exists)
            return VsirMutationResult.Failure($"UPDATE021: Semantic property '{mutation.Path}' already exists; use 'set' to change it.");
        if (mutation.Kind == VsirMutationKind.Set && !exists)
            return VsirMutationResult.Failure($"UPDATE022: Semantic property '{mutation.Path}' does not exist; use 'add' to establish it.");

        map.Children[fieldKey] = declaration;

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return VsirMutationResult.Success(writer.ToString());
    }

    private static string? ValidateStructuredFieldDeclaration(
        YamlMappingNode declaration,
        string path)
    {
        if (declaration.Children.Count == 0)
            return $"UPDATE043: Semantic property '{path}' requires a non-empty structured semantic field declaration.";

        var typeKey = new YamlScalarNode("type");
        if (declaration.Children.TryGetValue(typeKey, out var typeNode))
        {
            if (declaration.Children.Count != 1)
            {
                return $"UPDATE043: Expanded semantic field '{path}' may establish only 'type' at the field boundary; author 'from' or 'mapping' through their local semantic paths.";
            }

            return ValidateSemanticTypeNode(typeNode)
                ? null
                : $"UPDATE043: Expanded semantic field '{path}.type' must be a scalar type or structural semantic type declaration.";
        }

        if (declaration.Children.Count != 1)
            return $"UPDATE043: Structural semantic field '{path}' must declare exactly one type constructor.";

        var constructor = declaration.Children.Keys.Single() as YamlScalarNode;
        if (constructor is null || string.IsNullOrWhiteSpace(constructor.Value))
            return $"UPDATE043: Structural semantic field '{path}' requires a non-empty type constructor name.";

        var value = declaration.Children.Values.Single();
        return ValidateSemanticTypeNode(value)
            ? null
            : $"UPDATE043: Structural semantic field '{path}' requires a scalar or nested structural semantic type value.";
    }

    private static bool ValidateSemanticTypeNode(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
            return !string.IsNullOrWhiteSpace(scalar.Value);

        if (node is not YamlMappingNode mapping || mapping.Children.Count != 1)
            return false;

        var constructor = mapping.Children.Keys.Single() as YamlScalarNode;
        return constructor is not null &&
               !string.IsNullOrWhiteSpace(constructor.Value) &&
               ValidateSemanticTypeNode(mapping.Children.Values.Single());
    }

    private static bool HasTransformTrait(YamlMappingNode root)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode("traits"), out var traitsNode) ||
            traitsNode is not YamlSequenceNode traits)
            return false;

        return traits.Children
            .OfType<YamlScalarNode>()
            .Any(trait => string.Equals(trait.Value, "transform", StringComparison.Ordinal));
    }

    private static bool TrySemanticFieldPath(
        string path,
        out string mapPath,
        out string fieldName)
    {
        mapPath = string.Empty;
        fieldName = string.Empty;

        foreach (var candidate in new[] { "state", "representation", "input" })
        {
            var prefix = candidate + ".";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var remainder = path[prefix.Length..];
            if (string.IsNullOrWhiteSpace(remainder) || remainder.Contains('.', StringComparison.Ordinal))
                return false;

            mapPath = candidate;
            fieldName = remainder;
            return true;
        }

        return false;
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
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('.', StringComparison.Ordinal))
            return false;

        fieldName = candidate;
        return true;
    }
}
