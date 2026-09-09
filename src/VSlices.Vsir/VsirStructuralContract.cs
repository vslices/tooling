using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

/// <summary>
/// Structural guarantees shared by all canonical VSIR 0.1 domain-type forms.
/// Shape-specific parsers remain responsible for their own semantic grammar;
/// this preflight only prevents common YAML obligations from drifting between them.
/// </summary>
internal static class VsirStructuralContract
{
    public static IReadOnlyList<VsirDiagnostic> Validate(string source)
    {
        YamlMappingNode root;
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
                return [];
            root = mapping;
        }
        catch
        {
            // Syntax/document-shape diagnostics remain owned by the canonical parser.
            return [];
        }

        var diagnostics = new List<VsirDiagnostic>();
        RejectNonScalarKeys(root, "root", diagnostics);
        ValidateFieldMap(root, "state", allowFrom: true, allowMapping: false, diagnostics);
        ValidateFieldMap(root, "representation", allowFrom: true, allowMapping: true, diagnostics);
        ValidateFieldMap(root, "input", allowFrom: false, allowMapping: false, diagnostics);
        ValidateEquality(root, "equality", diagnostics);
        ValidateConstruction(root, "construction", diagnostics);

        if (root.Children.TryGetValue(new YamlScalarNode("variants"), out var variantsNode) &&
            variantsNode is YamlMappingNode variants)
        {
            foreach (var (variantNameNode, variantNode) in variants.Children)
            {
                if (variantNameNode is not YamlScalarNode variantName || string.IsNullOrWhiteSpace(variantName.Value))
                    continue;
                if (variantNode is not YamlMappingNode variant)
                    continue;

                var prefix = $"variants.{variantName.Value}";
                RejectNonScalarKeys(variant, prefix, diagnostics);
                ValidateFieldMap(variant, "state", allowFrom: true, allowMapping: false, diagnostics, prefix);
                ValidateFieldMap(variant, "representation", allowFrom: true, allowMapping: true, diagnostics, prefix);
                ValidateFieldMap(variant, "input", allowFrom: false, allowMapping: false, diagnostics, prefix);
                ValidateConstruction(variant, $"{prefix}.construction", diagnostics);
            }
        }

