using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

/// <summary>
/// Parses the canonical VSIR 0.1 sum-domain-type surface.
/// A sum owns no product coordinates itself; each variant owns its traits,
/// state, representation, input and construction semantics.
/// </summary>
public static class SumDomainTypeLanguageParser
{
    private static readonly HashSet<string> RootKeys = new(StringComparer.Ordinal)
    {
        "vsir", "kind", "name", "classification", "shape",
        "state", "representation", "variants"
    };

    private static readonly HashSet<string> VariantKeys = new(StringComparer.Ordinal)
    {
        "traits", "refined-from", "state", "representation", "input", "construction", "equality"
    };

    private static readonly HashSet<string> SupportedClassifications =
        new(["value-object", "identifier"], StringComparer.Ordinal);

    private static readonly HashSet<string> SupportedTraits =
        new(["transform", "identifier", "refined"], StringComparer.Ordinal);

    public static VsirParseResult Parse(
        string text,
        VsirValidationContext? validationContext = null)
    {
        validationContext ??= VsirValidationContext.Empty;
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

        RejectUnknownKeys(root, RootKeys, "root", diagnostics, "VSIR104");

        var version = Scalar(root, "vsir");
        var kind = Scalar(root, "kind");
        var name = Scalar(root, "name");
        var classification = Scalar(root, "classification");
        var shape = Scalar(root, "shape");

        Require(version == "0.1", "VSIR200", "Only VSIR 0.1 is supported.", "vsir");
        Require(kind == "domain-type", "VSIR201", "Only kind 'domain-type' is supported.", "kind");
        Require(SupportedClassifications.Contains(classification), "VSIR202",
            $"Unsupported classification '{classification}'. Supported classifications: {string.Join(", ", SupportedClassifications)}.",
            "classification");
        Require(shape == "sum", "VSIR203", "Sum parser requires shape 'sum'.", "shape");
        Require(!string.IsNullOrWhiteSpace(name), "VSIR105", "Domain Type name is required.", "name");

        ValidateEmptyRootProduct(root, "state");
        ValidateEmptyRootProduct(root, "representation");

        var variants = new List<DomainTypeVariant>();
        if (!TryMapping(root, "variants", out var variantsNode) || variantsNode.Children.Count == 0)
        {
            diagnostics.Add(new("VSIR270", "Shape 'sum' requires at least one variant.", SemanticPath: "variants"));
        }
        else
        {
            foreach (var pair in variantsNode.Children)
            {
                if (pair.Key is not YamlScalarNode variantKey || string.IsNullOrWhiteSpace(variantKey.Value) ||
                    pair.Value is not YamlMappingNode variantNode)
                {
                    diagnostics.Add(new("VSIR271", "Each sum variant requires a non-empty scalar name and mapping body.", SemanticPath: "variants"));
                    continue;
                }

                var variant = ParseVariant(variantKey.Value!, variantNode, validationContext, diagnostics);
                if (variant is not null)
                    variants.Add(variant);
            }
        }

        if (variants.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != variants.Count)
            diagnostics.Add(new("VSIR272", "Sum variant names must be unique.", SemanticPath: "variants"));

        var document = new DomainTypeVsir(
            version,
            kind,
            name,
            classification,
            shape,
            [],
            null,
            new ProductShape([]),
            new ProductShape([]),
            null,
            new Construction(ConstructionInput.Product([]), []),
            null,
            variants);

        return new(document, diagnostics);

        void ValidateEmptyRootProduct(YamlMappingNode map, string key)
        {
            if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node))
                return;
            if (node is not YamlMappingNode mapping || mapping.Children.Count != 0)
            {
                diagnostics.Add(new(
                    "VSIR273",
                    $"Shape 'sum' does not own root {key} coordinates; declare them inside each variant.",
                    SemanticPath: key));
            }
        }

        void Require(bool condition, string code, string message, string path)
        {
            if (!condition)
                diagnostics.Add(new(code, message, SemanticPath: path));
        }
    }

    private static DomainTypeVariant? ParseVariant(
        string name,
        YamlMappingNode node,
        VsirValidationContext validationContext,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var path = $"variants.{name}";
        RejectUnknownKeys(node, VariantKeys, path, diagnostics, "VSIR104");

        var traits = ReadScalarSequence(node, "traits", $"{path}.traits", diagnostics);
        foreach (var duplicate in traits.GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1))
            diagnostics.Add(new("VSIR217", $"Trait '{duplicate.Key}' is declared more than once.", SemanticPath: $"{path}.traits"));
        foreach (var trait in traits.Distinct(StringComparer.Ordinal))
        {
            if (!SupportedTraits.Contains(trait))
                diagnostics.Add(new("VSIR218", $"Unsupported trait '{trait}'.", SemanticPath: $"{path}.traits"));
        }

        if (!traits.Contains("transform", StringComparer.Ordinal))
            diagnostics.Add(new("VSIR204", $"Sum variant '{name}' requires trait 'transform'.", SemanticPath: $"{path}.traits"));

        var refinedFrom = OptionalScalar(node, "refined-from");
        var state = TryMapping(node, "state", out var stateNode)
            ? new ProductShape(ReadFields(stateNode, $"{path}.state", diagnostics))
            : new ProductShape([]);
        var representation = TryMapping(node, "representation", out var representationNode)
            ? new ProductShape(ReadFields(representationNode, $"{path}.representation", diagnostics))
            : new ProductShape([]);
        var input = ParseInput(node, path, diagnostics);
        var construction = new Construction(input, ParseConstruction(node, path, validationContext, diagnostics));

        if (state.Fields.Count == 0)
            diagnostics.Add(new("VSIR205", $"Sum variant '{name}' state must contain at least one field.", SemanticPath: $"{path}.state"));
        if (representation.Fields.Count == 0)
            diagnostics.Add(new("VSIR206", $"Sum variant '{name}' representation must contain at least one field.", SemanticPath: $"{path}.representation"));
        if (!input.IsScalar && input.Fields.Count == 0)
            diagnostics.Add(new("VSIR207", $"Sum variant '{name}' input must contain at least one field or declare a scalar type.", SemanticPath: $"{path}.input"));

        ValidateRefineBindings(name, state, input, construction.Steps, diagnostics);
        ValidateDirectRepresentation(name, state, representation, diagnostics);

        return new(
            name,
            traits,
            refinedFrom,
            state,
            representation,
            null,
            construction,
            null);
    }

    private static ConstructionInput ParseInput(
        YamlMappingNode variant,
        string variantPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!variant.Children.TryGetValue(new YamlScalarNode("input"), out var node))
        {
            diagnostics.Add(new("VSIR111", "Transform semantics require variant input.", SemanticPath: $"{variantPath}.input"));
            return ConstructionInput.Product([]);
        }

        if (node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            return ConstructionInput.Scalar(new NamedVsirType(scalar.Value!));

        if (node is YamlMappingNode mapping)
            return ConstructionInput.Product(ReadFields(mapping, $"{variantPath}.input", diagnostics));

        diagnostics.Add(new("VSIR111", "Variant input must be either a scalar semantic type or a product mapping.", SemanticPath: $"{variantPath}.input"));
        return ConstructionInput.Product([]);
    }

    private static IReadOnlyList<ConstructionStep> ParseConstruction(
        YamlMappingNode variant,
        string variantPath,
        VsirValidationContext validationContext,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!variant.Children.TryGetValue(new YamlScalarNode("construction"), out var node))
            return [];
        if (node is not YamlSequenceNode sequence)
        {
            diagnostics.Add(new("VSIR002", "Variant construction must be an ordered sequence.", SemanticPath: $"{variantPath}.construction"));
            return [];
        }

        var result = new List<ConstructionStep>();
        for (var index = 0; index < sequence.Children.Count; index++)
        {
            var child = sequence.Children[index];
            var stepPath = $"{variantPath}.construction[{index}]";
            if (child is not YamlMappingNode step || step.Children.Count != 1 ||
                step.Children.Keys.Single() is not YamlScalarNode operation ||
                step.Children.Values.Single() is not YamlMappingNode payload)
            {
                diagnostics.Add(new("VSIR100", "Each construction step must contain exactly one admitted semantic operation.", SemanticPath: stepPath));
                continue;
            }

            switch (operation.Value)
            {
                case "ensure":
                    ParseEnsure(payload, stepPath, diagnostics, result);
                    break;
                case "normalize":
                {
                    var target = Scalar(payload, "target");
                    var intrinsic = Scalar(payload, "intrinsic");
                    if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(intrinsic))
                    {
                        diagnostics.Add(new("VSIR120", "Normalize requires target and intrinsic.", SemanticPath: stepPath));
                        break;
                    }
                    if (intrinsic != "trim" && !validationContext.SemanticExtensions.DeclaresNormalize(intrinsic))
                    {
                        diagnostics.Add(new("VSIR221", $"Unsupported normalize intrinsic '{intrinsic}'.", SemanticPath: stepPath));
                        break;
                    }
                    result.Add(new NormalizeStep(target, intrinsic));
                    break;
                }
                case "refine":
                    ParseRefine(payload, stepPath, diagnostics, result);
                    break;
                default:
                    diagnostics.Add(new("VSIR100", $"Unsupported sum-variant construction step '{operation.Value}'.", SemanticPath: stepPath));
                    break;
            }
        }

        return result;
    }

    private static void ParseEnsure(
        YamlMappingNode ensure,
        string stepPath,
        ICollection<VsirDiagnostic> diagnostics,
        ICollection<ConstructionStep> result)
    {
        if (!TryMapping(ensure, "condition", out var condition) ||
            !TryMapping(condition, "args", out var args))
        {
            diagnostics.Add(new("VSIR101", "Ensure step requires condition.args.", SemanticPath: stepPath));
            return;
        }

        var intrinsic = Scalar(condition, "intrinsic");
        SemanticExpression? value = null;
        if (args.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
        {
            value = ParseExpression(valueNode, $"{stepPath}.ensure.condition.args.value", diagnostics);
        }
        else if (args.Children.TryGetValue(new YamlScalarNode("values"), out var valuesNode) && valuesNode is YamlSequenceNode values)
        {
            var operands = values.Children
                .Select((item, index) => ParseExpression(item, $"{stepPath}.ensure.condition.args.values[{index}]", diagnostics))
                .Where(item => item is not null)
                .Cast<SemanticExpression>()
                .ToArray();
            if (operands.Length == values.Children.Count && operands.Length > 0)
                value = new SemanticIntrinsicExpression("sum-lengths", operands);
        }

        Condition? parsed = intrinsic switch
        {
            "non-empty" when value is not null => new NonEmptyCondition(value),
            "not-whitespace" when value is not null => new NotWhitespaceCondition(value),
            "length-at-most" when value is not null && TryInt(args, "max", out var max) => new LengthAtMostCondition(value, max),
            "length-between" when value is not null && TryInt(args, "min", out var min) && TryInt(args, "max", out var upper) => new LengthBetweenCondition(value, min, upper),
            _ => null
        };

        if (parsed is null)
        {
            diagnostics.Add(new("VSIR102", $"Unsupported or malformed intrinsic '{intrinsic}'.", SemanticPath: stepPath));
            return;
        }

        var failureMessage = TryMapping(ensure, "failure", out var failure)
            ? Scalar(failure, "message")
            : string.Empty;
        if (string.IsNullOrWhiteSpace(failureMessage))
        {
            diagnostics.Add(new("VSIR103", "Ensure step requires failure.message.", SemanticPath: stepPath));
            return;
        }

        result.Add(new EnsureStep(parsed, failureMessage));
    }

    private static void ParseRefine(
        YamlMappingNode refine,
        string stepPath,
        ICollection<VsirDiagnostic> diagnostics,
        ICollection<ConstructionStep> result)
    {
        if (!TryMapping(refine, "state", out var state))
        {
            diagnostics.Add(new("VSIR110", "Sum-variant refine currently requires state bindings.", SemanticPath: stepPath));
            return;
        }

        foreach (var pair in state.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value) ||
                pair.Value is not YamlScalarNode value || string.IsNullOrWhiteSpace(value.Value))
            {
                diagnostics.Add(new("VSIR126", "refine.state requires non-empty scalar field bindings.", SemanticPath: stepPath));
                continue;
            }
            result.Add(new RefineStep(value.Value!, "state." + key.Value));
        }
    }

    private static SemanticExpression? ParseExpression(
        YamlNode node,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            return new SemanticReferenceExpression(scalar.Value!);

        diagnostics.Add(new("VSIR128", $"Semantic expression '{path}' must currently be a non-empty reference.", SemanticPath: path));
        return null;
    }

    private static IReadOnlyList<Field> ReadFields(
        YamlMappingNode map,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var result = new List<Field>(map.Children.Count);
        foreach (var pair in map.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
            {
                diagnostics.Add(new("VSIR116", $"Semantic product '{path}' requires scalar field names.", SemanticPath: path));
                continue;
            }

            var type = ParseType(pair.Value, $"{path}.{key.Value}", diagnostics);
            if (type is not null)
                result.Add(new Field(key.Value!, type));
        }
        return result;
    }

    private static VsirType? ParseType(
        YamlNode node,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
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

    private static void ValidateRefineBindings(
        string variantName,
        ProductShape state,
        ConstructionInput input,
        IReadOnlyList<ConstructionStep> steps,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var inputFields = input.Fields.ToDictionary(x => "input." + x.Name, x => x.Type, StringComparer.Ordinal);
        foreach (var refine in steps.OfType<RefineStep>())
        {
            if (!refine.As.StartsWith("state.", StringComparison.Ordinal))
            {
                diagnostics.Add(new("VSIR230", $"Refine target must be a state reference, got '{refine.As}'.", SemanticPath: $"variants.{variantName}.construction"));
                continue;
            }

            var stateName = refine.As["state.".Length..];
            var stateField = state.Fields.SingleOrDefault(x => x.Name == stateName);
            if (stateField is null)
            {
                diagnostics.Add(new("VSIR232", $"Refine references unknown state field '{stateName}'.", SemanticPath: $"variants.{variantName}.construction"));
                continue;
            }

            if (input.IsScalar && refine.Value == "input")
                continue;
            if (!inputFields.TryGetValue(refine.Value, out var inputType))
            {
                diagnostics.Add(new("VSIR231", $"Refine value must reference variant input, got '{refine.Value}'.", SemanticPath: $"variants.{variantName}.construction"));
                continue;
            }
            if (inputType != stateField.Type)
                diagnostics.Add(new("VSIR233", $"Refine source type '{inputType}' does not match state.{stateName} type '{stateField.Type}'.", SemanticPath: $"variants.{variantName}.construction"));
        }
    }

    private static void ValidateDirectRepresentation(
        string variantName,
        ProductShape state,
        ProductShape representation,
        ICollection<VsirDiagnostic> diagnostics)
    {
        foreach (var field in representation.Fields)
        {
            var stateField = state.Fields.SingleOrDefault(x => x.Name == field.Name);
            if (stateField is null || stateField.Type != field.Type)
            {
                diagnostics.Add(new(
                    "VSIR210",
                    $"Cannot project variants.{variantName}.representation.{field.Name} deterministically from same-named state.",
                    SemanticPath: $"variants.{variantName}.representation.{field.Name}"));
            }
        }
    }

    private static IReadOnlyList<string> ReadScalarSequence(
        YamlMappingNode map,
        string key,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node))
            return [];
        if (node is not YamlSequenceNode sequence)
        {
            diagnostics.Add(new("VSIR112", $"'{path}' must be a sequence of scalar values.", SemanticPath: path));
            return [];
        }
        var result = new List<string>();
        foreach (var child in sequence.Children)
        {
            if (child is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                diagnostics.Add(new("VSIR112", $"'{path}' requires non-empty scalar values.", SemanticPath: path));
            else
                result.Add(scalar.Value!);
        }
        return result;
    }

    private static void RejectUnknownKeys(
        YamlMappingNode map,
        IReadOnlySet<string> allowed,
        string path,
        ICollection<VsirDiagnostic> diagnostics,
        string code)
    {
        foreach (var key in map.Children.Keys.OfType<YamlScalarNode>())
        {
            if (!string.IsNullOrWhiteSpace(key.Value) && !allowed.Contains(key.Value!))
                diagnostics.Add(new(code, $"Unsupported semantic '{key.Value}' under {path}.", SemanticPath: path == "root" ? key.Value : $"{path}.{key.Value}"));
        }
    }

    private static bool TryMapping(YamlMappingNode map, string key, out YamlMappingNode value)
    {
        value = null!;
        return map.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
               node is YamlMappingNode mapping &&
               (value = mapping) is not null;
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

    private static bool TryInt(YamlMappingNode map, string key, out int value) =>
        int.TryParse(Scalar(map, key), out value);

    private static VsirParseResult Failure(string code, string message) =>
        new(null, [new(code, message)]);
}
