using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record RelationalRecommendation(string Family, string Type, string Role, string? QuestionId = null);
internal sealed record RelationalQuestionDefinition(
    string Id, string Text, string? Default, string? Connection,
    IReadOnlyList<RelationalRecommendation> Recommendations,
    IReadOnlyList<RelationalQuestionDefinition> Children);
internal sealed record NexusDefinition(
    string Type, IReadOnlyList<string> Scopes,
    IReadOnlyList<RelationalQuestionDefinition> Questions,
    IReadOnlyList<RelationalRecommendation> Recommendations);
internal sealed record ContinuityPathDefinition(
    string Type, string Purpose, string RecommendedTraversal, RelationalQuestionDefinition RootQuestion);
internal sealed record RelationalStandardCatalogResult(RelationalStandardCatalog? Catalog, string? Error)
{
    public bool IsSuccess => Catalog is not null && Error is null;
    public static RelationalStandardCatalogResult Success(RelationalStandardCatalog catalog) => new(catalog, null);
    public static RelationalStandardCatalogResult Failure(string error) => new(null, error);
}

internal sealed class RelationalStandardCatalog
{
    private readonly IReadOnlyDictionary<string, NexusDefinition> nexus;
    private readonly IReadOnlyDictionary<string, ContinuityPathDefinition> paths;

    private RelationalStandardCatalog(
        IReadOnlyDictionary<string, NexusDefinition> nexus,
        IReadOnlyDictionary<string, ContinuityPathDefinition> paths)
    {
        this.nexus = nexus;
        this.paths = paths;
    }

    public IEnumerable<NexusDefinition> Nexus => nexus.Values;
    public IEnumerable<ContinuityPathDefinition> ContinuityPaths => paths.Values;
    public bool TryGetNexus(string type, out NexusDefinition? definition) => nexus.TryGetValue(type, out definition);
    public bool TryGetContinuityPath(string type, out ContinuityPathDefinition? definition) => paths.TryGetValue(type, out definition);

    public static RelationalStandardCatalogResult Load(string root)
    {
        try
        {
            var nexus = new Dictionary<string, NexusDefinition>(StringComparer.OrdinalIgnoreCase);
            var paths = new Dictionary<string, ContinuityPathDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var family in new[] { "nexus", "continuity-paths" })
            {
                var directory = Path.Combine(root, family);
                if (File.Exists(directory))
                    return RelationalStandardCatalogResult.Failure($"RELSTD003: Candidate surface '{directory}' must be a directory.");
                if (!Directory.Exists(directory)) continue;

                foreach (var file in Directory.EnumerateFiles(directory, "*.yml").OrderBy(x => x, StringComparer.Ordinal))
                {
                    using var reader = File.OpenText(file);
                    var yaml = new YamlStream();
                    yaml.Load(reader);
                    if (yaml.Documents.Count != 1)
                        return RelationalStandardCatalogResult.Failure($"RELSTD090: '{file}' must contain one YAML document.");
                    var validation = new KnowledgeYamlReader("RELSTD", file);
                    if (family == "nexus")
                    {
                        var definition = ReadNexus(yaml.Documents[0].RootNode, validation);
                        if (validation.Error is not null) return RelationalStandardCatalogResult.Failure(validation.Error);
                        if (!nexus.TryAdd(definition!.Type, definition))
                            return RelationalStandardCatalogResult.Failure($"RELSTD001: Duplicate Nexus type '{definition.Type}'.");
                    }
                    else
                    {
                        var definition = ReadPath(yaml.Documents[0].RootNode, validation);
                        if (validation.Error is not null) return RelationalStandardCatalogResult.Failure(validation.Error);
                        if (!paths.TryAdd(definition!.Type, definition))
                            return RelationalStandardCatalogResult.Failure($"RELSTD002: Duplicate Continuity Path type '{definition.Type}'.");
                    }
                }
            }
            return RelationalStandardCatalogResult.Success(new RelationalStandardCatalog(nexus, paths));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or YamlDotNet.Core.YamlException or ArgumentException)
        {
            return RelationalStandardCatalogResult.Failure($"RELSTD099: Could not load relational definitions: {ex.Message}");
        }
    }

    private static NexusDefinition? ReadNexus(YamlNode node, KnowledgeYamlReader reader)
    {
        var root = reader.Mapping(node, "root", "kind", "version", "nexus");
        if (root is null) return null;
        reader.Expect(reader.Scalar(root, "kind", true), "vslices-nexus-definition", "kind");
        reader.Expect(reader.Scalar(root, "version", true), "0.1", "version");
        var nexus = reader.ChildMapping(root, "nexus", true, "type", "scopes", "questions", "recommendations");
        if (nexus is null) return null;
        var type = reader.Scalar(nexus, "type", true);
        var scopes = new List<string>();
        foreach (var scope in reader.Sequence(nexus, "scopes"))
        {
            var value = reader.NodeScalar(scope, "nexus.scopes[]", true);
            if (value is not null) scopes.Add(value);
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var questions = new List<RelationalQuestionDefinition>();
        foreach (var questionNode in reader.Sequence(nexus, "questions"))
        {
            var question = ReadQuestion(questionNode, ids, reader);
            if (question is not null) questions.Add(question);
        }
        var recommendations = ReadRecommendations(nexus, null, reader);
        return reader.Error is null ? new NexusDefinition(type!, scopes, questions, recommendations) : null;
    }

    private static ContinuityPathDefinition? ReadPath(YamlNode node, KnowledgeYamlReader reader)
    {
        var root = reader.Mapping(node, "root", "kind", "version", "continuity-path");
        if (root is null) return null;
        reader.Expect(reader.Scalar(root, "kind", true), "vslices-continuity-path-definition", "kind");
        reader.Expect(reader.Scalar(root, "version", true), "0.1", "version");
        var path = reader.ChildMapping(root, "continuity-path", true, "type", "purpose", "recommended-traversal", "question");
        if (path is null) return null;
        var type = reader.Scalar(path, "type", true);
        var purpose = reader.Scalar(path, "purpose", true);
        var traversal = reader.Scalar(path, "recommended-traversal", true);
        if (!path.Children.TryGetValue(new YamlScalarNode("question"), out var questionNode))
        {
            reader.Fail("continuity-path.question is required.");
            return null;
        }
        var question = ReadQuestion(questionNode, new HashSet<string>(StringComparer.Ordinal), reader);
        return reader.Error is null ? new ContinuityPathDefinition(type!, purpose!, traversal!, question!) : null;
    }

    private static RelationalQuestionDefinition? ReadQuestion(
        YamlNode node, HashSet<string> ids, KnowledgeYamlReader reader)
    {
        var mapping = reader.Mapping(node, "question", "id", "text", "default", "connection", "recommendations", "children");
        if (mapping is null) return null;
        var id = reader.Scalar(mapping, "id", true);
        var text = reader.Scalar(mapping, "text", true);
        if (id is not null && (!ids.Add(id) || id is "__purpose" or "__recommended-traversal" ||
                               id.Contains("-->", StringComparison.Ordinal) || id.Any(char.IsControl)))
            reader.Fail($"Question id '{id}' is duplicated or cannot be represented by the current question-marker profile.");
        var value = reader.Scalar(mapping, "default");
        var connection = reader.ChildMapping(mapping, "connection", false, "text");
        var connectionText = connection is null ? null : reader.Scalar(connection, "text", true);
        var children = new List<RelationalQuestionDefinition>();
        foreach (var childNode in reader.Sequence(mapping, "children"))
        {
            var child = ReadQuestion(childNode, ids, reader);
            if (child is not null) children.Add(child);
        }
        var recommendations = ReadRecommendations(mapping, id, reader);
        return reader.Error is null
            ? new RelationalQuestionDefinition(id!, text!, value, connectionText, recommendations, children)
            : null;
    }

    private static IReadOnlyList<RelationalRecommendation> ReadRecommendations(
        YamlMappingNode mapping, string? questionId, KnowledgeYamlReader reader)
    {
        var result = new List<RelationalRecommendation>();
        foreach (var node in reader.Sequence(mapping, "recommendations"))
        {
            var recommendation = reader.Mapping(node, "recommendation", "document", "nexus", "role");
            if (recommendation is null) continue;
            var hasDocument = recommendation.Children.ContainsKey(new YamlScalarNode("document"));
            var hasNexus = recommendation.Children.ContainsKey(new YamlScalarNode("nexus"));
            if (hasDocument == hasNexus)
            {
                reader.Fail("A recommendation must reference exactly one of document or nexus.");
                continue;
            }
            var family = hasDocument ? "document" : "nexus";
            var type = reader.Scalar(recommendation, family, true);
            var role = reader.Scalar(recommendation, "role", true);
            if (type is not null && role is not null)
                result.Add(new RelationalRecommendation(family, type, role, questionId));
        }
        return result;
    }
}