        return diagnostics;
    }

    private static void ValidateFieldMap(
        YamlMappingNode owner,
        string key,
        bool allowFrom,
        bool allowMapping,
        ICollection<VsirDiagnostic> diagnostics,
        string? prefix = null)
    {
        if (!owner.Children.TryGetValue(new YamlScalarNode(key), out var fieldsNode) ||
            fieldsNode is not YamlMappingNode fields)
            return;

        var mapPath = string.IsNullOrWhiteSpace(prefix) ? key : $"{prefix}.{key}";
        RejectNonScalarKeys(fields, mapPath, diagnostics);

        foreach (var (fieldNameNode, declarationNode) in fields.Children)
        {
            if (fieldNameNode is not YamlScalarNode fieldName || string.IsNullOrWhiteSpace(fieldName.Value))
                continue;
            if (declarationNode is not YamlMappingNode declaration)
                continue;

            // A mapping without declaration keys is a structural semantic type
            // shorthand (for example: sequence: string), not an expanded field declaration.
            var hasType = declaration.Children.ContainsKey(new YamlScalarNode("type"));
            var hasFrom = declaration.Children.ContainsKey(new YamlScalarNode("from"));
            var hasMapping = declaration.Children.ContainsKey(new YamlScalarNode("mapping"));
            if (!hasType && !hasFrom && !hasMapping)
                continue;

            var fieldPath = $"{mapPath}.{fieldName.Value}";
            RejectNonScalarKeys(declaration, fieldPath, diagnostics);

            var allowed = new HashSet<string>(StringComparer.Ordinal) { "type" };
            if (allowFrom)
                allowed.Add("from");
            if (allowMapping)
                allowed.Add("mapping");
            RejectUnknownScalarKeys(declaration, allowed, fieldPath, diagnostics);

            // Once source/projection metadata is present this is an expanded field
            // declaration, not a type-constructor shorthand. Keeping `type`
            // mandatory prevents forms such as `from: state.Value` from being
            // accidentally interpreted as a unary semantic type by one parser.
            if (!hasType && (hasFrom || hasMapping))
            {
                diagnostics.Add(new(
                    "VSIR156",
                    $"Expanded semantic property '{fieldPath}' requires 'type' when declaring 'from' or 'mapping'.",
                    SemanticPath: fieldPath));
            }

            if (hasFrom)
            {
                if (!allowFrom)
                {
                    diagnostics.Add(new(
                        "VSIR152",
                        $"Semantic property '{fieldPath}.from' is not admitted at this field boundary.",
                        SemanticPath: fieldPath + ".from"));
                }
                else if (declaration.Children[new YamlScalarNode("from")] is not YamlScalarNode from ||
                         string.IsNullOrWhiteSpace(from.Value))
                {
                    diagnostics.Add(new(
                        "VSIR153",
                        $"Semantic property '{fieldPath}.from' must be a non-empty scalar semantic reference.",
                        SemanticPath: fieldPath + ".from"));
                }
            }

            if (hasMapping && !allowMapping)
            {
                diagnostics.Add(new(
                    "VSIR154",
                    $"Semantic property '{fieldPath}.mapping' is not admitted at this field boundary.",
                    SemanticPath: fieldPath + ".mapping"));
            }

            if (hasFrom && hasMapping)
            {
                diagnostics.Add(new(
                    "VSIR155",
                    $"Semantic property '{fieldPath}' declares both 'from' and 'mapping'; a representation coordinate must have exactly one semantic source.",
                    SemanticPath: fieldPath));
            }

            if (hasMapping && allowMapping)
            {
                ValidateProjection(
                    declaration.Children[new YamlScalarNode("mapping")],
                    fieldPath + ".mapping",
                    diagnostics);
            }
        }
    }

    private static void ValidateEquality(
        YamlMappingNode owner,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!owner.Children.TryGetValue(new YamlScalarNode("equality"), out var equalityNode) ||
            equalityNode is not YamlMappingNode equality)
            return;

        ValidateFixedMapping(equality, ["intrinsic", "over", "by"], path, diagnostics);
    }

    private static void ValidateConstruction(
        YamlMappingNode owner,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var key = path.Contains('.', StringComparison.Ordinal)
            ? path[(path.LastIndexOf('.', StringComparison.Ordinal) + 1)..]
            : path;
        if (!owner.Children.TryGetValue(new YamlScalarNode(key), out var constructionNode) ||
            constructionNode is not YamlSequenceNode construction)
            return;

        for (var index = 0; index < construction.Children.Count; index++)
        {
            if (construction.Children[index] is not YamlMappingNode step || step.Children.Count != 1 ||
                step.Children.Keys.Single() is not YamlScalarNode operation ||
                step.Children.Values.Single() is not YamlMappingNode payload)
            {
                continue;
            }

            var stepPath = $"{path}[{index}].{operation.Value}";
            switch (operation.Value)
            {
                case "normalize":
                    ValidateFixedMapping(payload, ["target", "intrinsic"], stepPath, diagnostics);
                    break;
                case "ensure":
                    ValidateEnsure(payload, stepPath, diagnostics);
                    break;
                case "resolve":
                    ValidateResolve(payload, stepPath, diagnostics);
                    break;
                case "apply":
                    ValidateApply(payload, stepPath, diagnostics);
                    break;
                case "refine":
                    ValidateRefine(payload, stepPath, diagnostics);
                    break;
            }
        }
    }

    private static void ValidateEnsure(
        YamlMappingNode ensure,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        ValidateFixedMapping(ensure, ["condition", "failure"], path, diagnostics);

        if (ensure.Children.TryGetValue(new YamlScalarNode("condition"), out var conditionNode) &&
            conditionNode is YamlMappingNode condition)
        {
            var conditionPath = path + ".condition";
            ValidateFixedMapping(condition, ["intrinsic", "args"], conditionPath, diagnostics);

            if (condition.Children.TryGetValue(new YamlScalarNode("args"), out var argsNode) &&
                argsNode is YamlMappingNode args)
            {
                ValidateConditionArgs(condition, args, conditionPath + ".args", diagnostics);
            }
        }

        if (ensure.Children.TryGetValue(new YamlScalarNode("failure"), out var failureNode) &&
            failureNode is YamlMappingNode failure)
        {
            ValidateFixedMapping(failure, ["message"], path + ".failure", diagnostics);
        }
    }

    private static void ValidateConditionArgs(
        YamlMappingNode condition,
        YamlMappingNode args,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var intrinsic = condition.Children.TryGetValue(new YamlScalarNode("intrinsic"), out var intrinsicNode) &&
                        intrinsicNode is YamlScalarNode scalar
            ? scalar.Value
            : null;

        IReadOnlySet<string>? allowed = intrinsic switch
        {
            "non-empty" or "not-whitespace" => new HashSet<string>(["value"], StringComparer.Ordinal),
            "length-at-most" => new HashSet<string>(["value", "max"], StringComparer.Ordinal),
            "length-between" => new HashSet<string>(["value", "min", "max"], StringComparer.Ordinal),
            _ => null
        };

        if (allowed is null)
            RejectNonScalarKeys(args, path, diagnostics);
        else
            ValidateFixedMapping(args, allowed, path, diagnostics);

        if (args.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
            ValidateSemanticExpression(valueNode, path + ".value", diagnostics);
    }

    private static void ValidateSemanticExpression(
        YamlNode node,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (node is YamlScalarNode)
            return;
        if (node is not YamlMappingNode mapping)
            return;

        RejectNonScalarKeys(mapping, path, diagnostics);
        if (!mapping.Children.ContainsKey(new YamlScalarNode("intrinsic")))
            return;

        ValidateFixedMapping(mapping, ["intrinsic", "values"], path, diagnostics);
        if (mapping.Children.TryGetValue(new YamlScalarNode("values"), out var valuesNode) &&
            valuesNode is YamlSequenceNode values)
        {
            for (var index = 0; index < values.Children.Count; index++)
                ValidateSemanticExpression(values.Children[index], $"{path}.values[{index}]", diagnostics);
        }
    }

    private static void ValidateResolve(
        YamlMappingNode resolve,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        ValidateFixedMapping(resolve, ["source", "id", "as", "failure"], path, diagnostics);
        if (resolve.Children.TryGetValue(new YamlScalarNode("failure"), out var failureNode) &&
            failureNode is YamlMappingNode failure)
        {
            ValidateFixedMapping(failure, ["message"], path + ".failure", diagnostics);
        }
    }

    private static void ValidateApply(
        YamlMappingNode apply,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        ValidateFixedMapping(apply, ["over", "input", "as"], path, diagnostics);
        if (!apply.Children.TryGetValue(new YamlScalarNode("input"), out var inputNode) ||
            inputNode is not YamlMappingNode input)
            return;

        var inputPath = path + ".input";
        var hasSource = input.Children.ContainsKey(new YamlScalarNode("source"));
        var hasMap = input.Children.ContainsKey(new YamlScalarNode("map"));
        if (hasSource || hasMap)
        {
            ValidateFixedMapping(input, ["source", "map"], inputPath, diagnostics);
            if (input.Children.TryGetValue(new YamlScalarNode("map"), out var mapNode) &&
                mapNode is YamlMappingNode map)
            {
                // Mapped apply field names are semantic data, but they still must
                // be scalar names rather than YAML structures.
                RejectNonScalarKeys(map, inputPath + ".map", diagnostics);
            }
        }
        else
        {
            // Direct apply input is also a variable-key semantic data map.
            RejectNonScalarKeys(input, inputPath, diagnostics);
        }
    }

    private static void ValidateRefine(
        YamlMappingNode refine,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (refine.Children.TryGetValue(new YamlScalarNode("state"), out var stateNode))
        {
            ValidateFixedMapping(refine, ["state"], path, diagnostics);
            if (stateNode is YamlMappingNode state)
                RejectNonScalarKeys(state, path + ".state", diagnostics);
            return;
        }

        if (refine.Children.ContainsKey(new YamlScalarNode("intrinsic")))
        {
            ValidateFixedMapping(refine, ["intrinsic", "value", "as", "failure"], path, diagnostics);
            if (refine.Children.TryGetValue(new YamlScalarNode("as"), out var asNode) &&
                asNode is YamlMappingNode outputs)
            {
                RejectNonScalarKeys(outputs, path + ".as", diagnostics);
            }
            if (refine.Children.TryGetValue(new YamlScalarNode("failure"), out var failureNode) &&
                failureNode is YamlMappingNode failure)
            {
                ValidateFixedMapping(failure, ["message"], path + ".failure", diagnostics);
            }
            return;
        }

        ValidateFixedMapping(refine, ["value", "as"], path, diagnostics);
    }

    private static void ValidateProjection(
        YamlNode node,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (node is YamlScalarNode)
            return;
        if (node is not YamlMappingNode mapping)
            return;

        RejectNonScalarKeys(mapping, path, diagnostics);

        if (mapping.Children.ContainsKey(new YamlScalarNode("stringify")))
        {
            ValidateFixedMapping(mapping, ["stringify"], path, diagnostics);
            return;
        }

        if (mapping.Children.TryGetValue(new YamlScalarNode("represent"), out var represented))
        {
            ValidateFixedMapping(mapping, ["represent"], path, diagnostics);
            ValidateProjection(represented, path + ".represent", diagnostics);
            return;
        }

        if (mapping.Children.TryGetValue(new YamlScalarNode("select"), out var selectNode) &&
            selectNode is YamlMappingNode select)
        {
            ValidateFixedMapping(mapping, ["select"], path, diagnostics);
            ValidateFixedMapping(select, ["source", "field"], path + ".select", diagnostics);
            if (select.Children.TryGetValue(new YamlScalarNode("source"), out var sourceNode))
                ValidateProjection(sourceNode, path + ".select.source", diagnostics);
            return;
        }

        if (mapping.Children.TryGetValue(new YamlScalarNode("map"), out var mapNode) &&
            mapNode is YamlMappingNode map)
        {
            ValidateFixedMapping(mapping, ["map"], path, diagnostics);
            ValidateFixedMapping(map, ["source", "bind", "value"], path + ".map", diagnostics);
            if (map.Children.TryGetValue(new YamlScalarNode("source"), out var sourceNode))
                ValidateProjection(sourceNode, path + ".map.source", diagnostics);
            if (map.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
                ValidateProjection(valueNode, path + ".map.value", diagnostics);
            return;
        }

        if (mapping.Children.ContainsKey(new YamlScalarNode("intrinsic")))
        {
            ValidateFixedMapping(mapping, ["intrinsic", "values"], path, diagnostics);
            if (mapping.Children.TryGetValue(new YamlScalarNode("values"), out var valuesNode) &&
                valuesNode is YamlSequenceNode values)
            {
                for (var index = 0; index < values.Children.Count; index++)
                    ValidateProjection(values.Children[index], $"{path}.values[{index}]", diagnostics);
            }
        }
    }

    private static void ValidateFixedMapping(
        YamlMappingNode mapping,
        IEnumerable<string> allowed,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        RejectNonScalarKeys(mapping, path, diagnostics);
        RejectUnknownScalarKeys(
            mapping,
            allowed.ToHashSet(StringComparer.Ordinal),
            path,
            diagnostics);
    }

    private static void RejectUnknownScalarKeys(
        YamlMappingNode mapping,
        IReadOnlySet<string> allowed,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        foreach (var keyNode in mapping.Children.Keys.OfType<YamlScalarNode>())
        {
            if (string.IsNullOrWhiteSpace(keyNode.Value) || allowed.Contains(keyNode.Value!))
                continue;

            diagnostics.Add(new(
                "VSIR104",
                $"Unsupported semantic '{keyNode.Value}' under {path}.",
                SemanticPath: path + "." + keyNode.Value));
        }
    }

    private static void RejectNonScalarKeys(
        YamlMappingNode mapping,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        foreach (var keyNode in mapping.Children.Keys)
        {
            if (keyNode is YamlScalarNode)
                continue;

            diagnostics.Add(new(
                "VSIR104",
                $"Unsupported non-scalar semantic key under '{path}'. Semantic mapping keys must be scalar names.",
                SemanticPath: path));
        }
    }
}
