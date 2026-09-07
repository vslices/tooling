using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record VsirMutation(
    VsirMutationKind Kind,
    string Path,
    string? Value);

internal sealed record VsirMutationResult(
    string? Source,
    string? Error)
{
    public bool IsSuccess => Error is null;

    public static VsirMutationResult Success(string source) => new(source, null);
    public static VsirMutationResult Failure(string error) => new(null, error);
}

internal static class VsirMutationEngine
{
    public static VsirMutationResult Apply(
        string source,
        IReadOnlyList<VsirMutation> mutations)
    {
        if (mutations.Count == 0)
            return VsirMutationResult.Failure("UPDATE001: At least one semantic mutation is required.");

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

        var identityError = ValidateIdentity(root);
        if (identityError is not null)
            return VsirMutationResult.Failure(identityError);

        var contradiction = FindContradiction(mutations);
        if (contradiction is not null)
            return VsirMutationResult.Failure(contradiction);

        foreach (var mutation in mutations)
        {
            var error = ApplyOne(root, mutation);
            if (error is not null)
                return VsirMutationResult.Failure(error);
        }

        var candidateError = ValidateCandidate(root);
        if (candidateError is not null)
            return VsirMutationResult.Failure(candidateError);

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return VsirMutationResult.Success(writer.ToString());
    }

    public static IReadOnlyList<VsirPathContract> Discover(string source, out string? error)
    {
        error = null;
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                error = "DISC001: Expected one YAML mapping VSIR document.";
                return [];
            }

            var identityError = ValidateIdentity(root);
            if (identityError is not null)
            {
                error = identityError.Replace("UPDATE", "DISC", StringComparison.Ordinal);
                return [];
            }

