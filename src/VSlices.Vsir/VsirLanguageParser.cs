using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

/// <summary>
/// Parses the canonical VSIR 0.1 surface.
/// Pre-normalized experimental grammars are intentionally not compatibility-parsed.
/// </summary>
public static class VsirLanguageParser
{
    private static readonly HashSet<string> CanonicalRootKeys = new(StringComparer.Ordinal)
    {
        "vsir",
        "kind",
        "name",
        "classification",
        "shape",
        "traits",
        "refined-from",
        "state",
        "representation",
        "input",
        "construction",
        "equality"
    };

    public static VsirParseResult Parse(
        string text,
        VsirValidationContext? validationContext = null)
    {
        validationContext ??= VsirValidationContext.Empty;

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

        return ParseCanonical(root, validationContext);
    }

    private static VsirParseResult ParseCanonical(
        YamlMappingNode root,
        VsirValidationContext validationContext)
    {
        var diagnostics = new List<VsirDiagnostic>();
        RejectUnknownKeys(root, CanonicalRootKeys, "root", diagnostics, rootDiagnostic: true);

        var version = Scalar(root, "vsir");
        var kind = Scalar(root, "kind");
        var name = Scalar(root, "name");
        var classification = Scalar(root, "classification");
        var shape = Scalar(root, "shape");
        var traits = ReadScalarSequence(root, "traits", "traits", diagnostics);
        var refinedFrom = OptionalScalar(root, "refined-from");

        var state = TryMapping(root, "state", out var stateNode)
            ? new ProductShape(ReadFields(stateNode, "state", allowFrom: true, allowMapping: false, diagnostics).Fields)
            : new ProductShape([]);

        var representationResult = TryMapping(root, "representation", out var representationNode)
            ? ReadFields(representationNode, "representation", allowFrom: true, allowMapping: true, diagnostics)
            : new ParsedFields([], new Dictionary<string, RepresentationProjection>(StringComparer.Ordinal));

        var input = ParseRootInput(root, diagnostics);
        var steps = ParseConstruction(root, diagnostics);
        var equality = ParseEquality(root, diagnostics);

        var document = new DomainTypeVsir(
            version,
            kind,
            name,
            classification,
            shape,
            traits,
            refinedFrom,
            state,
            new ProductShape(representationResult.Fields),
            representationResult.Mappings.Count == 0
                ? null
                : new RepresentationMapping(representationResult.Mappings),
            new Construction(input, steps),
            equality);

        diagnostics.AddRange(DomainTypeValidator.Validate(document, validationContext));
        return new(document, diagnostics);
    }

