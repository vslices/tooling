using VSlices.Vsir;
using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record RulesetSemanticExtensionsLoadResult(
    VsirSemanticExtensions? Extensions,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Extensions is not null && Diagnostics.Count == 0;
}

internal static class RulesetSemanticExtensions
{
    public static RulesetSemanticExtensionsLoadResult Load(string rulesetRoot)
    {
        try
        {
            var manifestPath = Path.Combine(rulesetRoot, "manifest.yaml");
            if (!File.Exists(manifestPath))
            {
                return Failure(
                    "RSE001",
                    $"Ruleset manifest was not found at '{manifestPath}'.");
            }

            using var reader = File.OpenText(manifestPath);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return Failure("RSE002", "Ruleset manifest must contain one YAML mapping document.");

            if (!TryMapping(root, "semantic-extensions", out var extensionsNode))
                return new(VsirSemanticExtensions.None, []);

            var normalize = new HashSet<string>(StringComparer.Ordinal);
            var diagnostics = new List<VsirDiagnostic>();

            foreach (var entry in extensionsNode.Children)
            {
                if (entry.Key is not YamlScalarNode idNode || string.IsNullOrWhiteSpace(idNode.Value))
                {
                    diagnostics.Add(new("RSE003", "Semantic extension ids must be non-empty scalar keys."));
                    continue;
                }

                if (entry.Value is not YamlMappingNode declaration)
                {
                    diagnostics.Add(new("RSE004", $"Semantic extension '{idNode.Value}' must be a mapping."));
                    continue;
                }

                var kind = Scalar(declaration, "kind");
                if (!kind.Equals("normalize", StringComparison.Ordinal))
                {
                    diagnostics.Add(new(
                        "RSE005",
                        $"Semantic extension '{idNode.Value}' uses unsupported kind '{kind}'. Only 'normalize' is supported by this experiment."));
                    continue;
                }

                normalize.Add(idNode.Value);
            }

            return diagnostics.Count == 0
                ? new(new(normalize), [])
                : new(null, diagnostics);
        }
        catch (Exception ex)
        {
            return Failure("RSE000", ex.Message);
        }
    }

    private static RulesetSemanticExtensionsLoadResult Failure(string code, string message) =>
        new(null, [new(code, message)]);

    private static string Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value ?? string.Empty
            : string.Empty;

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
}