// Structural validation is a mechanism. It does not supply missing vocabulary or semantics.
internal sealed class KnowledgeYamlReader(string diagnosticPrefix, string source)
{
    public string? Error { get; private set; }
    public void Fail(string message) => Error ??= $"{diagnosticPrefix}100: {source}: {message}";
    public void Expect(string? actual, string expected, string field)
    {
        if (actual != expected) Fail($"{field} must be '{expected}'.");
    }

    public YamlMappingNode? Mapping(YamlNode node, string field, params string[] allowed)
    {
        if (node is not YamlMappingNode mapping)
        {
            Fail($"{field} must be a mapping.");
            return null;
        }
        foreach (var key in mapping.Children.Keys)
        {
            if (key is not YamlScalarNode scalar || scalar.Value is null ||
                !allowed.Contains(scalar.Value, StringComparer.Ordinal))
                Fail($"{field} contains an unknown or non-scalar key '{key}'.");
        }
        return mapping;
    }

    public YamlMappingNode? ChildMapping(YamlMappingNode parent, string key, bool required, params string[] allowed)
    {
        if (parent.Children.TryGetValue(new YamlScalarNode(key), out var node)) return Mapping(node, key, allowed);
        if (required) Fail($"{key} is required.");
        return null;
    }

    public string? Scalar(YamlMappingNode parent, string key, bool required = false)
    {
        if (parent.Children.TryGetValue(new YamlScalarNode(key), out var node)) return NodeScalar(node, key, true);
        if (required) Fail($"{key} is required.");
        return null;
    }

    public string? NodeScalar(YamlNode node, string field, bool required)
    {
        if (node is not YamlScalarNode scalar || (required && string.IsNullOrWhiteSpace(scalar.Value)))
        {
            Fail($"{field} must be a non-empty scalar.");
            return null;
        }
        return scalar.Value?.Trim();
    }

    public IReadOnlyList<YamlNode> Sequence(YamlMappingNode parent, string key)
    {
        if (!parent.Children.TryGetValue(new YamlScalarNode(key), out var node)) return [];
        if (node is YamlSequenceNode sequence) return sequence.Children.ToArray();
        Fail($"{key} must be a sequence.");
        return [];
    }
}
