using YamlDotNet.RepresentationModel;
using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

public sealed record CSharpLoweringRule(
    string Node,
    string Mode,
    string Renderer,
    string Template);

public sealed class CSharpLoweringRuleSet
{
    private readonly IReadOnlyDictionary<string, CSharpLoweringRule> _rules;

    private CSharpLoweringRuleSet(IReadOnlyDictionary<string, CSharpLoweringRule> rules) =>
        _rules = rules;

    public static CSharpLoweringRuleSetLoadResult Load(string rulesetRoot)
    {
        var diagnostics = new List<VsirDiagnostic>();

        try
        {
            var root = Path.GetFullPath(rulesetRoot);
            var manifestPath = Path.Combine(root, "manifest.yaml");
            if (!File.Exists(manifestPath))
                return Failure("CSR001", $"Ruleset manifest was not found at '{manifestPath}'.");

            var manifest = LoadMapping(manifestPath);
            if (!TryMapping(manifest, "targets", out var targets) ||
                !TryMapping(targets, "csharp", out var csharp) ||
                !TrySequence(csharp, "rules", out var ruleFiles))
            {
                return Failure("CSR002", "Ruleset manifest must declare targets.csharp.rules.");
            }

            var rules = new Dictionary<string, CSharpLoweringRule>(StringComparer.Ordinal);
            foreach (var fileNode in ruleFiles.Children.OfType<YamlScalarNode>())
            {
                var relativePath = fileNode.Value ?? string.Empty;
                var fullPath = Path.GetFullPath(relativePath, root);
                if (!IsWithin(root, fullPath))
                {
                    diagnostics.Add(new("CSR003", $"Ruleset path '{relativePath}' escapes the ruleset root."));
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    diagnostics.Add(new("CSR004", $"Ruleset file '{relativePath}' does not exist."));
                    continue;
                }

                var document = LoadMapping(fullPath);
                if (!TrySequence(document, "rules", out var entries))
                {
                    diagnostics.Add(new("CSR005", $"Ruleset file '{relativePath}' must declare a rules sequence."));
                    continue;
                }

                foreach (var entry in entries.Children.OfType<YamlMappingNode>())
                {
                    var node = Scalar(entry, "node");
                    var mode = Scalar(entry, "mode");
                    var renderer = Scalar(entry, "renderer");
                    var template = Scalar(entry, "template");

                    if (string.IsNullOrWhiteSpace(node))
                    {
                        diagnostics.Add(new("CSR006", $"Ruleset file '{relativePath}' contains a rule without a node."));
                        continue;
                    }

                    if (!mode.Equals("deterministic", StringComparison.OrdinalIgnoreCase))
                        diagnostics.Add(new("CSR008", $"Lowering rule '{node}' uses unsupported mode '{mode}'."));
                    if (!renderer.Equals("expression", StringComparison.OrdinalIgnoreCase))
                        diagnostics.Add(new("CSR009", $"Lowering rule '{node}' uses unsupported renderer '{renderer}'."));
                    if (string.IsNullOrWhiteSpace(template))
                        diagnostics.Add(new("CSR010", $"Lowering rule '{node}' must declare a non-empty template."));

                    if (!rules.TryAdd(node, new(node, mode, renderer, template)))
                        diagnostics.Add(new("CSR007", $"Lowering rule '{node}' is declared more than once."));
                }
            }

            return diagnostics.Count == 0
                ? new(new(rules), [])
                : new(null, diagnostics);
        }
        catch (Exception ex)
        {
            return Failure("CSR000", ex.Message);
        }
    }

    public bool TryRenderDeterministicExpression(
        string node,
        IReadOnlyDictionary<string, string> bindings,
        out string expression)
    {
        expression = string.Empty;
        if (!_rules.TryGetValue(node, out var rule) ||
            !rule.Mode.Equals("deterministic", StringComparison.OrdinalIgnoreCase) ||
            !rule.Renderer.Equals("expression", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(rule.Template))
        {
            return false;
        }

        expression = bindings.Aggregate(
            rule.Template,
            static (current, pair) => current.Replace(
                "{" + pair.Key + "}",
                pair.Value,
                StringComparison.Ordinal));

        return true;
    }

    private static CSharpLoweringRuleSetLoadResult Failure(string code, string message) =>
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

public sealed record CSharpLoweringRuleSetLoadResult(
    CSharpLoweringRuleSet? RuleSet,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => RuleSet is not null && Diagnostics.Count == 0;
}
