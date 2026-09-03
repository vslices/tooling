using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record ProjectConfiguration(
    string Version,
    string? DefaultTarget,
    string? RulesetSource,
    string? RulesetRef,
    string? UpdateSource,
    string? UpdateChannel,
    int? UpdatePullRequest)
{
    public const string CurrentVersion = "0.1";
    public const string OfficialRulesetSource = "https://github.com/vslices/ruleset";
    public const string OfficialRulesetRef = "main";
    public const string OfficialToolingSource = "https://github.com/vslices/tooling";
    public const string DefaultUpdateChannel = "preview";

    public static ProjectConfiguration Default(string? target = "csharp") =>
        new(
            CurrentVersion,
            target,
            OfficialRulesetSource,
            OfficialRulesetRef,
            OfficialToolingSource,
            DefaultUpdateChannel,
            null);

    public static ProjectConfiguration? LoadFromRulesetRoot(string rulesetRoot) =>
        LoadFromVslicesDirectory(Directory.GetParent(rulesetRoot)?.FullName);

    public static ProjectConfiguration? LoadFromProjectRoot(string projectRoot) =>
        LoadFromVslicesDirectory(Path.Combine(projectRoot, ".vslices"));

    public static ProjectConfiguration? LoadNearest(string start)
    {
        var current = new DirectoryInfo(Path.GetFullPath(start));
        while (current is not null)
        {
            var config = LoadFromProjectRoot(current.FullName);
            if (config is not null)
                return config;

            current = current.Parent;
        }

        return null;
    }

    public static async Task WriteAsync(
        string projectRoot,
        ProjectConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var vslicesDirectory = Path.Combine(projectRoot, ".vslices");
        Directory.CreateDirectory(vslicesDirectory);

        var root = new YamlMappingNode
        {
            { "version", configuration.Version },
            {
                "targets",
                new YamlMappingNode
                {
                    { "default", configuration.DefaultTarget ?? "csharp" }
                }
            }
        };

        var ruleset = new YamlMappingNode();
        if (!string.IsNullOrWhiteSpace(configuration.RulesetSource))
            ruleset.Add("source", configuration.RulesetSource);
        if (!string.IsNullOrWhiteSpace(configuration.RulesetRef))
            ruleset.Add("ref", configuration.RulesetRef);
        root.Add("ruleset", ruleset);

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

    private static ProjectConfiguration? LoadFromVslicesDirectory(string? vslicesDirectory)
    {
        if (string.IsNullOrWhiteSpace(vslicesDirectory))
            return null;

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
        var pullRequest = int.TryParse(pullRequestText, out var parsedPullRequest)
            ? parsedPullRequest
            : null;

        return new ProjectConfiguration(
            Scalar(root, "version") ?? CurrentVersion,
            NestedScalar(root, "targets", "default"),
            NestedScalar(root, "ruleset", "source"),
            NestedScalar(root, "ruleset", "ref"),
            NestedScalar(root, "updates", "source"),
            NestedScalar(root, "updates", "channel"),
            pullRequest);
    }

    private static string? NestedScalar(YamlMappingNode root, string section, string key)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(section), out var node) ||
            node is not YamlMappingNode mapping)
            return null;

        return Scalar(mapping, key);
    }

    private static string? Scalar(YamlMappingNode mapping, string key)
    {
        return mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
               node is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }
}
