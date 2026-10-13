using System.Text;
using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

/// <summary>Human comparison of two admitted Docs Standard snapshots; unknown YAML values remain visible.</summary>
internal static class DocsStandardDiff
{
    private static readonly string[] Families = ["documents", "support-notes", "nexus", "continuity-paths"];

    public static IReadOnlyDictionary<string, string> DeclaredFiles(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = File.OpenText(Path.Combine(root, "manifest.yaml"));
        var yaml = new YamlStream();
        yaml.Load(reader);
        var manifest = (YamlMappingNode)yaml.Documents[0].RootNode;
        foreach (var family in Families)
        {
            if (!manifest.Children.TryGetValue(new YamlScalarNode(family), out var node))
                continue;
            if (node is not YamlSequenceNode sequence)
                throw new InvalidDataException($"STDDIFF001: manifest.{family} must be a sequence.");
            foreach (var item in sequence.Children)
            {
                if (item is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                    throw new InvalidDataException($"STDDIFF002: manifest.{family} contains an invalid path.");
                var full = Path.GetFullPath(scalar.Value, root);
                var relative = Path.GetRelativePath(root, full);
                if (Path.IsPathRooted(relative) || relative.Equals("..", StringComparison.Ordinal) ||
                    relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidDataException($"STDDIFF003: Definition '{scalar.Value}' escapes the standard.");
                var key = family + "/" + relative.Replace('\\', '/');
                if (!files.TryAdd(key, full))
                    throw new InvalidDataException($"STDDIFF004: Duplicate definition '{key}'.");
            }
        }
        return files;
    }

    public static string Create(string name, string oldRoot, string nextRoot, string source)
    {
        var original = DeclaredFiles(oldRoot);
        var incoming = DeclaredFiles(nextRoot);
        var output = new StringBuilder();
        output.AppendLine($"# Docs Standard update — {name}");
        output.AppendLine();
        output.AppendLine($"Source: `{source.Replace("`", "'", StringComparison.Ordinal)}`");
        output.AppendLine();
        var added = incoming.Keys.Except(original.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var removed = original.Keys.Except(incoming.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var modified = incoming.Keys.Intersect(original.Keys, StringComparer.Ordinal)
            .Where(key => !File.ReadAllBytes(original[key]).SequenceEqual(File.ReadAllBytes(incoming[key])))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();

        WriteList(output, "Added definitions", added);
        WriteList(output, "Removed definitions", removed);
        output.AppendLine("## Modified definitions");
        if (modified.Length == 0) output.AppendLine("- None");
        foreach (var key in modified)
        {
            output.AppendLine($"### {key}");
            var oldValues = Flatten(original[key]);
            var nextValues = Flatten(incoming[key]);
            foreach (var path in oldValues.Keys.Union(nextValues.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                oldValues.TryGetValue(path, out var before);
                nextValues.TryGetValue(path, out var after);
                if (string.Equals(before, after, StringComparison.Ordinal)) continue;
                output.AppendLine($"- `{path.Replace("`", "'", StringComparison.Ordinal)}`: " +
                    $"`{Inline(before)}` → `{Inline(after)}`");
            }
        }

        var oldManifest = File.ReadAllText(Path.Combine(oldRoot, "manifest.yaml"));
        var newManifest = File.ReadAllText(Path.Combine(nextRoot, "manifest.yaml"));
        if (oldManifest != newManifest)
        {
            output.AppendLine();
            output.AppendLine("## Manifest");
            output.AppendLine("- Manifest content changed (including declared identity, ordering or memberships).");
        }
        if (added.Length == 0 && removed.Length == 0 && modified.Length == 0 && oldManifest == newManifest)
        {
            output.AppendLine();
            output.AppendLine("No changes in admitted definitions or manifest.");
        }
        output.AppendLine();
        output.AppendLine("## Interpretation limits");
        output.AppendLine("All differing YAML scalar paths are shown, including unfamiliar vocabulary. " +
            "Compatibility with existing authored artifacts has not been established by this file comparison.");
        return output.ToString();
    }

    private static void WriteList(StringBuilder output, string heading, IEnumerable<string> items)
    {
        output.AppendLine($"## {heading}");
        var any = false;
        foreach (var item in items) { output.AppendLine($"- {item}"); any = true; }
        if (!any) output.AppendLine("- None");
        output.AppendLine();
    }

    private static string Inline(string? text) =>
        (text ?? "<absent>").Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", " ↵ ", StringComparison.Ordinal)
            .Replace("`", "'", StringComparison.Ordinal);

    private static Dictionary<string, string> Flatten(string file)
    {
        using var reader = File.OpenText(file);
        var yaml = new YamlStream();
        yaml.Load(reader);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (yaml.Documents.Count == 1) Visit(yaml.Documents[0].RootNode, "$", entries);
        return entries;
    }

    private static void Visit(YamlNode node, string prefix, Dictionary<string, string> values)
    {
        if (node is YamlMappingNode mapping)
        {
            if (mapping.Children.Count == 0) values[prefix] = "{}";
            foreach (var child in mapping.Children)
                Visit(child.Value, prefix + "." + (child.Key as YamlScalarNode)?.Value, values);
        }
        else if (node is YamlSequenceNode sequence)
        {
            if (sequence.Children.Count == 0) values[prefix] = "[]";
            for (var index = 0; index < sequence.Children.Count; index++)
                Visit(sequence.Children[index], prefix + "[" + index + "]", values);
        }
        else if (node is YamlScalarNode scalar) values[prefix] = scalar.Value ?? "<null>";
        else values[prefix] = node.ToString();
    }
}
