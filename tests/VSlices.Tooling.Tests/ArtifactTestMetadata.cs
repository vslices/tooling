using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling.Tests;

internal static class ArtifactTestMetadata
{
    public static YamlMappingNode Read(string source)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.Equal("---", lines[0]);
        var end = Array.FindIndex(lines, 1, line => line == "---");
        Assert.True(end > 0);
        var yaml = new YamlStream();
        yaml.Load(new StringReader(string.Join("\n", lines.Skip(1).Take(end - 1))));
        return Assert.IsType<YamlMappingNode>(Assert.Single(yaml.Documents).RootNode);
    }

    public static YamlNode Node(YamlMappingNode root, params string[] path)
    {
        YamlNode node = root;
        foreach (var key in path)
            node = Assert.IsType<YamlMappingNode>(node).Children[new YamlScalarNode(key)];
        return node;
    }

    public static string Scalar(YamlMappingNode root, params string[] path) =>
        Assert.IsType<YamlScalarNode>(Node(root, path)).Value!;

    public static string Body(string source)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var end = Array.FindIndex(lines, 1, line => line == "---");
        Assert.True(end > 0);
        return string.Join("\n", lines.Skip(end + 1)).TrimStart('\n');
    }
}
