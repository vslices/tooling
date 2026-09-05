using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record ProjectConfiguration(
    string Version,
    string? DefaultTarget,
    string? RulesetSource,
    string? RulesetRef,
    string? UpdateSource,
    string? UpdateChannel,
    int? UpdatePullRequest = null,
    string? LineageBootstrapConvention = "existing-materialization",
    IReadOnlyList<string>? CSharpNamespaceIgnoredFolders = null)
{
    public const string CurrentVersion = "0.1";
    public const string OfficialRulesetSource = "https://github.com/vslices/ruleset";
    public const string OfficialRulesetRef = "main";
    public const string OfficialToolingSource = "https://github.com/vslices/tooling";
    public const string DefaultUpdateChannel = "preview";
    public const string DefaultLineageBootstrapConvention = "existing-materialization";

    public static ProjectConfiguration Default(string? target = "csharp") =>
        new(
            CurrentVersion,
            target,
            OfficialRulesetSource,
            OfficialRulesetRef,
            OfficialToolingSource,
            DefaultUpdateChannel,
            null,
            DefaultLineageBootstrapConvention,
            []);

    public static ProjectConfiguration? LoadFromProjectRoot(string projectRoot) =>
        LoadFromVslicesDirectory(Path.Combine(projectRoot, ".vslices"));

    public static async Task WriteAsync(
        string projectRoot,
        ProjectConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var vslicesDirectory = Path.Combine(projectRoot, ".vslices");
        Directory.CreateDirectory(vslicesDirectory);

        var targets = new YamlMappingNode
        {
            { "default", configuration.DefaultTarget ?? "csharp" }
        };

        if (configuration.CSharpNamespaceIgnoredFolders is { Count: > 0 })
        {
            targets.Add(
                "csharp",
                new YamlMappingNode
                {
                    {
                        "namespace",
                        new YamlMappingNode
                        {
                            {
                                "ignore-folders",
                                new YamlSequenceNode(
                                    configuration.CSharpNamespaceIgnoredFolders
                                        .Select(x => new YamlScalarNode(x)))
                            }
                        }
                    }
                });
        }

        var root = new YamlMappingNode
        {
            { "version", configuration.Version },
            { "targets", targets }
        };

        var ruleset = new YamlMappingNode();
        if (!string.IsNullOrWhiteSpace(configuration.RulesetSource))
            ruleset.Add("source", configuration.RulesetSource);
        if (!string.IsNullOrWhiteSpace(configuration.RulesetRef))
            ruleset.Add("ref", configuration.RulesetRef);
        root.Add("ruleset", ruleset);

        if (!string.IsNullOrWhiteSpace(configuration.LineageBootstrapConvention))
        {
            root.Add(
                "lineage",
                new YamlMappingNode
                {
                    {
                        "bootstrap",
                        new YamlMappingNode
                        {
                            { "convention", configuration.LineageBootstrapConvention }
                        }
                    }
                });
        }

        var updates = new YamlMappingNode
        {
            { "source", configuration.UpdateSource ?? OfficialToolingSource },
            { "channel", configuration.UpdateChannel ?? DefaultUpdateChannel }
        };
        if (configuration.UpdatePullRequest is not null)
            updates.Add("pull-request", configuration.UpdatePullRequest.Value.ToString());
        root.Add("updates", updates);

        var stream = new YamlStream(new YamlDocument(root));
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);

        await CommandInfrastructure.AtomicWrite(
            Path.Combine(vslicesDirectory, "config.yaml"),
            writer.ToString(),
            cancellationToken);
    }

    private static ProjectConfiguration? LoadFromVslicesDirectory(string vslicesDirectory)
    {
        var path = Path.Combine(vslicesDirectory, "config.yaml");
        if (!File.Exists(path))
            return null;

        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0 ||
            yaml.Documents[0].RootNode is not YamlMappingNode root)
            return null;

        var pullRequestText = NestedScalar(root, "updates", "pull-request");
        int? pullRequest = int.TryParse(pullRequestText, out var parsedPullRequest)
            ? parsedPullRequest
            : null;

        return new ProjectConfiguration(
            Scalar(root, "version") ?? CurrentVersion,
            NestedScalar(root, "targets", "default"),
            NestedScalar(root, "ruleset", "source"),
            NestedScalar(root, "ruleset", "ref"),
            NestedScalar(root, "updates", "source"),
            NestedScalar(root, "updates", "channel"),
            pullRequest,
            NestedScalar(root, "lineage", "bootstrap", "convention"),
            NestedSequence(root, "targets", "csharp", "namespace", "ignore-folders"));
    }

    private static string? NestedScalar(YamlMappingNode root, string section, string key)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(section), out var node) ||
            node is not YamlMappingNode mapping)
            return null;

        return Scalar(mapping, key);
    }

    private static string? NestedScalar(
        YamlMappingNode root,
        string section,
        string subsection,
        string key)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(section), out var sectionNode) ||
            sectionNode is not YamlMappingNode sectionMapping ||
            !sectionMapping.Children.TryGetValue(new YamlScalarNode(subsection), out var subsectionNode) ||
            subsectionNode is not YamlMappingNode subsectionMapping)
        {
            return null;
        }

        return Scalar(subsectionMapping, key);
    }

    private static IReadOnlyList<string> NestedSequence(
        YamlMappingNode root,
        string section,
        string subsection,
        string nestedSection,
        string key)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(section), out var sectionNode) ||
            sectionNode is not YamlMappingNode sectionMapping ||
            !sectionMapping.Children.TryGetValue(new YamlScalarNode(subsection), out var subsectionNode) ||
            subsectionNode is not YamlMappingNode subsectionMapping ||
            !subsectionMapping.Children.TryGetValue(new YamlScalarNode(nestedSection), out var nestedNode) ||
            nestedNode is not YamlMappingNode nestedMapping ||
            !nestedMapping.Children.TryGetValue(new YamlScalarNode(key), out var sequenceNode) ||
            sequenceNode is not YamlSequenceNode sequence)
        {
            return [];
        }

        return sequence.Children
            .OfType<YamlScalarNode>()
            .Select(x => x.Value?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? Scalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
        node is YamlScalarNode scalar
            ? scalar.Value
            : null;
}
