using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

public static class VsirParser
{
    public static VsirParseResult Parse(string text)
    {
        var diagnostics = new List<VsirDiagnostic>();

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));

            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return Failure("VSIR001", "Expected one YAML mapping document.");

            var version = Scalar(root, "vsir");
            var kind = Scalar(root, "kind");
            var name = Scalar(root, "name");
            var classification = Scalar(root, "classification");
            var shape = Scalar(root, "shape");
            var traits = Sequence(root, "traits");
            var state = Product(root, "state");
            var representation = Product(root, "representation");

            if (!TryMapping(root, "construction", out var constructionNode))
                return Failure("VSIR002", "Missing construction mapping.");

            var input = Product(constructionNode, "input");
            var steps = new List<ConstructionStep>();

            if (TrySequence(constructionNode, "steps", out var stepNodes))
            {
                foreach (var stepNode in stepNodes.Children.OfType<YamlMappingNode>())
                {
                    if (!TryMapping(stepNode, "ensure", out var ensure))
                    {
                        diagnostics.Add(new("VSIR100", "Only construction step 'ensure' is supported by the experimental parser."));
                        continue;
                    }

                    if (!TryMapping(ensure, "condition", out var conditionNode))
                    {
                        diagnostics.Add(new("VSIR101", "Ensure step requires condition."));
                        continue;
                    }

                    var intrinsic = Scalar(conditionNode, "intrinsic");
                    var value = Scalar(conditionNode, "value");
                    Condition? condition = intrinsic switch
                    {
                        "non-empty" => new NonEmptyCondition(value),
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

                    steps.Add(new EnsureStep(condition, Scalar(failure, "message")));
                }
            }

            var document = new DomainTypeVsir(
                version,
                kind,
                name,
                classification,
                shape,
                traits,
                state,
                representation,
                new Construction(input, steps));

            diagnostics.AddRange(DomainTypeValidator.Validate(document));
            return new(document, diagnostics);
        }
        catch (Exception ex)
        {
            return Failure("VSIR000", ex.Message);
        }
    }

    private static VsirParseResult Failure(string code, string message) => new(null, [new(code, message)]);

    private static string Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value ?? string.Empty
            : string.Empty;

    private static int Int(YamlMappingNode node, string key) => int.Parse(Scalar(node, key));

    private static IReadOnlyList<string> Sequence(YamlMappingNode node, string key) =>
        TrySequence(node, key, out var sequence)
            ? sequence.Children.OfType<YamlScalarNode>().Select(x => x.Value ?? string.Empty).ToArray()
            : [];

    private static ProductShape Product(YamlMappingNode node, string key)
    {
        if (!TryMapping(node, key, out var map))
            return new([]);

        return new(map.Children
            .Select(pair => new Field(
                ((YamlScalarNode)pair.Key).Value ?? string.Empty,
                ((YamlScalarNode)pair.Value).Value ?? string.Empty))
            .ToArray());
    }

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
