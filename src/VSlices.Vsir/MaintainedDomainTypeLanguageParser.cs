using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

/// <summary>
/// Parses the currently evidenced maintained Domain Type surface. Maintained values
/// establish the closed set of valid instances, so transform input/construction is
/// intentionally not required.
/// </summary>
public static class MaintainedDomainTypeLanguageParser
{
    private static readonly HashSet<string> RootKeys = new(StringComparer.Ordinal)
    {
        "vsir", "kind", "name", "shape", "classification",
        "state", "representation", "values", "equality"
    };

    public static VsirParseResult Parse(string text)
    {
        var diagnostics = new List<VsirDiagnostic>();
        YamlMappingNode root;
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
                return Failure("VSIR001", "Expected one YAML mapping document.");
            root = mapping;
        }
        catch (Exception ex)
        {
            return Failure("VSIR000", ex.Message);
        }

        RejectUnknownKeys(root, RootKeys, "root", diagnostics);

        var version = Scalar(root, "vsir");
        var kind = Scalar(root, "kind");
        var name = Scalar(root, "name");
        var shape = Scalar(root, "shape");
        var classification = Scalar(root, "classification");

        Require(version == "0.1", "VSIR200", "Only VSIR 0.1 is supported.", "vsir");
        Require(kind == "domain-type", "VSIR201", "Only kind 'domain-type' is supported.", "kind");
        Require(shape == "product", "VSIR203", "Maintained Domain Types currently require shape 'product'.", "shape");
        Require(classification == "maintained", "VSIR202", "Maintained parser requires classification 'maintained'.", "classification");
        Require(!string.IsNullOrWhiteSpace(name), "VSIR105", "Domain Type name is required.", "name");

        var state = TryMapping(root, "state", out var stateNode)
            ? new ProductShape(ReadFields(stateNode, "state", allowFrom: true, diagnostics))
            : new ProductShape([]);
        var representation = TryMapping(root, "representation", out var reprNode)
            ? new ProductShape(ReadFields(reprNode, "representation", allowFrom: true, diagnostics))
            : new ProductShape([]);

        Require(state.Fields.Count > 0, "VSIR205", "Maintained state must contain at least one field.", "state");
        Require(representation.Fields.Count > 0, "VSIR206", "Maintained representation must contain at least one field.", "representation");

        var values = ParseValues(root, state, diagnostics);
        var equality = ParseEquality(root, state, diagnostics);

