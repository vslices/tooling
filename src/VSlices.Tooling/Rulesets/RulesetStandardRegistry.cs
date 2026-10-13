using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record RulesetStandardInstallation(
    string Name,
    string DeclaredName,
    bool IsLocal,
    string? Source,
    string? Reference,
    bool Legacy = false);

/// <summary>Project-owned registry of distinct Ruleset Standard installations, not a definition schema.</summary>
internal sealed class RulesetStandardRegistry
{
    private readonly List<RulesetStandardInstallation> installations;

    private RulesetStandardRegistry(VSlicesProjectContext project, List<RulesetStandardInstallation> installations)
    {
        Project = project;
        this.installations = installations;
    }

    public VSlicesProjectContext Project { get; }
    public string Root => Path.Combine(Project.VslicesRoot, "rulesets");
    private string RegistryPath => Path.Combine(Root, "registry.yaml");
    public IReadOnlyList<RulesetStandardInstallation> Installations => installations;

    public static RulesetStandardRegistry Load(VSlicesProjectContext project)
    {
        var root = Path.Combine(project.VslicesRoot, "rulesets");
        var path = Path.Combine(root, "registry.yaml");
        var entries = new List<RulesetStandardInstallation>();
        if (File.Exists(path))
        {
            using var reader = File.OpenText(path);
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode data)
                throw new InvalidDataException("RSREG001: Registry must be a YAML mapping.");

            var version = Scalar(data, "version");
            if (version != "0.1")
                throw new InvalidDataException("RSREG002: Unsupported Ruleset Standard registry version.");
            if (data.Children.Keys.Any(key => key is not YamlScalarNode scalar ||
                    scalar.Value is not ("version" or "standards")))
                throw new InvalidDataException("RSREG003: Registry contains unknown fields.");
            if (data.Children.TryGetValue(new YamlScalarNode("standards"), out var node))
            {
                if (node is not YamlSequenceNode sequence)
                    throw new InvalidDataException("RSREG004: Registry standards must be a sequence.");
                foreach (var entry in sequence.Children)
                {
                    if (entry is not YamlMappingNode mapping)
                        throw new InvalidDataException("RSREG005: Registry entry must be a mapping.");
                    if (mapping.Children.Keys.Any(key => key is not YamlScalarNode scalar ||
                            scalar.Value is not ("name" or "declared-name" or "local" or "source" or "ref")))
                        throw new InvalidDataException("RSREG006: Registry entry contains unknown fields.");
                    var name = Scalar(mapping, "name");
                    var declared = Scalar(mapping, "declared-name");
                    var local = Scalar(mapping, "local");
                    var source = Scalar(mapping, "source");
                    var reference = Scalar(mapping, "ref");
                    if (!IsValidName(name) || !IsValidName(declared) || local is not ("true" or "false") ||
                        (local == "false" && string.IsNullOrWhiteSpace(source)) ||
                        (local == "true" && (source is not null || reference is not null)))
                        throw new InvalidDataException("RSREG007: Registry entry has invalid identity or provenance.");
                    entries.Add(new RulesetStandardInstallation(name!, declared!, local == "true", source, reference));
                }
            }
        }

        // Preserve pre-collection installations without guessing that custom source content is official.
        var legacyRoot = Path.Combine(project.VslicesRoot, "ruleset");
        if (File.Exists(Path.Combine(legacyRoot, "manifest.yaml")))
        {
            var declared = RulesetStandardManifest.ReadName(legacyRoot);
            var name = declared ?? (project.Configuration.RulesetSource == ProjectConfiguration.OfficialRulesetSource
                ? "vslices"
                : "legacy");
            if (!IsValidName(name) || entries.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("RSREG008: Legacy Ruleset Standard installation collides with the registry.");
            entries.Add(new RulesetStandardInstallation(name, declared ?? name, false,
                project.Configuration.RulesetSource, project.Configuration.RulesetRef, Legacy: true));
        }

        if (entries.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count)
            throw new InvalidDataException("RSREG009: Duplicate Ruleset Standard installation names.");
        foreach (var item in entries.Where(item => !item.Legacy))
            if (!File.Exists(Path.Combine(GetPath(root, item), "manifest.yaml")))
                throw new InvalidDataException($"RSREG010: Snapshot for '{item.Name}' is missing manifest.yaml.");
        return new RulesetStandardRegistry(project, entries);
    }

