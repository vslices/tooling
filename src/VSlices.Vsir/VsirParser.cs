using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

public static class VsirParser
{
    private static readonly string[] SupportedRootKeys =
    [
        "vsir",
        "kind",
        "name",
        "classification",
        "shape",
        "traits",
        "refined-from",
        "state",
        "representation",
        "representation-mapping",
        "construction",
        "equality"
    ];

    public static VsirParseResult Parse(
        string text,
        VsirValidationContext? validationContext = null)
    {
        validationContext ??= VsirValidationContext.Empty;
        var diagnostics = new List<VsirDiagnostic>();

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));

            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return Failure("VSIR001", "Expected one YAML mapping document.");

            RejectUnknownKeys(root, SupportedRootKeys, "root", diagnostics, rootDiagnostic: true);

            var version = Scalar(root, "vsir");
            var kind = Scalar(root, "kind");
            var name = Scalar(root, "name");
            var classification = Scalar(root, "classification");
            var shape = Scalar(root, "shape");
            var traits = ReadScalarSequence(root, "traits", "traits", diagnostics);
            var refinedFrom = OptionalScalar(root, "refined-from");
            var state = Product(root, "state");
            var representation = Product(root, "representation");
            var representationMapping = ParseRepresentationMapping(root, diagnostics);
            var equality = ParseEquality(root, diagnostics);

            if (!TryMapping(root, "construction", out var constructionNode))
                return Failure("VSIR002", "Missing construction mapping.");

            RejectUnknownKeys(constructionNode, ["input", "steps"], "construction", diagnostics);

            var input = ParseConstructionInput(constructionNode, diagnostics);
            var steps = new List<ConstructionStep>();

            foreach (var stepNode in ReadMappingSequence(
                         constructionNode,
                         "steps",
                         "construction.steps",
                         diagnostics))
            {
                if (TryMapping(stepNode, "normalize", out var normalize))
                {
                    RejectUnknownKeys(stepNode, ["normalize"], "construction.steps[]", diagnostics);
                    RejectUnknownKeys(
                        normalize,
                        ["target", "intrinsic"],
                        "construction.steps[].normalize",
                        diagnostics);

                    var target = Scalar(normalize, "target");
                    var normalizeIntrinsic = Scalar(normalize, "intrinsic");
                    if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(normalizeIntrinsic))
                    {
                        diagnostics.Add(new(
                            "VSIR109",
                            "Normalize step requires both 'target' and 'intrinsic'."));
                        continue;
                    }

                    steps.Add(new NormalizeStep(target, normalizeIntrinsic));
                    continue;
                }

                if (TryMapping(stepNode, "refine", out var refine))
                {
                    RejectUnknownKeys(stepNode, ["refine"], "construction.steps[]", diagnostics);
                    RejectUnknownKeys(
                        refine,
                        ["value", "as"],
                        "construction.steps[].refine",
                        diagnostics);

                    var refineValue = Scalar(refine, "value");
                    var refineTarget = Scalar(refine, "as");
                    if (string.IsNullOrWhiteSpace(refineValue) || string.IsNullOrWhiteSpace(refineTarget))
                    {
                        diagnostics.Add(new(
                            "VSIR110",
                            "Refine step requires both 'value' and 'as'."));
                        continue;
                    }

                    steps.Add(new RefineStep(refineValue, refineTarget));
                    continue;
                }

                if (!TryMapping(stepNode, "ensure", out var ensure))
                {
                    diagnostics.Add(new(
                        "VSIR100",
                        "Only construction steps 'normalize', 'ensure', and 'refine' are supported by the experimental parser."));
                    continue;
                }

                RejectUnknownKeys(stepNode, ["ensure"], "construction.steps[]", diagnostics);
                RejectUnknownKeys(
                    ensure,
                    ["condition", "failure"],
                    "construction.steps[].ensure",
                    diagnostics);

                if (!TryMapping(ensure, "condition", out var conditionNode))
                {
                    diagnostics.Add(new("VSIR101", "Ensure step requires condition."));
                    continue;
                }

                var intrinsic = Scalar(conditionNode, "intrinsic");
                RejectUnknownKeys(
                    conditionNode,
                    intrinsic == "length-at-most"
                        ? ["intrinsic", "value", "max"]
                        : ["intrinsic", "value"],
                    "construction.steps[].ensure.condition",
                    diagnostics);

                var value = Scalar(conditionNode, "value");
                Condition? condition = intrinsic switch
                {
                    "non-empty" => new NonEmptyCondition(value),
                    "not-whitespace" => new NotWhitespaceCondition(value),
                    "length-at-most" => new LengthAtMostCondition(value, Int(conditionNode, "max")),
                    _ => null
                };

                if (condition is null)
                {
                    diagnostics.Add(new("VSIR102", $"Unsupported intrinsic '{intrinsic}'."));
                    continue;
                }

                if (!TryMapping(ensure, "failure", out var failure))
                {
                    diagnostics.Add(new("VSIR103", "Ensure step requires failure."));
                    continue;
                }

                RejectUnknownKeys(failure, ["message"], "construction.steps[].ensure.failure", diagnostics);
                steps.Add(new EnsureStep(condition, Scalar(failure, "message")));
            }

            var document = new DomainTypeVsir(
                version,
                kind,
                name,
                classification,
                shape,
                traits,
                refinedFrom,
                state,
                representation,
                representationMapping,
                new Construction(input, steps),
                equality);

            diagnostics.AddRange(DomainTypeValidator.Validate(document, validationContext));
            return new(document, diagnostics);
        }
        catch (Exception ex)
        {
            return Failure("VSIR000", ex.Message);
        }
    }

    private static ConstructionInput ParseConstructionInput(
        YamlMappingNode construction,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!construction.Children.TryGetValue(new YamlScalarNode("input"), out var node))
        {
            diagnostics.Add(new("VSIR111", "Construction requires input semantics."));
            return ConstructionInput.Product([]);
        }

        if (node is YamlScalarNode scalar)
        {
            var type = scalar.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(type))
                diagnostics.Add(new("VSIR111", "Construction scalar input requires a type."));
            return ConstructionInput.Scalar(type);
        }

        if (node is YamlMappingNode mapping)
            return ConstructionInput.Product(ReadFields(mapping));

        diagnostics.Add(new("VSIR111", "Construction input must be either a scalar type or a product mapping."));
        return ConstructionInput.Product([]);
    }

    private static RepresentationMapping? ParseRepresentationMapping(
        YamlMappingNode root,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!root.Children.ContainsKey(new YamlScalarNode("representation-mapping")))
            return null;

        if (!TryMapping(root, "representation-mapping", out var mapping))
        {
            diagnostics.Add(new("VSIR112", "representation-mapping must be a mapping."));
            return null;
        }

        var fields = new Dictionary<string, RepresentationProjection>(StringComparer.Ordinal);
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
            {
                diagnostics.Add(new("VSIR113", "representation-mapping requires scalar field names."));
                continue;
            }

            if (pair.Value is not YamlMappingNode projection)
            {
                diagnostics.Add(new("VSIR114", $"representation-mapping.{key.Value} must be a mapping."));
                continue;
            }

            RejectUnknownKeys(
                projection,
                ["stringify"],
                $"representation-mapping.{key.Value}",
                diagnostics);

            var stringify = Scalar(projection, "stringify");
            if (string.IsNullOrWhiteSpace(stringify))
            {
                diagnostics.Add(new("VSIR115", $"representation-mapping.{key.Value} requires a supported projection."));
                continue;
            }

            fields[key.Value] = new StringifyProjection(stringify);
        }

        return new(fields);
    }

    private static EqualitySemantics? ParseEquality(
        YamlMappingNode root,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!root.Children.ContainsKey(new YamlScalarNode("equality")))
            return null;

        if (!TryMapping(root, "equality", out var equalityNode))
        {
            diagnostics.Add(new("VSIR105", "Equality must be a mapping."));
            return null;
        }

        RejectUnknownKeys(equalityNode, ["intrinsic", "over", "by"], "equality", diagnostics);

        var intrinsic = OptionalScalar(equalityNode, "intrinsic");
        var over = OptionalScalar(equalityNode, "over");
        var by = Scalar(equalityNode, "by");

        if (string.IsNullOrWhiteSpace(by) || (intrinsic is null) == (over is null))
        {
            diagnostics.Add(new(
                "VSIR106",
                "Equality requires 'by' and exactly one of 'intrinsic' or 'over'."));
            return null;
        }

        return new(intrinsic, over, by);
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
                diagnostics.Add(new(
                    "VSIR104",
                    $"Unsupported non-scalar semantic key in '{semanticPath}'."));
                continue;
            }

            var key = scalar.Value ?? string.Empty;
            if (allowed.Contains(key))
                continue;

            diagnostics.Add(new(
                "VSIR104",
                rootDiagnostic
                    ? $"Unsupported root semantic '{key}'."
                    : $"Unsupported semantic '{semanticPath}.{key}'."));
        }
    }

    private static IReadOnlyList<string> ReadScalarSequence(
        YamlMappingNode node,
        string key,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!TrySequence(node, key, out var sequence))
            return [];

        var values = new List<string>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            if (child is not YamlScalarNode scalar)
            {
                diagnostics.Add(new(
                    "VSIR107",
                    $"Semantic sequence '{semanticPath}' requires scalar entries."));
                continue;
            }

            values.Add(scalar.Value ?? string.Empty);
        }

        return values;
    }

    private static IReadOnlyList<YamlMappingNode> ReadMappingSequence(
        YamlMappingNode node,
        string key,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!TrySequence(node, key, out var sequence))
            return [];

        var values = new List<YamlMappingNode>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            if (child is not YamlMappingNode mapping)
            {
                diagnostics.Add(new(
                    "VSIR108",
                    $"Semantic sequence '{semanticPath}' requires mapping entries."));
                continue;
            }

            values.Add(mapping);
        }

        return values;
    }

    private static VsirParseResult Failure(string code, string message) => new(null, [new(code, message)]);

    private static string Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value ?? string.Empty
            : string.Empty;

    private static string? OptionalScalar(YamlMappingNode node, string key)
    {
        var value = Scalar(node, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int Int(YamlMappingNode node, string key) => int.Parse(Scalar(node, key));

    private static ProductShape Product(YamlMappingNode node, string key) =>
        TryMapping(node, key, out var map)
            ? new(ReadFields(map))
            : new([]);

    private static IReadOnlyList<Field> ReadFields(YamlMappingNode map) =>
        map.Children
            .Select(pair => new Field(
                ((YamlScalarNode)pair.Key).Value ?? string.Empty,
                ((YamlScalarNode)pair.Value).Value ?? string.Empty))
            .ToArray();

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
}