        foreach (var field in representation.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.From))
            {
                var same = state.Fields.SingleOrDefault(x => x.Name == field.Name);
                Require(same is not null && same.Type == field.Type, "VSIR210",
                    $"Cannot project representation.{field.Name} deterministically from state. Declare an explicit from source.",
                    $"representation.{field.Name}");
                continue;
            }

            Require(field.From!.StartsWith("state.", StringComparison.Ordinal), "VSIR238",
                $"representation.{field.Name}.from must reference state.", $"representation.{field.Name}.from");
            if (field.From.StartsWith("state.", StringComparison.Ordinal))
            {
                var first = field.From["state.".Length..].Split('.', 2)[0];
                Require(state.Fields.Any(x => x.Name == first), "VSIR237",
                    $"representation.{field.Name} references unknown state field '{first}'.",
                    $"representation.{field.Name}.from");
            }
        }

        var document = new DomainTypeVsir(
            version,
            kind,
            name,
            classification,
            shape,
            [],
            null,
            state,
            representation,
            null,
            new Construction(ConstructionInput.Product([]), []),
            equality,
            Values: values);

        return new(document, diagnostics);

        void Require(bool condition, string code, string message, string path)
        {
            if (!condition)
                diagnostics.Add(new(code, message, SemanticPath: path));
        }
    }

    private static IReadOnlyList<MaintainedValue> ParseValues(
        YamlMappingNode root,
        ProductShape state,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!TryMapping(root, "values", out var valuesNode) || valuesNode.Children.Count == 0)
        {
            diagnostics.Add(new("VSIR280", "Classification 'maintained' requires at least one maintained value.", SemanticPath: "values"));
            return [];
        }

        var result = new List<MaintainedValue>();
        foreach (var pair in valuesNode.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value) ||
                pair.Value is not YamlMappingNode valueNode)
            {
                diagnostics.Add(new("VSIR281", "Each maintained value requires a non-empty scalar name and mapping body.", SemanticPath: "values"));
                continue;
            }

            var path = $"values.{key.Value}";
            RejectUnknownKeys(valueNode, new HashSet<string>(["state"], StringComparer.Ordinal), path, diagnostics);
            if (!TryMapping(valueNode, "state", out var valueState))
            {
                diagnostics.Add(new("VSIR282", $"Maintained value '{key.Value}' requires state.", SemanticPath: $"{path}.state"));
                continue;
            }

            var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var statePair in valueState.Children)
            {
                if (statePair.Key is not YamlScalarNode stateKey || string.IsNullOrWhiteSpace(stateKey.Value) ||
                    statePair.Value is not YamlScalarNode stateValue || string.IsNullOrWhiteSpace(stateValue.Value))
                {
                    diagnostics.Add(new("VSIR283", $"Maintained value '{key.Value}' state requires scalar field values.", SemanticPath: $"{path}.state"));
                    continue;
                }
                bindings[stateKey.Value!] = stateValue.Value!;
            }

            foreach (var field in state.Fields)
            {
                if (!bindings.ContainsKey(field.Name))
                    diagnostics.Add(new("VSIR284", $"Maintained value '{key.Value}' does not establish state.{field.Name}.", SemanticPath: $"{path}.state"));
            }
            foreach (var binding in bindings.Keys)
            {
                if (!state.Fields.Any(x => x.Name == binding))
                    diagnostics.Add(new("VSIR285", $"Maintained value '{key.Value}' establishes unknown state field '{binding}'.", SemanticPath: $"{path}.state.{binding}"));
            }

            result.Add(new MaintainedValue(key.Value!, bindings));
        }

        if (result.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != result.Count)
            diagnostics.Add(new("VSIR286", "Maintained value names must be unique.", SemanticPath: "values"));

        return result;
    }

    private static EqualitySemantics? ParseEquality(
        YamlMappingNode root,
        ProductShape state,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!TryMapping(root, "equality", out var equality))
        {
            diagnostics.Add(new("VSIR216", "Classification 'maintained' requires explicit equality.", SemanticPath: "equality"));
            return null;
        }

        RejectUnknownKeys(equality, new HashSet<string>(["intrinsic", "over", "by"], StringComparer.Ordinal), "equality", diagnostics);
        var intrinsic = OptionalScalar(equality, "intrinsic");
        var over = OptionalScalar(equality, "over");
        var by = Scalar(equality, "by");
        if (string.IsNullOrWhiteSpace(by) || (intrinsic is null) == (over is null))
        {
            diagnostics.Add(new("VSIR106", "Equality requires by and exactly one of intrinsic or over.", SemanticPath: "equality"));
            return null;
        }

        if (!by.StartsWith("state.", StringComparison.Ordinal))
            diagnostics.Add(new("VSIR214", $"Equality currently requires a state reference, got '{by}'.", SemanticPath: "equality.by"));
        else
        {
            var field = by["state.".Length..].Split('.', 2)[0];
            if (!state.Fields.Any(x => x.Name == field))
                diagnostics.Add(new("VSIR215", $"Equality references unknown state field '{field}'.", SemanticPath: "equality.by"));
        }

        return new EqualitySemantics(intrinsic, over, by);
    }

    private static IReadOnlyList<Field> ReadFields(
        YamlMappingNode map,
        string path,
        bool allowFrom,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var fields = new List<Field>();
        foreach (var pair in map.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
            {
                diagnostics.Add(new("VSIR116", $"Semantic product '{path}' requires scalar field names.", SemanticPath: path));
                continue;
            }

            string? from = null;
            VsirType? type;
            if (pair.Value is YamlMappingNode declaration && declaration.Children.ContainsKey(new YamlScalarNode("type")))
            {
                var allowed = allowFrom
                    ? new HashSet<string>(["type", "from"], StringComparer.Ordinal)
                    : new HashSet<string>(["type"], StringComparer.Ordinal);
                RejectUnknownKeys(declaration, allowed, $"{path}.{key.Value}", diagnostics);
                type = declaration.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode)
                    ? ParseType(typeNode, $"{path}.{key.Value}.type", diagnostics)
                    : null;
                if (allowFrom)
                    from = OptionalScalar(declaration, "from");
            }
            else
            {
                type = ParseType(pair.Value, $"{path}.{key.Value}", diagnostics);
            }

            if (type is not null)
                fields.Add(new Field(key.Value!, type, from));
        }
        return fields;
    }

    private static VsirType? ParseType(YamlNode node, string path, ICollection<VsirDiagnostic> diagnostics)
    {
        if (node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            return new NamedVsirType(scalar.Value!);
        if (node is YamlMappingNode mapping && mapping.Children.Count == 1 &&
            mapping.Children.Keys.Single() is YamlScalarNode constructor && !string.IsNullOrWhiteSpace(constructor.Value))
        {
            var inner = ParseType(mapping.Children.Values.Single(), path, diagnostics);
            return inner is null ? null : new UnaryVsirType(constructor.Value!, inner);
        }
        diagnostics.Add(new("VSIR115", $"Invalid semantic type declaration at '{path}'.", SemanticPath: path));
        return null;
    }

    private static void RejectUnknownKeys(
        YamlMappingNode map,
        IReadOnlySet<string> allowed,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        foreach (var key in map.Children.Keys.OfType<YamlScalarNode>())
        {
            if (!string.IsNullOrWhiteSpace(key.Value) && !allowed.Contains(key.Value!))
                diagnostics.Add(new("VSIR104", $"Unsupported semantic '{key.Value}' under {path}.", SemanticPath: path == "root" ? key.Value : $"{path}.{key.Value}"));
        }
    }

    private static bool TryMapping(YamlMappingNode map, string key, out YamlMappingNode value)
    {
        value = null!;
        return map.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
               node is YamlMappingNode mapping && (value = mapping) is not null;
    }

    private static string Scalar(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar
            ? scalar.Value ?? string.Empty
            : string.Empty;

    private static string? OptionalScalar(YamlMappingNode map, string key)
    {
        var value = Scalar(map, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static VsirParseResult Failure(string code, string message) =>
        new(null, [new VsirDiagnostic(code, message)]);
}
