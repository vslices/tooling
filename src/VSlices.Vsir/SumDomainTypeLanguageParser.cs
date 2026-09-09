using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

/// <summary>
/// Parses the canonical VSIR 0.1 sum-domain-type surface, including shared
/// state/representation and variant-local transform semantics.
/// </summary>
public static class SumDomainTypeLanguageParser
{
    private static readonly HashSet<string> RootKeys = new(StringComparer.Ordinal)
    {
        "vsir", "kind", "name", "classification", "shape",
        "state", "representation", "identity", "variants"
    };

    private static readonly HashSet<string> VariantKeys = new(StringComparer.Ordinal)
    {
        "traits", "refined-from", "state", "representation", "input", "construction", "equality"
    };

    private static readonly HashSet<string> SupportedClassifications =
        new(["value-object", "identifier", "entity", "aggregate-root"], StringComparer.Ordinal);

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

        var state = TryMapping(root, "state", out var stateNode)
            ? new ProductShape(ReadFields(stateNode, "state", allowFrom: true, allowMapping: false, diagnostics).Fields)
            : new ProductShape([]);

        var representationResult = TryMapping(root, "representation", out var representationNode)
            ? ReadFields(representationNode, "representation", allowFrom: true, allowMapping: true, diagnostics)
            : new ParsedFields([], new Dictionary<string, RepresentationProjection>(StringComparer.Ordinal));
        var representation = new ProductShape(representationResult.Fields);
        var rootMapping = representationResult.Mappings.Count == 0
            ? null
            : new RepresentationMapping(representationResult.Mappings);

        var identity = ParseIdentity(root, classification, state, diagnostics);
        ValidateRepresentation("representation", state, representation, rootMapping, diagnostics);

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

