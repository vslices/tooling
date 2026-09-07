using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

/// <summary>
/// Canonical VSIR parser entry point.
/// VSIR 0.1 has one admitted surface; pre-normalized experimental grammars are intentionally unsupported.
/// </summary>
public static class VsirParser
{
    public static VsirParseResult Parse(
        string text,
        VsirValidationContext? validationContext = null)
    {
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return Failure("VSIR001", "Expected one YAML mapping document.");

            if (!UsesCanonicalSurface(root))
            {
                return Failure(
                    "VSIR090",
                    "Unsupported pre-normalized VSIR surface. Migrate the artifact to the canonical VSIR 0.1 grammar; compatibility parsing is intentionally not provided.");
            }

            return VsirLanguageParser.Parse(text, validationContext);
        }
        catch (Exception ex)
        {
            return Failure("VSIR000", ex.Message);
        }
    }

    private static bool UsesCanonicalSurface(YamlMappingNode root)
    {
        if (root.Children.ContainsKey(new YamlScalarNode("input")))
            return true;

        if (root.Children.TryGetValue(new YamlScalarNode("construction"), out var construction) &&
            construction is YamlSequenceNode)
            return true;

        foreach (var section in new[] { "state", "representation" })
        {
            if (!TryMapping(root, section, out var map))
                continue;

            foreach (var declaration in map.Children.Values.OfType<YamlMappingNode>())
            {
                if (declaration.Children.ContainsKey(new YamlScalarNode("from")) ||
                    declaration.Children.ContainsKey(new YamlScalarNode("mapping")) ||
                    declaration.Children.ContainsKey(new YamlScalarNode("type")))
                    return true;
            }
        }

        return false;
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

    private static VsirParseResult Failure(string code, string message) =>
        new(null, [new VsirDiagnostic(code, message)]);
}
