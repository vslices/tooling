using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;
using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

public sealed record CSharpLoweringRule(
    string Node,
    string Mode,
    string Renderer,
    IReadOnlyList<string> Bindings,
    string Template);

public sealed class CSharpLoweringRuleSet
{
    private static readonly Regex PlaceholderPattern =
        new(@"\{(?<name>[A-Za-z][A-Za-z0-9_-]*)\}", RegexOptions.CultureInvariant);

    private readonly IReadOnlyDictionary<string, CSharpLoweringRule> _rules;

    private CSharpLoweringRuleSet(IReadOnlyDictionary<string, CSharpLoweringRule> rules) =>
        _rules = rules;

    public static CSharpLoweringRuleSetLoadResult Load(
        string rulesetRoot,
        IReadOnlyList<CSharpLoweringRule>? additionalRules = null)
    {
        var diagnostics = new List<VsirDiagnostic>();

        try
        {
            var root = Path.GetFullPath(rulesetRoot);
            var manifestPath = Path.Combine(root, "manifest.yaml");
            if (!File.Exists(manifestPath))
                return Failure("CSR001", $"Ruleset manifest was not found at '{manifestPath}'.");

            var manifest = LoadMapping(manifestPath);
            RejectUnknownKeys(
                manifest,
                ["$schema", "kind", "version", "targets"],
                "ruleset manifest",
                diagnostics);

            if (!TryRequiredMapping(manifest, "targets", "ruleset manifest", diagnostics, out var targets) ||
                !TryRequiredMapping(targets, "csharp", "ruleset manifest.targets", diagnostics, out var csharp) ||
                !TryRequiredSequence(csharp, "rules", "ruleset manifest.targets.csharp", diagnostics, out var ruleFiles))
            {
                return new(null, diagnostics);
            }

            RejectUnknownKeys(csharp, ["rules"], "ruleset manifest.targets.csharp", diagnostics);

            var rules = new Dictionary<string, CSharpLoweringRule>(StringComparer.Ordinal);
            foreach (var fileNode in ruleFiles.Children)
            {
                if (fileNode is not YamlScalarNode fileScalar || string.IsNullOrWhiteSpace(fileScalar.Value))
                {
                    diagnostics.Add(new(
                        "CSR012",
                        "Ruleset manifest targets.csharp.rules requires non-empty scalar path entries."));
                    continue;
                }

                var relativePath = fileScalar.Value.Trim();
                var fullPath = ResolveRulesetPath(root, relativePath, diagnostics);
                if (fullPath is null)
                    continue;

                var document = LoadMapping(fullPath);
                RejectUnknownKeys(document, ["rules"], $"ruleset file '{relativePath}'", diagnostics);
                if (!TryRequiredSequence(
                        document,
                        "rules",
                        $"ruleset file '{relativePath}'",
                        diagnostics,
                        out var entries))
                {
                    continue;
                }

                foreach (var entryNode in entries.Children)
                {
                    if (entryNode is not YamlMappingNode entry)
                    {
                        diagnostics.Add(new(
                            "CSR013",
                            $"Ruleset file '{relativePath}' requires mapping rule entries."));
                        continue;
                    }

                    RejectUnknownKeys(
                        entry,
                        ["node", "mode", "renderer", "bindings", "template"],
                        $"ruleset rule in '{relativePath}'",
                        diagnostics);
                    AddRule(entry, relativePath, rules, diagnostics);
                }
            }

            foreach (var rule in additionalRules ?? [])
                AddRule(rule, ".vslices/extensions", rules, diagnostics);

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
        out string expression) =>
        TryRenderDeterministic(node, "expression", bindings, out expression);

    public bool TryRenderDeterministicType(
        string node,
        IReadOnlyDictionary<string, string> bindings,
        out string type) =>
        TryRenderDeterministic(node, "type", bindings, out type);

    private bool TryRenderDeterministic(
        string node,
        string renderer,
        IReadOnlyDictionary<string, string> bindings,
        out string rendered)
    {
        rendered = string.Empty;
        if (!_rules.TryGetValue(node, out var rule) ||
            !rule.Mode.Equals("deterministic", StringComparison.OrdinalIgnoreCase) ||
            !rule.Renderer.Equals(renderer, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(rule.Template))
        {
            return false;
        }

        var required = rule.Bindings.ToHashSet(StringComparer.Ordinal);
        if (bindings.Count != required.Count || bindings.Keys.Any(key => !required.Contains(key)))
            return false;

        rendered = bindings.Aggregate(
            rule.Template,
            static (current, pair) => current.Replace(
                "{" + pair.Key + "}",
                pair.Value,
                StringComparison.Ordinal));

        return true;
    }

    private static string? ResolveRulesetPath(
        string root,
        string relativePath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var fullPath = Path.GetFullPath(relativePath, root);
        if (!IsWithin(root, fullPath))
        {
            diagnostics.Add(new("CSR003", $"Ruleset path '{relativePath}' escapes the ruleset root."));
            return null;
        }

        if (!File.Exists(fullPath))
        {
            diagnostics.Add(new("CSR004", $"Ruleset file '{relativePath}' does not exist."));
            return null;
        }

        return fullPath;
    }

    private static void AddRule(
        YamlMappingNode entry,
        string relativePath,
        IDictionary<string, CSharpLoweringRule> rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!TryRequiredSequence(
                entry,
                "bindings",
                $"ruleset rule in '{relativePath}'",
                diagnostics,
                out var bindingsNode))
        {
            return;
        }

        var bindings = ReadBindings(bindingsNode, relativePath, diagnostics);
        if (bindings is null)
            return;

        AddRule(
            new CSharpLoweringRule(
                Scalar(entry, "node"),
                Scalar(entry, "mode"),
                Scalar(entry, "renderer"),
                bindings,
                Scalar(entry, "template")),
            relativePath,
            rules,
            diagnostics);
    }

    private static void AddRule(
        CSharpLoweringRule rule,
        string source,
        IDictionary<string, CSharpLoweringRule> rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(rule.Node))
        {
            diagnostics.Add(new("CSR006", $"Ruleset source '{source}' contains a rule without a node."));
            return;
        }

        if (!rule.Mode.Equals("deterministic", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new("CSR008", $"Lowering rule '{rule.Node}' uses unsupported mode '{rule.Mode}'."));
        if (!rule.Renderer.Equals("expression", StringComparison.OrdinalIgnoreCase) &&
            !rule.Renderer.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new("CSR009", $"Lowering rule '{rule.Node}' uses unsupported renderer '{rule.Renderer}'."));
        }
        if (string.IsNullOrWhiteSpace(rule.Template))
            diagnostics.Add(new("CSR010", $"Lowering rule '{rule.Node}' must declare a non-empty template."));

        ValidateBindingContract(rule, source, diagnostics);

        if (!rules.TryAdd(rule.Node, rule))
            diagnostics.Add(new("CSR007", $"Lowering rule '{rule.Node}' is declared more than once."));
    }