                var variant = ParseVariant(
                    variantKey.Value!, variantNode, state, validationContext, diagnostics);
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
            state,
            representation,
            rootMapping,
            new Construction(ConstructionInput.Product([]), []),
            null,
            variants,
            identity);

        return new(document, diagnostics);

        void Require(bool condition, string code, string message, string path)
        {
            if (!condition)
                diagnostics.Add(new(code, message, SemanticPath: path));
        }
    }

    private static IdentitySemantics? ParseIdentity(
        YamlMappingNode root,
        string classification,
        ProductShape state,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var requiresIdentity = classification is "entity" or "aggregate-root";
        if (!root.Children.ContainsKey(new YamlScalarNode("identity")))
        {
            if (requiresIdentity)
                diagnostics.Add(new("VSIR274", $"Classification '{classification}' requires identity semantics.", SemanticPath: "identity"));
            return null;
        }

        if (!TryMapping(root, "identity", out var identity))
        {
            diagnostics.Add(new("VSIR274", "Identity must be a mapping with type and from.", SemanticPath: "identity"));
            return null;
        }

        RejectUnknownKeys(identity, new HashSet<string>(["type", "from"], StringComparer.Ordinal), "identity", diagnostics, "VSIR104");
        if (!identity.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode))
        {
            diagnostics.Add(new("VSIR274", "Identity requires type.", SemanticPath: "identity.type"));
            return null;
        }

        var type = ParseType(typeNode, "identity.type", diagnostics);
        var from = Scalar(identity, "from");
        if (type is null || string.IsNullOrWhiteSpace(from))
        {
            diagnostics.Add(new("VSIR274", "Identity requires a valid type and non-empty from reference.", SemanticPath: "identity"));
            return null;
        }

        if (!TryResolveStateRoot(from, state, out _))
            diagnostics.Add(new("VSIR275", $"Identity source '{from}' must originate from declared root state.", SemanticPath: "identity.from"));

        return new IdentitySemantics(type, from);
    }

    private static DomainTypeVariant? ParseVariant(
        string name,
        YamlMappingNode node,
        ProductShape sharedState,
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
            ? new ProductShape(ReadFields(stateNode, $"{path}.state", allowFrom: true, allowMapping: false, diagnostics).Fields)
            : new ProductShape([]);
        var representationResult = TryMapping(node, "representation", out var representationNode)
            ? ReadFields(representationNode, $"{path}.representation", allowFrom: true, allowMapping: true, diagnostics)
            : new ParsedFields([], new Dictionary<string, RepresentationProjection>(StringComparer.Ordinal));
        var representation = new ProductShape(representationResult.Fields);
        var representationMapping = representationResult.Mappings.Count == 0
            ? null
            : new RepresentationMapping(representationResult.Mappings);
        var input = ParseInput(node, path, diagnostics);
        var construction = new Construction(input, ParseConstruction(node, path, validationContext, diagnostics));

        if (!input.IsScalar && input.Fields.Count == 0)
            diagnostics.Add(new("VSIR207", $"Sum variant '{name}' input must contain at least one field or declare a scalar type.", SemanticPath: $"{path}.input"));

        var effectiveState = new ProductShape(sharedState.Fields.Concat(state.Fields).ToArray());
        ValidateRefineBindings(name, effectiveState, input, construction.Steps, diagnostics);
        ValidateRepresentation($"{path}.representation", effectiveState, representation, representationMapping, diagnostics);

        return new(
            name,
            traits,
            refinedFrom,
            state,
            representation,
            representationMapping,
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
            return ConstructionInput.Product(ReadFields(mapping, $"{variantPath}.input", allowFrom: false, allowMapping: false, diagnostics).Fields);

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
        if (!args.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
        {
            diagnostics.Add(new("VSIR102", $"Unsupported or malformed intrinsic '{intrinsic}': missing value expression.", SemanticPath: stepPath));
            return;
        }

        var value = ParseExpression(valueNode, $"{stepPath}.ensure.condition.args.value", diagnostics);
        Condition? parsed = value is null
            ? null
            : intrinsic switch
            {
                "non-empty" => new NonEmptyCondition(value),
                "not-whitespace" => new NotWhitespaceCondition(value),
                "length-at-most" when TryInt(args, "max", out var max) => new LengthAtMostCondition(value, max),
                "length-between" when TryInt(args, "min", out var min) && TryInt(args, "max", out var upper) => new LengthBetweenCondition(value, min, upper),
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

        if (node is not YamlMappingNode mapping || mapping.Children.Count == 0)
        {
            diagnostics.Add(new("VSIR128", $"Semantic expression '{path}' must be a non-empty reference or intrinsic mapping.", SemanticPath: path));
            return null;
        }

        var intrinsic = OptionalScalar(mapping, "intrinsic");
        if (intrinsic is null)
        {
            diagnostics.Add(new("VSIR128", $"Semantic expression '{path}' currently requires intrinsic and values.", SemanticPath: path));
            return null;
        }

        RejectUnknownKeys(mapping, new HashSet<string>(["intrinsic", "values"], StringComparer.Ordinal), path, diagnostics, "VSIR104");
        if (!mapping.Children.TryGetValue(new YamlScalarNode("values"), out var valuesNode) ||
            valuesNode is not YamlSequenceNode values || values.Children.Count == 0)
        {
            diagnostics.Add(new("VSIR128", $"Intrinsic semantic expression '{path}' requires at least one value expression.", SemanticPath: path));
            return null;
        }

        var parsedValues = new List<SemanticExpression>(values.Children.Count);
        for (var index = 0; index < values.Children.Count; index++)
        {
            var parsed = ParseExpression(values.Children[index], $"{path}.values[{index}]", diagnostics);
            if (parsed is not null)
                parsedValues.Add(parsed);
        }

        return parsedValues.Count == values.Children.Count
            ? new SemanticIntrinsicExpression(intrinsic, parsedValues)
            : null;
    }

    private static ParsedFields ReadFields(
        YamlMappingNode map,
        string path,
        bool allowFrom,
        bool allowMapping,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var fields = new List<Field>(map.Children.Count);
        var mappings = new Dictionary<string, RepresentationProjection>(StringComparer.Ordinal);

        foreach (var pair in map.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
            {
                diagnostics.Add(new("VSIR116", $"Semantic product '{path}' requires scalar field names.", SemanticPath: path));
                continue;
            }

            var fieldName = key.Value!;
            string? from = null;
            VsirType? type;

            if (pair.Value is YamlMappingNode declaration &&
                declaration.Children.ContainsKey(new YamlScalarNode("type")))
            {
                var allowed = new HashSet<string>(StringComparer.Ordinal) { "type" };
                if (allowFrom) allowed.Add("from");
                if (allowMapping) allowed.Add("mapping");
                RejectUnknownKeys(declaration, allowed, $"{path}.{fieldName}", diagnostics, "VSIR104");

                type = declaration.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode)
                    ? ParseType(typeNode, $"{path}.{fieldName}.type", diagnostics)
                    : null;
                if (allowFrom)
                    from = OptionalScalar(declaration, "from");
                if (allowMapping && declaration.Children.TryGetValue(new YamlScalarNode("mapping"), out var mappingNode))
                {
                    var projection = ParseProjection(mappingNode, $"{path}.{fieldName}.mapping", diagnostics);
                    if (projection is not null)
                        mappings[fieldName] = projection;
                }
            }
            else
            {
                type = ParseType(pair.Value, $"{path}.{fieldName}", diagnostics);
            }

            if (type is not null)
                fields.Add(new Field(fieldName, type, from));
        }

        return new(fields, mappings);
    }

    private static RepresentationProjection? ParseProjection(
        YamlNode node,
        string path,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            return new ReferenceProjection(scalar.Value!);
        if (node is not YamlMappingNode mapping || mapping.Children.Count == 0)
        {
            diagnostics.Add(new("VSIR114", $"Semantic expression '{path}' must be a non-empty mapping or reference.", SemanticPath: path));
            return null;
        }

        if (mapping.Children.ContainsKey(new YamlScalarNode("stringify")))
        {
            RejectUnknownKeys(mapping, new HashSet<string>(["stringify"], StringComparer.Ordinal), path, diagnostics, "VSIR104");
            var value = Scalar(mapping, "stringify");
            if (string.IsNullOrWhiteSpace(value))
            {
                diagnostics.Add(new("VSIR115", $"{path}: stringify requires a semantic reference.", SemanticPath: path));
                return null;
            }
            return new StringifyProjection(value);
        }

        if (mapping.Children.TryGetValue(new YamlScalarNode("represent"), out var represented))
        {
            RejectUnknownKeys(mapping, new HashSet<string>(["represent"], StringComparer.Ordinal), path, diagnostics, "VSIR104");
            var value = ParseProjection(represented, path + ".represent", diagnostics);
            return value is null ? null : new RepresentProjection(value);
        }

        if (TryMapping(mapping, "map", out var map))
        {
            RejectUnknownKeys(mapping, new HashSet<string>(["map"], StringComparer.Ordinal), path, diagnostics, "VSIR104");
            RejectUnknownKeys(map, new HashSet<string>(["source", "bind", "value"], StringComparer.Ordinal), path + ".map", diagnostics, "VSIR104");
            if (!map.Children.TryGetValue(new YamlScalarNode("source"), out var sourceNode) ||
                !map.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
            {
                diagnostics.Add(new("VSIR115", $"{path}: map requires source, bind, and value.", SemanticPath: path));
                return null;
            }
            var source = ParseProjection(sourceNode, path + ".map.source", diagnostics);
            var value = ParseProjection(valueNode, path + ".map.value", diagnostics);
            var bind = Scalar(map, "bind");
            if (source is null || value is null || string.IsNullOrWhiteSpace(bind))
            {
                diagnostics.Add(new("VSIR115", $"{path}: map requires source, bind, and value.", SemanticPath: path));
                return null;
            }
            return new MapProjection(source, bind, value);
        }

        diagnostics.Add(new("VSIR115", $"{path}: unknown semantic projection expression.", SemanticPath: path));
        return null;
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
            var inner = ParseType(mapping.Children.Values.Single(), path + "." + constructor.Value, diagnostics);
            return inner is null ? null : new UnaryVsirType(constructor.Value!, inner);
        }

        diagnostics.Add(new("VSIR115", $"Invalid semantic type declaration at '{path}'.", SemanticPath: path));
        return null;
    }

    private static void ValidateRefineBindings(
        string variantName,
        ProductShape effectiveState,
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
            var stateField = effectiveState.Fields.SingleOrDefault(x => x.Name == stateName);
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

    private static void ValidateRepresentation(
        string path,
        ProductShape effectiveState,
        ProductShape representation,
        RepresentationMapping? mapping,
        ICollection<VsirDiagnostic> diagnostics)
    {
        foreach (var field in representation.Fields)
        {
            if (mapping?.Fields.ContainsKey(field.Name) == true)
                continue;

            if (field.From is not null)
            {
                if (!TryResolveStateRoot(field.From, effectiveState, out _))
                    diagnostics.Add(new("VSIR210", $"Representation source '{field.From}' does not originate from declared state.", SemanticPath: $"{path}.{field.Name}"));
                continue;
            }

            var stateField = effectiveState.Fields.SingleOrDefault(x => x.Name == field.Name);
            if (stateField is null || stateField.Type != field.Type)
            {
                diagnostics.Add(new(
                    "VSIR210",
                    $"Cannot project {path}.{field.Name} deterministically from same-named state.",
                    SemanticPath: $"{path}.{field.Name}"));
            }
        }
    }

    private static bool TryResolveStateRoot(string reference, ProductShape state, out Field? field)
    {
        field = null;
        if (!reference.StartsWith("state.", StringComparison.Ordinal))
            return false;
        var tail = reference["state.".Length..];
        var separator = tail.IndexOf('.', StringComparison.Ordinal);
        var rootName = separator < 0 ? tail : tail[..separator];
        field = state.Fields.SingleOrDefault(x => x.Name == rootName);
        return field is not null;
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

    private sealed record ParsedFields(
        IReadOnlyList<Field> Fields,
        IReadOnlyDictionary<string, RepresentationProjection> Mappings);
}