    private static ConstructionInput ParseRootInput(
        YamlMappingNode root,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode("input"), out var node))
        {
            diagnostics.Add(new("VSIR111", "Transform semantics require root input."));
            return ConstructionInput.Product([]);
        }

        if (node is YamlScalarNode scalar)
        {
            if (string.IsNullOrWhiteSpace(scalar.Value))
            {
                diagnostics.Add(new("VSIR111", "Scalar input requires a semantic type."));
                return ConstructionInput.Product([]);
            }

            return ConstructionInput.Scalar(new NamedVsirType(scalar.Value!));
        }

        if (node is YamlMappingNode mapping)
        {
            var parsed = ReadFields(mapping, "input", allowFrom: false, allowMapping: false, diagnostics);
            return ConstructionInput.Product(parsed.Fields);
        }

        diagnostics.Add(new("VSIR111", "Input must be either a scalar semantic type or a product mapping."));
        return ConstructionInput.Product([]);
    }

    private static IReadOnlyList<ConstructionStep> ParseConstruction(
        YamlMappingNode root,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode("construction"), out var node))
            return [];

        if (node is not YamlSequenceNode sequence)
        {
            diagnostics.Add(new("VSIR002", "Canonical construction, when declared, must be an ordered sequence."));
            return [];
        }

        var result = new List<ConstructionStep>();
        foreach (var child in sequence.Children)
        {
            if (child is not YamlMappingNode step || step.Children.Count != 1)
            {
                diagnostics.Add(new("VSIR100", "Each construction step must contain exactly one admitted semantic operation."));
                continue;
            }

            var operation = step.Children.Keys.Single() as YamlScalarNode;
            if (operation is null || string.IsNullOrWhiteSpace(operation.Value) ||
                step.Children.Values.Single() is not YamlMappingNode payload)
            {
                diagnostics.Add(new("VSIR100", "Construction steps require a scalar operation name and mapping payload."));
                continue;
            }

            switch (operation.Value)
            {
                case "normalize":
                    ParseNormalize(payload, diagnostics, result);
                    break;
                case "ensure":
                    ParseEnsure(payload, diagnostics, result);
                    break;
                case "resolve":
                    ParseResolve(payload, diagnostics, result);
                    break;
                case "apply":
                {
                    var apply = ParseApply(payload, diagnostics);
                    if (apply is not null)
                        result.Add(apply);
                    break;
                }
                case "refine":
                    ParseRefine(payload, diagnostics, result);
                    break;
                default:
                    diagnostics.Add(new("VSIR100", $"Unsupported construction step '{operation.Value}'."));
                    break;
            }
        }

        return result;
    }

    private static void ParseNormalize(
        YamlMappingNode normalize,
        ICollection<VsirDiagnostic> diagnostics,
        ICollection<ConstructionStep> result)
    {
        RejectUnknownKeys(normalize, ["target", "intrinsic"], "construction[].normalize", diagnostics);
        var target = Scalar(normalize, "target");
        var intrinsic = Scalar(normalize, "intrinsic");
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(intrinsic))
        {
            diagnostics.Add(new("VSIR120", "Normalize requires target and intrinsic."));
            return;
        }

        result.Add(new NormalizeStep(target, intrinsic));
    }

    private static void ParseEnsure(
        YamlMappingNode ensure,
        ICollection<VsirDiagnostic> diagnostics,
        ICollection<ConstructionStep> result)
    {
        RejectUnknownKeys(ensure, ["condition", "failure"], "construction[].ensure", diagnostics);
        if (!TryMapping(ensure, "condition", out var condition))
        {
            diagnostics.Add(new("VSIR101", "Ensure step requires condition."));
            return;
        }

        RejectUnknownKeys(condition, ["intrinsic", "args"], "construction[].ensure.condition", diagnostics);
        var intrinsic = Scalar(condition, "intrinsic");
        if (!TryMapping(condition, "args", out var args))
        {
            diagnostics.Add(new("VSIR101", "Ensure condition requires args."));
            return;
        }

        var value = Scalar(args, "value");
        Condition? parsed = intrinsic switch
        {
            "non-empty" => new NonEmptyCondition(value),
            "not-whitespace" => new NotWhitespaceCondition(value),
            "length-at-most" when TryInt(args, "max", out var max) => new LengthAtMostCondition(value, max),
            "length-between" when TryInt(args, "min", out var min) && TryInt(args, "max", out var upper) =>
                new LengthBetweenCondition(value, min, upper),
            _ => null
        };

        if (parsed is null)
        {
            diagnostics.Add(new("VSIR102", $"Unsupported or malformed intrinsic '{intrinsic}'."));
            return;
        }

        string failureMessage;
        if (TryMapping(ensure, "failure", out var failure))
        {
            RejectUnknownKeys(failure, ["message"], "construction[].ensure.failure", diagnostics);
            failureMessage = Scalar(failure, "message");
        }
        else
        {
            failureMessage = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(failureMessage))
        {
            diagnostics.Add(new("VSIR103", "Ensure step requires failure.message."));
            return;
        }

        result.Add(new EnsureStep(parsed, failureMessage));
    }

    private static void ParseResolve(
        YamlMappingNode resolve,
        ICollection<VsirDiagnostic> diagnostics,
        ICollection<ConstructionStep> result)
    {
        RejectUnknownKeys(resolve, ["source", "id", "as", "failure"], "construction[].resolve", diagnostics);
        var source = Scalar(resolve, "source");
        var id = Scalar(resolve, "id");
        var binding = Scalar(resolve, "as");
        string failureMessage;
        if (TryMapping(resolve, "failure", out var failure))
        {
            RejectUnknownKeys(failure, ["message"], "construction[].resolve.failure", diagnostics);
            failureMessage = Scalar(failure, "message");
        }
        else
        {
            failureMessage = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(binding) || string.IsNullOrWhiteSpace(failureMessage))
        {
            diagnostics.Add(new("VSIR125", "Resolve requires source, id, as, and failure.message."));
            return;
        }

        result.Add(new ResolveStep(source, id, binding, failureMessage));
    }

    private static ApplyStep? ParseApply(
        YamlMappingNode apply,
        ICollection<VsirDiagnostic> diagnostics)
    {
        RejectUnknownKeys(apply, ["over", "input", "as"], "construction[].apply", diagnostics);
        var over = Scalar(apply, "over");
        var binding = Scalar(apply, "as");
        if (string.IsNullOrWhiteSpace(over) || string.IsNullOrWhiteSpace(binding) ||
            !TryMapping(apply, "input", out var input))
        {
            diagnostics.Add(new("VSIR119", "Apply requires over, input, and as."));
            return null;
        }

        var hasSource = input.Children.ContainsKey(new YamlScalarNode("source"));
        var hasMap = input.Children.ContainsKey(new YamlScalarNode("map"));
        if (hasSource || hasMap)
        {
            RejectUnknownKeys(input, ["source", "map"], "construction[].apply.input", diagnostics);
            var source = Scalar(input, "source");
            if (string.IsNullOrWhiteSpace(source) || !TryMapping(input, "map", out var map))
            {
                diagnostics.Add(new("VSIR121", "Mapped apply input requires source and map."));
                return null;
            }

            var fields = ReadScalarMap(map, "construction[].apply.input.map", diagnostics);
            if (fields.Count == 0)
            {
                diagnostics.Add(new("VSIR122", "Mapped apply input requires at least one mapped field."));
                return null;
            }

            return new ApplyStep(over, new MappedApplyInput(source, fields), binding);
        }

        var direct = ReadScalarMap(input, "construction[].apply.input", diagnostics);
        if (direct.Count == 0)
        {
            diagnostics.Add(new("VSIR123", "Direct apply input requires at least one mapped field."));
            return null;
        }

        return new ApplyStep(over, new DirectApplyInput(direct), binding);
    }

    private static void ParseRefine(
        YamlMappingNode refine,
        ICollection<VsirDiagnostic> diagnostics,
        ICollection<ConstructionStep> result)
    {
        if (TryMapping(refine, "state", out var state))
        {
            RejectUnknownKeys(refine, ["state"], "construction[].refine", diagnostics);
            foreach (var pair in state.Children)
            {
                if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value) ||
                    pair.Value is not YamlScalarNode value || string.IsNullOrWhiteSpace(value.Value))
                {
                    diagnostics.Add(new("VSIR126", "refine.state requires non-empty scalar field bindings."));
                    continue;
                }

                result.Add(new RefineStep(value.Value!, "state." + key.Value));
            }
            return;
        }

        var intrinsic = OptionalScalar(refine, "intrinsic");
        if (intrinsic is not null)
        {
            RejectUnknownKeys(refine, ["intrinsic", "value", "as", "failure"], "construction[].refine", diagnostics);
            var intrinsicValue = Scalar(refine, "value");
            var outputs = TryMapping(refine, "as", out var asMap)
                ? ReadScalarMap(asMap, "construction[].refine.as", diagnostics)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            var failureMessage = TryMapping(refine, "failure", out var failure)
                ? Scalar(failure, "message")
                : string.Empty;
            if (TryMapping(refine, "failure", out failure))
                RejectUnknownKeys(failure, ["message"], "construction[].refine.failure", diagnostics);

            if (string.IsNullOrWhiteSpace(intrinsicValue) || outputs.Count == 0 || string.IsNullOrWhiteSpace(failureMessage))
            {
                diagnostics.Add(new("VSIR127", "Intrinsic refine requires intrinsic, value, non-empty as bindings, and failure.message."));
                return;
            }

            result.Add(new IntrinsicRefineStep(intrinsic, intrinsicValue, outputs, failureMessage));
            return;
        }

        RejectUnknownKeys(refine, ["value", "as"], "construction[].refine", diagnostics);
        var valueRef = Scalar(refine, "value");
        var target = Scalar(refine, "as");
        if (string.IsNullOrWhiteSpace(valueRef) || string.IsNullOrWhiteSpace(target))
        {
            diagnostics.Add(new("VSIR110", "Refine requires state, intrinsic refinement, or value/as semantics."));
            return;
        }

        result.Add(new RefineStep(valueRef, target));
    }

    private static ParsedFields ReadFields(
        YamlMappingNode map,
        string semanticPath,
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
                diagnostics.Add(new("VSIR116", $"Semantic product '{semanticPath}' requires scalar field names."));
                continue;
            }

            var fieldName = key.Value!;
            string? from = null;
            VsirType? type;

            if (pair.Value is YamlMappingNode declaration &&
                declaration.Children.ContainsKey(new YamlScalarNode("type")))
            {
                var allowed = new List<string> { "type" };
                if (allowFrom) allowed.Add("from");
                if (allowMapping) allowed.Add("mapping");
                RejectUnknownKeys(declaration, allowed, $"{semanticPath}.{fieldName}", diagnostics);

                type = declaration.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode)
                    ? ParseType(typeNode, $"{semanticPath}.{fieldName}.type", diagnostics)
                    : null;

                if (allowFrom)
                    from = OptionalScalar(declaration, "from");

                if (allowMapping && declaration.Children.TryGetValue(new YamlScalarNode("mapping"), out var mappingNode))
                {
                    var projection = ParseProjection(mappingNode, $"{semanticPath}.{fieldName}.mapping", diagnostics);
                    if (projection is not null)
                        mappings[fieldName] = projection;
                }
            }
            else
            {
                type = ParseType(pair.Value, $"{semanticPath}.{fieldName}", diagnostics);
            }

            if (type is not null)
                fields.Add(new Field(fieldName, type, from));
        }

        return new(fields, mappings);
    }

    private static RepresentationProjection? ParseProjection(
        YamlNode node,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            return new ReferenceProjection(scalar.Value!);

        if (node is not YamlMappingNode mapping || mapping.Children.Count == 0)
        {
            diagnostics.Add(new("VSIR114", $"Semantic expression '{semanticPath}' must be a non-empty mapping or reference."));
            return null;
        }

        if (mapping.Children.ContainsKey(new YamlScalarNode("stringify")))
        {
            RejectUnknownKeys(mapping, ["stringify"], semanticPath, diagnostics);
            var value = Scalar(mapping, "stringify");
            return string.IsNullOrWhiteSpace(value)
                ? InvalidProjection("stringify requires a semantic reference.")
                : new StringifyProjection(value);
        }

        if (mapping.Children.TryGetValue(new YamlScalarNode("represent"), out var represented))
        {
            RejectUnknownKeys(mapping, ["represent"], semanticPath, diagnostics);
            var value = ParseProjection(represented, semanticPath + ".represent", diagnostics);
            return value is null ? null : new RepresentProjection(value);
        }

        if (TryMapping(mapping, "select", out var select))
        {
            RejectUnknownKeys(mapping, ["select"], semanticPath, diagnostics);
            RejectUnknownKeys(select, ["source", "field"], semanticPath + ".select", diagnostics);
            if (!select.Children.TryGetValue(new YamlScalarNode("source"), out var sourceNode))
                return InvalidProjection("select requires source.");
            var source = ParseProjection(sourceNode, semanticPath + ".select.source", diagnostics);
            var field = Scalar(select, "field");
            return source is null || string.IsNullOrWhiteSpace(field)
                ? InvalidProjection("select requires source and field.")
                : new SelectProjection(source, field);
        }

        if (TryMapping(mapping, "map", out var map))
        {
            RejectUnknownKeys(mapping, ["map"], semanticPath, diagnostics);
            RejectUnknownKeys(map, ["source", "bind", "value"], semanticPath + ".map", diagnostics);
            if (!map.Children.TryGetValue(new YamlScalarNode("source"), out var sourceNode) ||
                !map.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
                return InvalidProjection("map requires source, bind, and value.");

            var source = ParseProjection(sourceNode, semanticPath + ".map.source", diagnostics);
            var value = ParseProjection(valueNode, semanticPath + ".map.value", diagnostics);
            var bind = Scalar(map, "bind");
            return source is null || value is null || string.IsNullOrWhiteSpace(bind)
                ? InvalidProjection("map requires source, bind, and value.")
                : new MapProjection(source, bind, value);
        }

        var intrinsic = OptionalScalar(mapping, "intrinsic");
        if (intrinsic is not null)
        {
            RejectUnknownKeys(mapping, ["intrinsic", "values"], semanticPath, diagnostics);
            if (!TrySequence(mapping, "values", out var values))
                return InvalidProjection("intrinsic projection requires values.");

            var parsedValues = values.Children
                .Select((value, index) => ParseProjection(value, $"{semanticPath}.values[{index}]", diagnostics))
                .Where(value => value is not null)
                .Cast<RepresentationProjection>()
                .ToArray();
            return new IntrinsicProjection(intrinsic, parsedValues);
        }

        return InvalidProjection("Unknown semantic projection expression.");

        RepresentationProjection? InvalidProjection(string message)
        {
            diagnostics.Add(new("VSIR115", $"{semanticPath}: {message}"));
            return null;
        }
    }

    private static EqualitySemantics? ParseEquality(
        YamlMappingNode root,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!root.Children.ContainsKey(new YamlScalarNode("equality")))
            return null;
        if (!TryMapping(root, "equality", out var equality))
        {
            diagnostics.Add(new("VSIR105", "Equality must be a mapping."));
            return null;
        }

        RejectUnknownKeys(equality, ["intrinsic", "over", "by"], "equality", diagnostics);
        var intrinsic = OptionalScalar(equality, "intrinsic");
        var over = OptionalScalar(equality, "over");
        var by = Scalar(equality, "by");
        if (string.IsNullOrWhiteSpace(by) || (intrinsic is null) == (over is null))
        {
            diagnostics.Add(new("VSIR106", "Equality requires by and exactly one of intrinsic or over."));
            return null;
        }

        return new EqualitySemantics(intrinsic, over, by);
    }

    private static VsirType? ParseType(
        YamlNode node,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (node is YamlScalarNode scalar)
        {
            if (string.IsNullOrWhiteSpace(scalar.Value))
            {
                diagnostics.Add(new("VSIR117", $"Semantic type '{semanticPath}' requires a non-empty type reference."));
                return null;
            }
            return new NamedVsirType(scalar.Value!);
        }

        if (node is not YamlMappingNode mapping || mapping.Children.Count != 1)
        {
            diagnostics.Add(new("VSIR118", $"Semantic type '{semanticPath}' must be a scalar reference or a single unary type-constructor mapping."));
            return null;
        }

        var pair = mapping.Children.Single();
        if (pair.Key is not YamlScalarNode constructor || string.IsNullOrWhiteSpace(constructor.Value))
        {
            diagnostics.Add(new("VSIR118", $"Semantic type '{semanticPath}' requires a scalar unary type-constructor name."));
            return null;
        }

        var value = ParseType(pair.Value, semanticPath + "." + constructor.Value, diagnostics);
        return value is null ? null : new UnaryVsirType(constructor.Value!, value);
    }

    private static IReadOnlyDictionary<string, string> ReadScalarMap(
        YamlMappingNode map,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in map.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value) ||
                pair.Value is not YamlScalarNode value || string.IsNullOrWhiteSpace(value.Value))
            {
                diagnostics.Add(new("VSIR124", $"Semantic mapping '{semanticPath}' requires non-empty scalar keys and values."));
                continue;
            }
            values[key.Value!] = value.Value!;
        }
        return values;
    }

    private static IReadOnlyList<string> ReadScalarSequence(
        YamlMappingNode node,
        string key,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!TrySequence(node, key, out var sequence))
            return [];

        var result = new List<string>();
        foreach (var child in sequence.Children)
        {
            if (child is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                diagnostics.Add(new("VSIR107", $"Semantic sequence '{semanticPath}' requires non-empty scalar entries."));
                continue;
            }
            result.Add(scalar.Value!);
        }
        return result;
    }

    private static void RejectUnknownKeys(
        YamlMappingNode mapping,
        IEnumerable<string> allowedKeys,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics,
        bool rootDiagnostic = false)
    {
        var allowed = allowedKeys.ToHashSet(StringComparer.Ordinal);
        foreach (var keyNode in mapping.Children.Keys)
        {
            if (keyNode is not YamlScalarNode scalar)
            {
                diagnostics.Add(new("VSIR104", $"Unsupported non-scalar semantic key in '{semanticPath}'."));
                continue;
            }

            var key = scalar.Value ?? string.Empty;
            if (!allowed.Contains(key))
            {
                diagnostics.Add(new(
                    "VSIR104",
                    rootDiagnostic
                        ? $"Unsupported root semantic '{key}'."
                        : $"Unsupported semantic '{semanticPath}.{key}'."));
            }
        }
    }

    private static string Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value ?? string.Empty
            : string.Empty;

    private static string? OptionalScalar(YamlMappingNode node, string key)
    {
        var value = Scalar(node, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool TryInt(YamlMappingNode node, string key, out int value) =>
        int.TryParse(Scalar(node, key), out value);

    private static bool TryMapping(YamlMappingNode node, string key, out YamlMappingNode mapping)
    {
        if (node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlMappingNode result)
        {
            mapping = result;
            return true;
        }
        mapping = null!;
        return false;
    }

    private static bool TrySequence(YamlMappingNode node, string key, out YamlSequenceNode sequence)
    {
        if (node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlSequenceNode result)
        {
            sequence = result;
            return true;
        }
        sequence = null!;
        return false;
    }

    private static VsirParseResult Failure(string code, string message) =>
        new(null, [new VsirDiagnostic(code, message)]);

    private sealed record ParsedFields(
        IReadOnlyList<Field> Fields,
        IReadOnlyDictionary<string, RepresentationProjection> Mappings);
}
