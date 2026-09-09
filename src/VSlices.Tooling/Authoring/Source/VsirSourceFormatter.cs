using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal static class VsirSourceFormatter
{
    public static string FormatAfterMutation(string source)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(source));

        if (yaml.Documents.Count == 1)
            ForceReadableStyle(yaml.Documents[0].RootNode);

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);

        return RemoveDocumentMarkers(writer.ToString(), source);
    }

    private static void ForceReadableStyle(YamlNode node, bool forceSequenceBlock = false)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                mapping.Style = YamlDotNet.Core.Events.MappingStyle.Block;
                foreach (var (keyNode, valueNode) in mapping.Children)
                {
                    var isConstruction = keyNode is YamlScalarNode key &&
                        string.Equals(key.Value, "construction", StringComparison.Ordinal);
                    ForceReadableStyle(valueNode, forceSequenceBlock || isConstruction);
                }
                break;

            case YamlSequenceNode sequence:
                if (forceSequenceBlock)
                    sequence.Style = YamlDotNet.Core.Events.SequenceStyle.Block;

                foreach (var child in sequence.Children)
                    ForceReadableStyle(child, forceSequenceBlock);
                break;
        }
    }

    private static string RemoveDocumentMarkers(string serialized, string original)
    {
        var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = serialized
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var lines = normalized.Split('\n');
        var retained = lines
            .Where(line => line is not "---" and not "...")
            .ToArray();

        return string.Join(newline, retained).TrimEnd('\r', '\n') + newline;
    }
}