    public IReadOnlyList<(string Name, string Root)> Targets(string target) =>
        installations.Where(item => Directory.Exists(Path.Combine(PathFor(item), target)))
            .Select(item => (item.Name, PathFor(item))).ToArray();

    public bool HasTarget(string target) => Targets(target).Count > 0;

    public RulesetStandardInstallation? Find(string name) =>
        installations.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public string PathFor(RulesetStandardInstallation installation) =>
        installation.Legacy ? Path.Combine(Project.VslicesRoot, "ruleset") : GetPath(Root, installation);

    public string NewPath(string name, bool local) =>
        Path.Combine(Root, local ? "local" : string.Empty, name);

    public void Add(RulesetStandardInstallation installation)
    {
        if (Find(installation.Name) is not null)
            throw new InvalidOperationException($"RSREG011: Ruleset Standard '{installation.Name}' already exists. Specify another name.");
        installations.Add(installation);
    }

    public void Remove(string name) => installations.RemoveAll(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Root);
        var data = new YamlMappingNode { { "version", "0.1" } };
        var sequence = new YamlSequenceNode();
        foreach (var installation in installations.Where(i => !i.Legacy).OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            var mapping = new YamlMappingNode
            {
                { "name", installation.Name },
                { "declared-name", installation.DeclaredName },
                { "local", installation.IsLocal ? "true" : "false" }
            };
            if (installation.Source is not null) mapping.Add("source", installation.Source);
            if (installation.Reference is not null) mapping.Add("ref", installation.Reference);
            sequence.Add(mapping);
        }
        data.Add("standards", sequence);
        var stream = new YamlStream(new YamlDocument(data));
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        await AtomicFile.WriteTextAsync(RegistryPath, writer.ToString(), cancellationToken);
    }

    public void MigrateLegacy()
    {
        var legacy = installations.FirstOrDefault(i => i.Legacy);
        if (legacy is null) return;
        var destination = NewPath(legacy.Name, false);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"RSREG012: Cannot migrate legacy installation: '{destination}' already exists.");
        Directory.CreateDirectory(Root);
        Directory.Move(PathFor(legacy), destination);
        installations.Remove(legacy);
        installations.Add(legacy with { Legacy = false });
    }

    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Equals("local", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("registry", StringComparison.OrdinalIgnoreCase) || name.Length > 100 ||
            !((name[0] is >= 'A' and <= 'Z') || (name[0] is >= 'a' and <= 'z')))
            return false;
        return name.All(ch => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_');
    }

    private static string GetPath(string root, RulesetStandardInstallation installation) =>
        Path.Combine(root, installation.IsLocal ? "local" : string.Empty, installation.Name);

    private static string? Scalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar
            ? scalar.Value : null;
}

internal static class RulesetStandardManifest
{
    public static string? ReadName(string root)
    {
        var path = Path.Combine(root, "manifest.yaml");
        if (!File.Exists(path)) throw new InvalidDataException($"RSSTD001: Missing Ruleset Standard manifest in '{root}'.");
        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode manifest)
            throw new InvalidDataException("RSSTD002: Ruleset Standard manifest must be a YAML mapping.");
        if (!manifest.Children.TryGetValue(new YamlScalarNode("name"), out var name))
            return null; // Legacy snapshots may predate manifest names.
        if (name is not YamlScalarNode scalar || !RulesetStandardRegistry.IsValidName(scalar.Value))
            throw new InvalidDataException("RSSTD003: Ruleset Standard manifest.name must be a valid identifier.");
        return scalar.Value;
    }
}