            var kind = Scalar(root, "kind");
            var classification = Scalar(root, "classification");
            var traits = Sequence(root, "traits");
            return VsirAuthoringContract.Discover(
                kind,
                classification,
                traits,
                HasKey(root, "state"),
                HasKey(root, "representation"),
                HasKey(root, "input"),
                HasKey(root, "construction"));
        }
        catch (Exception ex)
        {
            error = $"DISC002: Could not parse VSIR artifact: {ex.Message}";
            return [];
        }
    }

    private static string? ApplyOne(YamlMappingNode root, VsirMutation mutation)
    {
        if (TryChildPath(mutation.Path, "state", out var stateField, out var stateTail))
        {
            return stateTail switch
            {
                null => ApplyMapFieldMutation(root, "state", stateField, mutation),
                "from" => ApplyStateFromMutation(root, stateField, mutation),
                _ => $"UPDATE004: Semantic path '{mutation.Path}' is not writable by the current authoring contract."
            };
        }

        if (TryChildPath(mutation.Path, "representation", out var representationField, out var representationTail))
        {
            return representationTail is null
                ? ApplyMapFieldMutation(root, "representation", representationField, mutation)
                : $"UPDATE004: Semantic path '{mutation.Path}' is not writable by the current authoring contract.";
        }

        if (TryChildPath(mutation.Path, "values", out var maintainedMember, out var maintainedTail))
        {
            return maintainedTail is null
                ? ApplyMaintainedValueMutation(root, maintainedMember, mutation)
                : $"UPDATE004: Semantic path '{mutation.Path}' is not writable by the current authoring contract.";
        }

        return mutation.Path switch
        {
            "tags" => ApplySetMutation(root, "tags", mutation),
            "traits" => ApplySetMutation(root, "traits", mutation),
            "kind" => ApplyScalarMutation(root, "kind", mutation),
            "classification" => ApplyScalarMutation(root, "classification", mutation),
            _ => $"UPDATE004: Semantic path '{mutation.Path}' is not writable by the current authoring contract."
        };
    }

    private static string? ApplyMapFieldMutation(
        YamlMappingNode root,
        string mapPath,
        string fieldName,
        VsirMutation mutation)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return $"UPDATE020: Semantic path '{mutation.Path}' requires a property name.";

        var mapKey = new YamlScalarNode(mapPath);
        YamlMappingNode map;
        if (root.Children.TryGetValue(mapKey, out var existingMapNode))
        {
            if (existingMapNode is not YamlMappingNode existingMap)
                return $"UPDATE025: Semantic path '{mapPath}' must be a mapping before its properties can be mutated.";
            map = existingMap;
        }
        else
        {
            if (mutation.Kind != VsirMutationKind.Add)
                return $"UPDATE022: Semantic property '{mapPath}.{fieldName}' does not exist; use 'add' to establish it.";
            map = new YamlMappingNode();
            root.Children[mapKey] = map;
        }

        var fieldKey = new YamlScalarNode(fieldName);
        var exists = map.Children.ContainsKey(fieldKey);

        switch (mutation.Kind)
        {
            case VsirMutationKind.Add when exists:
                return $"UPDATE021: Semantic property '{mapPath}.{fieldName}' already exists; use 'set' to change it.";

            case VsirMutationKind.Set when !exists:
                return $"UPDATE022: Semantic property '{mapPath}.{fieldName}' does not exist; use 'add' to establish it.";

            case VsirMutationKind.Remove when !exists:
                return $"UPDATE023: Semantic property '{mapPath}.{fieldName}' does not exist and cannot be removed.";
        }

        if (mutation.Kind == VsirMutationKind.Remove)
        {
            if (map.Children.Count == 1 && IsRequiredMap(root, mapPath))
                return $"UPDATE024: Cannot remove the last property from required semantic map '{mapPath}'.";

            map.Children.Remove(fieldKey);
            if (map.Children.Count == 0)
                root.Children.Remove(mapKey);
            return null;
        }

        var value = mutation.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || ContainsLineBreak(value))
            return $"UPDATE013: Semantic property '{mapPath}.{fieldName}' requires a non-empty single-line type declaration.";

        map.Children[fieldKey] = new YamlScalarNode(value);
        return null;
    }

    private static string? ApplyMaintainedValueMutation(
        YamlMappingNode root,
        string memberName,
        VsirMutation mutation)
    {
        if (string.IsNullOrWhiteSpace(memberName))
            return $"UPDATE020: Semantic path '{mutation.Path}' requires a maintained member name.";

        var valuesKey = new YamlScalarNode("values");
        YamlMappingNode values;
        if (root.Children.TryGetValue(valuesKey, out var existingValuesNode))
        {
            if (existingValuesNode is not YamlMappingNode existingValues)
                return "UPDATE025: Semantic path 'values' must be a mapping before its members can be mutated.";
            values = existingValues;
        }
        else
        {
            if (mutation.Kind != VsirMutationKind.Add)
                return $"UPDATE022: Maintained member 'values.{memberName}' does not exist; use 'add' to establish it.";
            values = new YamlMappingNode();
            root.Children[valuesKey] = values;
        }

        var memberKey = new YamlScalarNode(memberName);
        var exists = values.Children.ContainsKey(memberKey);

        switch (mutation.Kind)
        {
            case VsirMutationKind.Add when exists:
                return $"UPDATE021: Maintained member 'values.{memberName}' already exists; use 'set' to change it.";

            case VsirMutationKind.Set when !exists:
                return $"UPDATE022: Maintained member 'values.{memberName}' does not exist; use 'add' to establish it.";

            case VsirMutationKind.Remove when !exists:
                return $"UPDATE023: Maintained member 'values.{memberName}' does not exist and cannot be removed.";
        }

        if (mutation.Kind == VsirMutationKind.Remove)
        {
            if (values.Children.Count == 1 &&
                string.Equals(Scalar(root, "classification"), "maintained", StringComparison.Ordinal))
            {
                return "UPDATE024: Cannot remove the last member from required semantic map 'values'.";
            }

            values.Children.Remove(memberKey);
            if (values.Children.Count == 0)
                root.Children.Remove(valuesKey);
            return null;
        }

        var parsed = ParseMaintainedValueDeclaration(mutation.Value, mutation.Path);
        if (parsed.Error is not null)
            return parsed.Error;

        values.Children[memberKey] = parsed.Declaration!;
        return null;
    }

    private static (YamlMappingNode? Declaration, string? Error) ParseMaintainedValueDeclaration(
        string? value,
        string path)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, $"UPDATE013: Maintained member '{path}' requires a declaration value.");

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(value));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode declaration)
            {
                return (null,
                    $"UPDATE028: Maintained member '{path}' must use an inline mapping declaration such as {{state: {{Name: Natural}}}}.");
            }

            if (declaration.Children.Count != 1 ||
                !declaration.Children.TryGetValue(new YamlScalarNode("state"), out var stateNode) ||
                stateNode is not YamlMappingNode state ||
                state.Children.Count == 0)
            {
                return (null,
                    $"UPDATE028: Maintained member '{path}' must declare exactly one non-empty 'state' mapping.");
            }

            return (declaration, null);
        }
        catch (Exception ex)
        {
            return (null, $"UPDATE028: Could not parse maintained member '{path}': {ex.Message}");
        }
    }

    private static string? ApplyStateFromMutation(
        YamlMappingNode root,
        string fieldName,
        VsirMutation mutation)
    {
        if (!TryMapping(root, "state", out var state))
            return $"UPDATE022: Semantic property 'state.{fieldName}' does not exist; establish it before declaring 'from'.";

        var fieldKey = new YamlScalarNode(fieldName);
        if (!state.Children.TryGetValue(fieldKey, out var fieldNode))
            return $"UPDATE022: Semantic property 'state.{fieldName}' does not exist; establish it before declaring 'from'.";

        YamlMappingNode declaration;
        if (fieldNode is YamlScalarNode scalar)
        {
            if (string.IsNullOrWhiteSpace(scalar.Value))
                return $"UPDATE025: Semantic property 'state.{fieldName}' has no type declaration.";

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
            return $"UPDATE025: Semantic property 'state.{fieldName}' has an unsupported declaration shape.";
        }

        var fromKey = new YamlScalarNode("from");
        var exists = declaration.Children.ContainsKey(fromKey);

        if (mutation.Kind == VsirMutationKind.Add && exists)
            return $"UPDATE021: Semantic property 'state.{fieldName}.from' already exists; use 'set' to change it.";
        if (mutation.Kind == VsirMutationKind.Remove && !exists)
            return $"UPDATE023: Semantic property 'state.{fieldName}.from' does not exist and cannot be removed.";

        if (mutation.Kind == VsirMutationKind.Remove)
        {
            declaration.Children.Remove(fromKey);
            if (declaration.Children.Count == 1 &&
                declaration.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode) &&
                typeNode is YamlScalarNode typeScalar)
            {
                state.Children[fieldKey] = new YamlScalarNode(typeScalar.Value);
            }
            else
            {
                state.Children[fieldKey] = declaration;
            }
            return null;
        }

        var value = mutation.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || ContainsLineBreak(value))
            return $"UPDATE013: Semantic property 'state.{fieldName}.from' requires a non-empty single-line state reference.";

        declaration.Children[fromKey] = new YamlScalarNode(value);
        state.Children[fieldKey] = declaration;
        return null;
    }

    private static string? ApplyScalarMutation(
        YamlMappingNode root,
        string path,
        VsirMutation mutation)
    {
        if (mutation.Kind != VsirMutationKind.Set)
            return $"UPDATE012: Semantic path '{path}' supports only 'set'.";

        var value = mutation.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return $"UPDATE005: Value for semantic path '{path}' must not be empty.";

        root.Children[new YamlScalarNode(path)] = new YamlScalarNode(value);
        return null;
    }

    private static string? ApplySetMutation(
        YamlMappingNode root,
        string path,
        VsirMutation mutation)
    {
        var requested = ParseSetValues(mutation.Value);
        if (requested.Error is not null)
            return requested.Error.Replace("VALUE", path, StringComparison.Ordinal);

        var current = Sequence(root, path).ToList();
        switch (mutation.Kind)
        {
            case VsirMutationKind.Add:
                foreach (var value in requested.Values!)
                {
                    if (!current.Contains(value, StringComparer.Ordinal))
                        current.Add(value);
                }
                break;

            case VsirMutationKind.Remove:
                current.RemoveAll(value => requested.Values!.Contains(value, StringComparer.Ordinal));
                break;

            case VsirMutationKind.Set:
                current = requested.Values!.ToList();
                break;
        }

        if (current.Count == 0)
        {
            root.Children.Remove(new YamlScalarNode(path));
            return null;
        }

        root.Children[new YamlScalarNode(path)] =
            new YamlSequenceNode(current.Select(value => new YamlScalarNode(value)))
            {
                Style = YamlDotNet.Core.Events.SequenceStyle.Flow
            };
        return null;
    }

    private static (IReadOnlyList<string>? Values, string? Error) ParseSetValues(string? value)
    {
        if (value is null)
            return (null, "UPDATE013: VALUE mutation requires a value.");

        var values = value
            .Split(',', StringSplitOptions.TrimEntries)
            .ToArray();

        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
            return (null, "UPDATE013: VALUE values must be non-empty.");

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            return (null, "UPDATE014: VALUE values must be unique.");

        if (values.Any(ContainsLineBreak))
            return (null, "UPDATE013: VALUE values must be single-line.");

        return (values, null);
    }

    private static string? ValidateCandidate(YamlMappingNode root)
    {
        var kind = Scalar(root, "kind");
        var classification = Scalar(root, "classification");
        var traits = Sequence(root, "traits");

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var error = VsirAuthoringContract.ValidateScalar("kind", kind, kind);
            if (error is not null)
                return error;
        }

        if (!string.IsNullOrWhiteSpace(classification))
        {
            var error = VsirAuthoringContract.ValidateScalar("classification", classification, kind);
            if (error is not null)
                return error;
        }

        if (traits.Count > 0 && !string.Equals(kind, VsirAuthoringContract.DomainTypeKind, StringComparison.Ordinal))
            return "UPDATE011: 'traits' requires kind 'domain-type'.";

        var unsupportedTrait = traits.FirstOrDefault(
            trait => !VsirAuthoringContract.ExplicitDomainTypeTraits.Contains(trait, StringComparer.Ordinal));
        if (unsupportedTrait is not null)
        {
            return $"UPDATE026: Trait '{unsupportedTrait}' is not currently available for explicit authoring. " +
                   $"Supported explicit traits: {string.Join(", ", VsirAuthoringContract.ExplicitDomainTypeTraits)}.";
        }

        if (HasKey(root, "values") && !string.Equals(classification, "maintained", StringComparison.Ordinal))
            return "UPDATE027: 'values' is writable only for classification 'maintained'.";

        return null;
    }

    private static string? ValidateIdentity(YamlMappingNode root)
    {
        var version = Scalar(root, "vsir");
        var name = Scalar(root, "name");
        if (string.IsNullOrWhiteSpace(version))
            return "UPDATE016: Progressive VSIR artifact requires 'vsir'.";
        if (string.IsNullOrWhiteSpace(name))
            return "UPDATE017: Progressive VSIR artifact requires 'name'.";
        return null;
    }

    private static string? FindContradiction(IReadOnlyList<VsirMutation> mutations)
    {
        foreach (var group in mutations.GroupBy(mutation => mutation.Path, StringComparer.Ordinal))
        {
            if (group.Any(mutation => mutation.Kind == VsirMutationKind.Set) && group.Count() > 1)
                return $"UPDATE018: Semantic path '{group.Key}' cannot combine 'set' with another mutation in the same transaction.";

            var adds = group
                .Where(mutation => mutation.Kind == VsirMutationKind.Add)
                .SelectMany(mutation => ParseSetValues(mutation.Value).Values ?? [])
                .ToHashSet(StringComparer.Ordinal);
            var removes = group
                .Where(mutation => mutation.Kind == VsirMutationKind.Remove)
                .SelectMany(mutation => ParseSetValues(mutation.Value).Values ?? [])
                .ToHashSet(StringComparer.Ordinal);
            var overlap = adds.FirstOrDefault(removes.Contains);
            if (overlap is not null)
                return $"UPDATE019: Value '{overlap}' is both added to and removed from semantic path '{group.Key}' in the same transaction.";

            if (group.Key is not ("tags" or "traits") && group.Count() > 1)
                return $"UPDATE018: Semantic path '{group.Key}' cannot be set more than once in the same transaction.";
        }

        return null;
    }

    private static bool TryChildPath(
        string path,
        string rootPath,
        out string fieldName,
        out string? tail)
    {
        fieldName = string.Empty;
        tail = null;

        var prefix = rootPath + ".";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var remainder = path[prefix.Length..];
        var separator = remainder.IndexOf('.');
        if (separator < 0)
        {
            fieldName = remainder;
            return true;
        }

        fieldName = remainder[..separator];
        tail = remainder[(separator + 1)..];
        return true;
    }

    private static bool IsRequiredMap(YamlMappingNode root, string mapPath)
    {
        var classification = Scalar(root, "classification");
        return mapPath is "state" or "representation" &&
               classification is "value-object" or "entity" or "maintained" or "aggregate-root";
    }

    private static string? Scalar(YamlMappingNode root, string key) =>
        root.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar
            ? scalar.Value
            : null;

    private static bool HasKey(YamlMappingNode root, string key) =>
        root.Children.ContainsKey(new YamlScalarNode(key));

    private static bool TryMapping(YamlMappingNode root, string key, out YamlMappingNode mapping)
    {
        if (root.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlMappingNode value)
        {
            mapping = value;
            return true;
        }

        mapping = null!;
        return false;
    }

    private static IReadOnlyList<string> Sequence(YamlMappingNode root, string key)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(key), out var node))
            return [];

        if (node is not YamlSequenceNode sequence)
            return [];

        return sequence.Children
            .OfType<YamlScalarNode>()
            .Select(child => child.Value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static bool ContainsLineBreak(string value) =>
        value.Contains('\r') || value.Contains('\n');
}
