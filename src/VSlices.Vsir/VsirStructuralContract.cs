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

            // Expanded field declarations are one of the shared field laws that
            // already belonged to this preflight before recursive key conservation.
            // Keep their scalar-key grammar here; deeper semantic grammars remain
            // owned by their specialized parsers.
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "type" };
            if (allowFrom)
                allowed.Add("from");
            if (allowMapping)
                allowed.Add("mapping");
            RejectUnknownScalarKeys(declaration, allowed, fieldPath, diagnostics);

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
        if (owner.Children.TryGetValue(new YamlScalarNode("equality"), out var equalityNode) &&
            equalityNode is YamlMappingNode equality)
        {
            RejectNonScalarKeys(equality, path, diagnostics);
        }
    }

    private static void ValidateConstruction(
        YamlMappingNode owner,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var separator = path.LastIndexOf('.');
        var key = separator >= 0 ? path[(separator + 1)..] : path;
        if (!owner.Children.TryGetValue(new YamlScalarNode(key), out var constructionNode) ||
            constructionNode is not YamlSequenceNode construction)
            return;

        for (var index = 0; index < construction.Children.Count; index++)
        {
            if (construction.Children[index] is not YamlMappingNode step || step.Children.Count != 1)
                continue;

            RejectNonScalarKeys(step, $"{path}[{index}]", diagnostics);

            if (step.Children.Keys.Single() is not YamlScalarNode operation ||
                step.Children.Values.Single() is not YamlMappingNode payload)
            {
                continue;
            }

            var stepPath = $"{path}[{index}].{operation.Value}";
            RejectNonScalarKeys(payload, stepPath, diagnostics);

            switch (operation.Value)
            {
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
        if (ensure.Children.TryGetValue(new YamlScalarNode("condition"), out var conditionNode) &&
            conditionNode is YamlMappingNode condition)
        {
            var conditionPath = path + ".condition";
            RejectNonScalarKeys(condition, conditionPath, diagnostics);

            if (condition.Children.TryGetValue(new YamlScalarNode("args"), out var argsNode) &&
                argsNode is YamlMappingNode args)
            {
                var argsPath = conditionPath + ".args";
                RejectNonScalarKeys(args, argsPath, diagnostics);
                if (args.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
                    ValidateSemanticExpression(valueNode, argsPath + ".value", diagnostics);
            }
        }

        if (ensure.Children.TryGetValue(new YamlScalarNode("failure"), out var failureNode) &&
            failureNode is YamlMappingNode failure)
        {
            RejectNonScalarKeys(failure, path + ".failure", diagnostics);
        }
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
        if (resolve.Children.TryGetValue(new YamlScalarNode("failure"), out var failureNode) &&
            failureNode is YamlMappingNode failure)
        {
            RejectNonScalarKeys(failure, path + ".failure", diagnostics);
        }
    }

    private static void ValidateApply(
        YamlMappingNode apply,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!apply.Children.TryGetValue(new YamlScalarNode("input"), out var inputNode) ||
            inputNode is not YamlMappingNode input)
            return;

        var inputPath = path + ".input";
        RejectNonScalarKeys(input, inputPath, diagnostics);

        if (input.Children.TryGetValue(new YamlScalarNode("map"), out var mapNode) &&
            mapNode is YamlMappingNode map)
        {
            // Mapped apply field names are semantic data. Arbitrary scalar names
            // remain valid data; structured YAML keys are never silently ignored.
            RejectNonScalarKeys(map, inputPath + ".map", diagnostics);
        }
    }

    private static void ValidateRefine(
        YamlMappingNode refine,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (refine.Children.TryGetValue(new YamlScalarNode("state"), out var stateNode) &&
            stateNode is YamlMappingNode state)
        {
            RejectNonScalarKeys(state, path + ".state", diagnostics);
        }

        if (refine.Children.TryGetValue(new YamlScalarNode("as"), out var asNode) &&
            asNode is YamlMappingNode outputs)
        {
            RejectNonScalarKeys(outputs, path + ".as", diagnostics);
        }

        if (refine.Children.TryGetValue(new YamlScalarNode("failure"), out var failureNode) &&
            failureNode is YamlMappingNode failure)
        {
            RejectNonScalarKeys(failure, path + ".failure", diagnostics);
        }
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

        if (mapping.Children.TryGetValue(new YamlScalarNode("represent"), out var represented))
        {
            ValidateProjection(represented, path + ".represent", diagnostics);
        }

        if (mapping.Children.TryGetValue(new YamlScalarNode("select"), out var selectNode) &&
            selectNode is YamlMappingNode select)
        {
            var selectPath = path + ".select";
            RejectNonScalarKeys(select, selectPath, diagnostics);
            if (select.Children.TryGetValue(new YamlScalarNode("source"), out var sourceNode))
                ValidateProjection(sourceNode, selectPath + ".source", diagnostics);
        }

        if (mapping.Children.TryGetValue(new YamlScalarNode("map"), out var mapNode) &&
            mapNode is YamlMappingNode map)
        {
            var mapPath = path + ".map";
            RejectNonScalarKeys(map, mapPath, diagnostics);
            if (map.Children.TryGetValue(new YamlScalarNode("source"), out var sourceNode))
                ValidateProjection(sourceNode, mapPath + ".source", diagnostics);
            if (map.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
                ValidateProjection(valueNode, mapPath + ".value", diagnostics);
        }

        if (mapping.Children.TryGetValue(new YamlScalarNode("values"), out var valuesNode) &&
            valuesNode is YamlSequenceNode values)
        {
            for (var index = 0; index < values.Children.Count; index++)
                ValidateProjection(values.Children[index], $"{path}.values[{index}]", diagnostics);
        }
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