    private static IReadOnlyList<string>? ReadBindings(
        YamlSequenceNode bindings,
        string source,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var result = new List<string>(bindings.Children.Count);
        foreach (var node in bindings.Children)
        {
            if (node is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                diagnostics.Add(new("CSR016", $"Ruleset source '{source}' contains a non-scalar or empty binding declaration."));
                return null;
            }

            result.Add(scalar.Value.Trim());
        }

        if (result.Distinct(StringComparer.Ordinal).Count() != result.Count)
        {
            diagnostics.Add(new("CSR017", $"Ruleset source '{source}' contains duplicate binding declarations."));
            return null;
        }

        return result;
    }

    private static void ValidateBindingContract(
        CSharpLoweringRule rule,
        string source,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var declared = rule.Bindings.ToHashSet(StringComparer.Ordinal);
        if (declared.Count != rule.Bindings.Count || rule.Bindings.Any(string.IsNullOrWhiteSpace))
        {
            diagnostics.Add(new("CSR017", $"Lowering rule '{rule.Node}' from '{source}' must declare unique non-empty bindings."));
            return;
        }

        var placeholders = PlaceholderPattern.Matches(rule.Template ?? string.Empty)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var undeclared = placeholders.Where(name => !declared.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (undeclared.Length > 0)
        {
            diagnostics.Add(new(
                "CSR018",
                $"Lowering rule '{rule.Node}' uses undeclared template bindings: {string.Join(", ", undeclared)}."));
        }

        var unused = declared.Where(name => !placeholders.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (unused.Length > 0)
        {
            diagnostics.Add(new(
                "CSR019",
                $"Lowering rule '{rule.Node}' declares bindings not used by its template: {string.Join(", ", unused)}."));
        }
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

    private static bool TryRequiredMapping(
        YamlMappingNode mapping,
        string key,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics,
        out YamlMappingNode result)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlMappingNode typed)
        {
            result = typed;
            return true;
        }

        diagnostics.Add(new("CSR002", $"{semanticPath}.{key} must be a mapping."));
        result = null!;
        return false;
    }

    private static bool TryRequiredSequence(
        YamlMappingNode mapping,
        string key,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics,
        out YamlSequenceNode result)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlSequenceNode typed)
        {
            result = typed;
            return true;
        }

        diagnostics.Add(new("CSR005", $"{semanticPath}.{key} must be a sequence."));
        result = null!;
        return false;
    }

    private static void RejectUnknownKeys(
        YamlMappingNode mapping,
        IEnumerable<string> allowedKeys,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var allowed = allowedKeys.ToHashSet(StringComparer.Ordinal);
        foreach (var keyNode in mapping.Children.Keys)
        {
            if (keyNode is not YamlScalarNode scalar)
            {
                diagnostics.Add(new("CSR014", $"{semanticPath} contains a non-scalar key."));
                continue;
            }

            var key = scalar.Value ?? string.Empty;
            if (!allowed.Contains(key))
                diagnostics.Add(new("CSR015", $"Unsupported ruleset key '{semanticPath}.{key}'."));
        }
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
