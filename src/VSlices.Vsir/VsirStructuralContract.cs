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
