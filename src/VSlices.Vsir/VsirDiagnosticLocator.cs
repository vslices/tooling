using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace VSlices.Vsir;

internal static partial class VsirDiagnosticLocator
{
    private static readonly IReadOnlyDictionary<string, string> CodePaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VSIR101"] = "construction",
            ["VSIR102"] = "construction",
            ["VSIR103"] = "construction",
            ["VSIR105"] = "equality",
            ["VSIR106"] = "equality",
            ["VSIR107"] = "traits",
            ["VSIR110"] = "construction",
            ["VSIR111"] = "input",
            ["VSIR119"] = "construction",
            ["VSIR120"] = "construction",
            ["VSIR121"] = "construction",
            ["VSIR122"] = "construction",
            ["VSIR123"] = "construction",
            ["VSIR124"] = "construction",
            ["VSIR125"] = "construction",
            ["VSIR126"] = "construction",
            ["VSIR127"] = "construction",
            ["VSIR150"] = "tags",
            ["VSIR151"] = "tags",
            ["VSIR200"] = "vsir",
            ["VSIR201"] = "kind",
            ["VSIR202"] = "classification",
            ["VSIR203"] = "shape",
            ["VSIR204"] = "traits",
            ["VSIR205"] = "state",
            ["VSIR206"] = "representation",
            ["VSIR207"] = "input",
            ["VSIR209"] = "state",
            ["VSIR210"] = "representation",
            ["VSIR211"] = "construction",
            ["VSIR213"] = "equality",
            ["VSIR214"] = "equality",
            ["VSIR215"] = "equality",
            ["VSIR216"] = "equality",
            ["VSIR217"] = "traits",
            ["VSIR218"] = "traits",
            ["VSIR219"] = "construction",
            ["VSIR221"] = "construction",
            ["VSIR222"] = "refined-from",
            ["VSIR223"] = "refined-from",
            ["VSIR224"] = "input",
            ["VSIR225"] = "input",
            ["VSIR226"] = "refined-from",
            ["VSIR227"] = "equality",
            ["VSIR228"] = "equality",
            ["VSIR229"] = "state",
            ["VSIR230"] = "construction",
            ["VSIR231"] = "construction",
            ["VSIR232"] = "state",
            ["VSIR233"] = "construction",
            ["VSIR234"] = "representation",
            ["VSIR235"] = "representation",
            ["VSIR237"] = "representation",
            ["VSIR238"] = "state",
            ["VSIR239"] = "state",
            ["VSIR240"] = "state",
            ["VSIR241"] = "construction",
            ["VSIR242"] = "construction",
            ["VSIR243"] = "construction",
            ["VSIR244"] = "construction",
            ["VSIR245"] = "construction",
            ["VSIR246"] = "construction",
            ["VSIR247"] = "representation",
            ["VSIR248"] = "representation",
            ["VSIR249"] = "representation",
            ["VSIR250"] = "representation",
            ["VSIR251"] = "representation",
            ["VSIR252"] = "equality",
            ["VSIR253"] = "construction",
            ["VSIR254"] = "construction",
            ["VSIR255"] = "construction",
            ["VSIR256"] = "construction",
            ["VSIR257"] = "construction",
            ["VSIR258"] = "construction"
        };

    public static VsirParseResult Attach(string source, VsirParseResult result)
    {
        if (result.Diagnostics.Count == 0)
            return result;

        var index = SourceIndex.TryCreate(source);
        var diagnostics = result.Diagnostics
            .Select(diagnostic => Attach(index, diagnostic))
            .ToArray();
        return result with { Diagnostics = diagnostics };
    }

    public static VsirDiagnostic Attach(string source, VsirDiagnostic diagnostic) =>
        Attach(SourceIndex.TryCreate(source), diagnostic);

    private static VsirDiagnostic Attach(SourceIndex? index, VsirDiagnostic diagnostic)
    {
        if (diagnostic.Source is not null && !string.IsNullOrWhiteSpace(diagnostic.SemanticPath))
            return diagnostic;

        var semanticPath = diagnostic.SemanticPath ?? InferSemanticPath(diagnostic);
        if (semanticPath is null)
            return diagnostic;

        var preferKey = diagnostic.Code == "VSIR104";
        var span = diagnostic.Source ?? index?.Find(semanticPath, preferKey);
        return diagnostic with
        {
            SemanticPath = semanticPath,
            Source = span
        };
    }

    private static string? InferSemanticPath(VsirDiagnostic diagnostic)
    {
        if (diagnostic.Code == "VSIR104")
        {
            var unsupported = UnsupportedSemanticRegex().Match(diagnostic.Message);
            if (unsupported.Success)
                return unsupported.Groups["path"].Value;
        }

        var explicitPath = SemanticPathRegex().Match(diagnostic.Message);
        if (explicitPath.Success)
            return explicitPath.Groups["path"].Value;

        return CodePaths.TryGetValue(diagnostic.Code, out var path)
            ? path
            : null;
    }

    private sealed class SourceIndex
    {
        private readonly Dictionary<string, List<Entry>> _entries = new(StringComparer.Ordinal);

        public static SourceIndex? TryCreate(string source)
        {
            try
            {
                var yaml = new YamlStream();
                yaml.Load(new StringReader(source));
                if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                    return null;

                var index = new SourceIndex();
                index.IndexMapping(root, string.Empty);
                return index;
            }
            catch
            {
                return null;
            }
        }

        public VsirSourceSpan? Find(string path, bool preferKey)
        {
            var canonical = Canonicalize(path);
            if (!_entries.TryGetValue(canonical, out var entries) || entries.Count == 0)
                return null;

            var entry = entries[0];
            return preferKey ? entry.Key : entry.Value;
        }

        private void IndexMapping(YamlMappingNode mapping, string prefix)
        {
            foreach (var pair in mapping.Children)
            {
                if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
                    continue;

                var path = string.IsNullOrEmpty(prefix)
                    ? key.Value!
                    : prefix + "." + key.Value;
                Add(path, new(ToSpan(pair.Key), ToSpan(pair.Value)));
                IndexNode(pair.Value, path);
            }
        }

        private void IndexNode(YamlNode node, string path)
        {
            switch (node)
            {
                case YamlMappingNode mapping:
                    IndexMapping(mapping, path);
                    break;
                case YamlSequenceNode sequence:
                    for (var i = 0; i < sequence.Children.Count; i++)
                    {
                        var childPath = $"{path}[{i}]";
                        Add(childPath, new(ToSpan(sequence.Children[i]), ToSpan(sequence.Children[i])));
                        IndexNode(sequence.Children[i], childPath);
                    }
                    break;
            }
        }

        private void Add(string path, Entry entry)
        {
            var canonical = Canonicalize(path);
            if (!_entries.TryGetValue(canonical, out var values))
            {
                values = [];
                _entries[canonical] = values;
            }
            values.Add(entry);
        }

        private static string Canonicalize(string path) =>
            SequenceIndexRegex().Replace(path, "[]");

        private static VsirSourceSpan ToSpan(YamlNode node) =>
            new(
                checked((int)node.Start.Line) + 1,
                checked((int)node.Start.Column) + 1,
                checked((int)node.End.Line) + 1,
                checked((int)node.End.Column) + 1);

        private sealed record Entry(VsirSourceSpan Key, VsirSourceSpan Value);
    }

    [GeneratedRegex("Unsupported (?:root )?semantic '(?<path>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedSemanticRegex();

    [GeneratedRegex("(?<path>(?:state|representation|input|construction|equality|traits|refined-from|shape|classification|kind|vsir|tags)(?:\\[[0-9]*\\])?(?:\\.[A-Za-z_][A-Za-z0-9_-]*(?:\\[[0-9]*\\])?)*)", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticPathRegex();

    [GeneratedRegex("\\[[0-9]+\\]", RegexOptions.CultureInvariant)]
    private static partial Regex SequenceIndexRegex();
}
