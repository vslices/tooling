using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal static class VsirSourceFormatter
{
    public static string FormatAfterMutation(string source)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(source));

        if (yaml.Documents.Count == 1)
            ForceBlockStyle(yaml.Documents[0].RootNode);

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);

        return RemoveDocumentMarkers(writer.ToString(), source);
    }

    private static void ForceBlockStyle(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                mapping.Style = YamlDotNet.Core.Events.MappingStyle.Block;
                foreach (var child in mapping.Children.Values)
                    ForceBlockStyle(child);
                break;

            case YamlSequenceNode sequence:
                sequence.Style = YamlDotNet.Core.Events.SequenceStyle.Block;
                foreach (var child in sequence.Children)
                    ForceBlockStyle(child);
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
