using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

/// <summary>
/// Canonical VSIR parser entry point.
/// VSIR 0.1 has one admitted semantic grammar and no compatibility dispatch by historical document shape.
/// Searchable artifact metadata such as tags is validated and removed before semantic parsing.
/// </summary>
public static class VsirParser
{
    public static VsirParseResult Parse(
        string text,
        VsirValidationContext? validationContext = null)
    {
        var semanticText = StripSearchMetadata(text, out var metadataDiagnostic);
        if (metadataDiagnostic is not null)
            return new(null, [VsirDiagnosticLocator.Attach(text, metadataDiagnostic)]);

        VsirParseResult result;
        if (IsMaintainedDomainType(semanticText!))
            result = MaintainedDomainTypeLanguageParser.Parse(semanticText!);
        else if (IsSumDomainType(semanticText!))
            result = SumDomainTypeLanguageParser.Parse(semanticText!, validationContext);
        else
            result = VsirLanguageParser.Parse(semanticText!, validationContext);

        return VsirDiagnosticLocator.Attach(text, result);
    }

    private static bool IsMaintainedDomainType(string text) =>
        MatchesDomainType(text, "classification", "maintained");

    private static bool IsSumDomainType(string text) =>
        MatchesDomainType(text, "shape", "sum");

    private static bool MatchesDomainType(string text, string key, string expected)
    {
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(text));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return false;

            return root.Children.TryGetValue(new YamlScalarNode("kind"), out var kindNode) &&
                   kindNode is YamlScalarNode kind &&
                   string.Equals(kind.Value, "domain-type", StringComparison.Ordinal) &&
                   root.Children.TryGetValue(new YamlScalarNode(key), out var valueNode) &&
                   valueNode is YamlScalarNode value &&
                   string.Equals(value.Value, expected, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string? StripSearchMetadata(
        string text,
        out VsirDiagnostic? diagnostic)
    {
        diagnostic = null;

        YamlStream yaml;
        YamlMappingNode root;
        try
        {
            yaml = new YamlStream();
            yaml.Load(new StringReader(text));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
                return text;
            root = mapping;
        }
        catch
        {
            return text;
        }

        var tagsKey = new YamlScalarNode("tags");
        if (!root.Children.TryGetValue(tagsKey, out var tagsNode))
            return text;

        if (tagsNode is not YamlSequenceNode tags)
        {
            diagnostic = new(
                "VSIR150",
                "Artifact metadata 'tags' must be a sequence of non-empty unique strings.");
            return null;
        }

        var values = new List<string>(tags.Children.Count);
        foreach (var child in tags.Children)
        {
            if (child is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                diagnostic = new(
                    "VSIR150",
                    "Artifact metadata 'tags' must be a sequence of non-empty unique strings.");
                return null;
            }

            values.Add(scalar.Value!);
        }

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            diagnostic = new(
                "VSIR151",
                "Artifact metadata 'tags' values must be unique.");
            return null;
        }

        root.Children.Remove(tagsKey);
        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return writer.ToString();
    }
}
