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
            var root = Path.GetFullPath(rulesetRoot);
            var manifestPath = Path.Combine(root, "manifest.yaml");
            if (!File.Exists(manifestPath))
            {
                return Failure(
                    "RSE001",
                    $"Ruleset manifest was not found at '{manifestPath}'.");
            }

            var manifest = LoadMapping(manifestPath);
            if (!TrySequence(manifest, "extensions", out var extensionFiles))
                return new(VsirSemanticExtensions.None, []);

            var normalize = new HashSet<string>(StringComparer.Ordinal);
            var diagnostics = new List<VsirDiagnostic>();

            foreach (var fileNode in extensionFiles.Children.OfType<YamlScalarNode>())
            {
                var relativePath = fileNode.Value ?? string.Empty;
                var fullPath = Path.GetFullPath(relativePath, root);
                if (!IsWithin(root, fullPath))
                {
                    diagnostics.Add(new("RSE003", $"Semantic extension path '{relativePath}' escapes the ruleset root."));
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    diagnostics.Add(new("RSE004", $"Semantic extension file '{relativePath}' does not exist."));
                    continue;
                }

                var document = LoadMapping(fullPath);
                if (!TrySequence(document, "extensions", out var entries))
                {
                    diagnostics.Add(new("RSE005", $"Semantic extension file '{relativePath}' must declare an extensions sequence."));
                    continue;
                }

                foreach (var entry in entries.Children.OfType<YamlMappingNode>())
                {
                    var node = Scalar(entry, "node");
                    if (string.IsNullOrWhiteSpace(node))
                    {
                        diagnostics.Add(new("RSE006", $"Semantic extension file '{relativePath}' contains an extension without a node."));
                        continue;
                    }

                    if (!TryMapping(entry, "semantic", out var semantic))
                    {
                        diagnostics.Add(new("RSE007", $"Semantic extension '{node}' must declare semantic metadata."));
                        continue;
                    }

                    var kind = Scalar(semantic, "kind");
                    if (!kind.Equals("normalize", StringComparison.Ordinal))
                    {
                        diagnostics.Add(new(
                            "RSE008",
                            $"Semantic extension '{node}' uses unsupported kind '{kind}'. Only 'normalize' is supported by this experiment."));
                        continue;
                    }

                    const string intrinsicPrefix = "intrinsic.";
                    if (!node.StartsWith(intrinsicPrefix, StringComparison.Ordinal) || node.Length == intrinsicPrefix.Length)
                    {
                        diagnostics.Add(new(
                            "RSE009",
                            $"Normalize semantic extension '{node}' must use an 'intrinsic.<name>' node identity."));
                        continue;
                    }

                    var intrinsic = node[intrinsicPrefix.Length..];
                    if (!normalize.Add(intrinsic))
                    {
                        diagnostics.Add(new(
                            "RSE010",
                            $"Normalize semantic extension '{intrinsic}' is declared more than once."));
                    }
                }
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

    private static YamlMappingNode LoadMapping(string path)
    {
        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return yaml.Documents.Count == 1 && yaml.Documents[0].RootNode is YamlMappingNode root
            ? root
            : throw new InvalidDataException($"Expected one YAML mapping document in '{path}'.");
    }

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

    private static bool IsWithin(string root, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return path.StartsWith(normalizedRoot, comparison);
    }
}
