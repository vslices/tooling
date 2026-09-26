using System.Text;
using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record RelationalRecommendation(string Family, string Type, string Role, string? QuestionId = null);

internal sealed record RelationalQuestionDefinition(
    string Id,
    string Text,
    string? Default,
    string? Connection,
    IReadOnlyList<RelationalRecommendation> Recommendations,
    IReadOnlyList<RelationalQuestionDefinition> Children);

internal sealed record NexusDefinition(
    string Type,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<RelationalQuestionDefinition> Questions,
    IReadOnlyList<RelationalRecommendation> Recommendations);

internal sealed record ContinuityPathDefinition(
    string Type,
    string Purpose,
    string RecommendedTraversal,
    RelationalQuestionDefinition RootQuestion);

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
            var nexusRoot = Path.Combine(root, "nexus");
            if (Directory.Exists(nexusRoot))
            {
                foreach (var file in Directory.EnumerateFiles(nexusRoot, "*.yml").OrderBy(x => x, StringComparer.Ordinal))
                {
                    var parsed = ParseNexus(file);
                    if (!parsed.IsSuccess) return RelationalStandardCatalogResult.Failure(parsed.Error!);
                    if (!nexus.TryAdd(parsed.Definition!.Type, parsed.Definition))
                        return RelationalStandardCatalogResult.Failure($"RELSTD001: Duplicate Nexus type '{parsed.Definition.Type}'.");
                }
            }

            var paths = new Dictionary<string, ContinuityPathDefinition>(StringComparer.OrdinalIgnoreCase);
            var pathsRoot = Path.Combine(root, "continuity-paths");
            if (Directory.Exists(pathsRoot))
            {
                foreach (var file in Directory.EnumerateFiles(pathsRoot, "*.yml").OrderBy(x => x, StringComparer.Ordinal))
                {
                    var parsed = ParsePath(file);
                    if (!parsed.IsSuccess) return RelationalStandardCatalogResult.Failure(parsed.Error!);
                    if (!paths.TryAdd(parsed.Definition!.Type, parsed.Definition))
                        return RelationalStandardCatalogResult.Failure($"RELSTD002: Duplicate Continuity Path type '{parsed.Definition.Type}'.");
                }
            }

            return RelationalStandardCatalogResult.Success(new RelationalStandardCatalog(nexus, paths));
        }
        catch (Exception ex)
        {
            return RelationalStandardCatalogResult.Failure($"RELSTD099: Could not load relational Docs Standard definitions: {ex.Message}");
        }
    }

    private static NexusParseResult ParseNexus(string path)
    {
        var loaded = LoadMapping(path);
        if (!loaded.IsSuccess) return NexusParseResult.Failure(loaded.Error!);
        var root = loaded.Mapping!;
        if (Scalar(root, "kind") != "vslices-nexus-definition" ||
            Scalar(root, "version") != "0.1" ||
            Mapping(root, "nexus") is not { } nexus)
            return NexusParseResult.Failure($"RELSTD010: '{path}' is not a supported Nexus definition.");

        var type = Scalar(nexus, "type");
        if (string.IsNullOrWhiteSpace(type))
            return NexusParseResult.Failure($"RELSTD011: Nexus definition '{path}' must declare nexus.type.");

        var scopes = SequenceScalars(nexus, "scopes");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var questions = new List<RelationalQuestionDefinition>();
        if (Sequence(nexus, "questions") is { } questionNodes)
        {
            foreach (var node in questionNodes.Children)
            {
                var parsed = ParseQuestion(node, ids, $"Nexus '{type}'");
                if (!parsed.IsSuccess) return NexusParseResult.Failure(parsed.Error!);
                questions.Add(parsed.Question!);
            }
        }

        return NexusParseResult.Success(new NexusDefinition(type, scopes, questions, ParseRecommendations(nexus, null)));
    }

    private static PathParseResult ParsePath(string path)
    {
        var loaded = LoadMapping(path);
        if (!loaded.IsSuccess) return PathParseResult.Failure(loaded.Error!);
        var root = loaded.Mapping!;
        if (Scalar(root, "kind") != "vslices-continuity-path-definition" ||
            Scalar(root, "version") != "0.1" ||
            Mapping(root, "continuity-path") is not { } pathNode)
            return PathParseResult.Failure($"RELSTD020: '{path}' is not a supported Continuity Path definition.");

        var type = Scalar(pathNode, "type");
        var purpose = Scalar(pathNode, "purpose");
        var traversal = Scalar(pathNode, "recommended-traversal");
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(purpose) ||
            string.IsNullOrWhiteSpace(traversal) ||
            !pathNode.Children.TryGetValue(new YamlScalarNode("question"), out var questionNode))
            return PathParseResult.Failure($"RELSTD021: Continuity Path definition '{path}' is incomplete.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var question = ParseQuestion(questionNode, ids, $"Continuity Path '{type}'");
        if (!question.IsSuccess) return PathParseResult.Failure(question.Error!);

        return PathParseResult.Success(new ContinuityPathDefinition(type, purpose.Trim(), traversal.Trim(), question.Question!));
    }

    private static RelQuestionParseResult ParseQuestion(YamlNode node, HashSet<string> ids, string owner)
    {
        if (node is not YamlMappingNode mapping)
            return RelQuestionParseResult.Failure($"RELSTD030: Question in {owner} must be a mapping.");

        var id = Scalar(mapping, "id");
        var text = Scalar(mapping, "text");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
            return RelQuestionParseResult.Failure($"RELSTD031: Question in {owner} must declare id and text.");
        if (!ids.Add(id))
            return RelQuestionParseResult.Failure($"RELSTD032: {owner} declares duplicate question id '{id}'.");

        var defaultValue = Scalar(mapping, "default");
        var connection = Mapping(mapping, "connection") is { } connectionNode ? Scalar(connectionNode, "text") : null;
        var children = new List<RelationalQuestionDefinition>();
        if (Sequence(mapping, "children") is { } childNodes)
        {
            foreach (var childNode in childNodes.Children)
            {
                var child = ParseQuestion(childNode, ids, owner);
                if (!child.IsSuccess) return child;
                children.Add(child.Question!);
            }
        }

        return RelQuestionParseResult.Success(new RelationalQuestionDefinition(
            id,
            text.Trim(),
            string.IsNullOrWhiteSpace(defaultValue) ? null : defaultValue.Trim(),
            string.IsNullOrWhiteSpace(connection) ? null : connection.Trim(),
            ParseRecommendations(mapping, id),
            children));
    }

    private static IReadOnlyList<RelationalRecommendation> ParseRecommendations(YamlMappingNode mapping, string? questionId)
    {
        var result = new List<RelationalRecommendation>();
        if (Sequence(mapping, "recommendations") is not { } sequence) return result;
        foreach (var node in sequence.Children.OfType<YamlMappingNode>())
        {
            var document = Scalar(node, "document");
            var nexus = Scalar(node, "nexus");
            var role = Scalar(node, "role");
            if (string.IsNullOrWhiteSpace(role)) continue;
            if (!string.IsNullOrWhiteSpace(document))
                result.Add(new RelationalRecommendation("document", document.Trim(), role.Trim(), questionId));
            else if (!string.IsNullOrWhiteSpace(nexus))
                result.Add(new RelationalRecommendation("nexus", nexus.Trim(), role.Trim(), questionId));
        }
        return result;
    }

    private static MappingLoadResult LoadMapping(string path)
    {
        try
        {
            using var reader = File.OpenText(path);
            var yaml = new YamlStream();
            yaml.Load(reader);
            return yaml.Documents.Count == 1 && yaml.Documents[0].RootNode is YamlMappingNode mapping
                ? MappingLoadResult.Success(mapping)
                : MappingLoadResult.Failure($"RELSTD090: '{path}' must contain one YAML mapping.");
        }
        catch (Exception ex)
        {
            return MappingLoadResult.Failure($"RELSTD091: Could not parse '{path}': {ex.Message}");
        }
    }

    private static string? Scalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar ? scalar.Value : null;
    private static YamlMappingNode? Mapping(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlMappingNode result ? result : null;
    private static YamlSequenceNode? Sequence(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlSequenceNode result ? result : null;
    private static IReadOnlyList<string> SequenceScalars(YamlMappingNode mapping, string key) =>
        Sequence(mapping, key)?.Children.OfType<YamlScalarNode>().Select(x => x.Value?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray() ?? [];

    private sealed record MappingLoadResult(YamlMappingNode? Mapping, string? Error)
    {
        public bool IsSuccess => Mapping is not null && Error is null;
        public static MappingLoadResult Success(YamlMappingNode mapping) => new(mapping, null);
        public static MappingLoadResult Failure(string error) => new(null, error);
    }
    private sealed record NexusParseResult(NexusDefinition? Definition, string? Error)
    {
        public bool IsSuccess => Definition is not null && Error is null;
        public static NexusParseResult Success(NexusDefinition definition) => new(definition, null);
        public static NexusParseResult Failure(string error) => new(null, error);
    }
    private sealed record PathParseResult(ContinuityPathDefinition? Definition, string? Error)
    {
        public bool IsSuccess => Definition is not null && Error is null;
        public static PathParseResult Success(ContinuityPathDefinition definition) => new(definition, null);
        public static PathParseResult Failure(string error) => new(null, error);
    }
    private sealed record RelQuestionParseResult(RelationalQuestionDefinition? Question, string? Error)
    {
        public bool IsSuccess => Question is not null && Error is null;
        public static RelQuestionParseResult Success(RelationalQuestionDefinition question) => new(question, null);
        public static RelQuestionParseResult Failure(string error) => new(null, error);
    }
}

internal sealed record ArtifactRelation(
    string Relation,
    string Path,
    string Kind,
    string Type,
    string Role,
    string? RecommendationId);

internal sealed record KnowledgeArtifactMetadata(
    string Kind,
    string Type,
    string? Scope,
    string? Target,
    string Status,
    string ToolingVersion,
    string? TemplateName,
    IReadOnlyList<ArtifactRelation> Relations,
    int ClosingLine);

internal sealed record KnowledgeArtifactMetadataResult(KnowledgeArtifactMetadata? Metadata, string? Error)
{
    public bool IsSuccess => Metadata is not null && Error is null;
    public static KnowledgeArtifactMetadataResult Success(KnowledgeArtifactMetadata metadata) => new(metadata, null);
    public static KnowledgeArtifactMetadataResult Failure(string error) => new(null, error);
}

internal static class KnowledgeArtifactFrontMatter
{
    public static KnowledgeArtifactMetadataResult Read(string source)
    {
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
            return KnowledgeArtifactMetadataResult.Failure("RELART001: Artifact requires YAML front-matter.");

        var closing = Array.FindIndex(lines, 1, line => line.Trim() == "---");
        if (closing < 0)
            return KnowledgeArtifactMetadataResult.Failure("RELART002: Artifact front-matter has no closing delimiter.");

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(string.Join("\n", lines.Skip(1).Take(closing - 1))));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return KnowledgeArtifactMetadataResult.Failure("RELART003: Artifact front-matter must be one YAML mapping.");

            if (!root.Children.TryGetValue(new YamlScalarNode("artifact"), out var artifactNode) || artifactNode is not YamlMappingNode artifact)
                return KnowledgeArtifactMetadataResult.Failure("RELART004: Front-matter requires artifact metadata.");

            var kind = Scalar(artifact, "kind");
            var type = Scalar(artifact, "type");
            var scope = Scalar(artifact, "scope");
            var target = Scalar(artifact, "target");
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(type))
                return KnowledgeArtifactMetadataResult.Failure("RELART005: artifact.kind and artifact.type are required.");

            var metadataNode = root.Children.TryGetValue(new YamlScalarNode("metadata"), out var metadataValue) &&
                               metadataValue is YamlMappingNode parsedMetadata
                ? parsedMetadata
                : null;
            var status = metadataNode is null ? "draft" : Scalar(metadataNode, "status") ?? "draft";

            var toolingNode = root.Children.TryGetValue(new YamlScalarNode("tooling"), out var toolingValue) &&
                              toolingValue is YamlMappingNode parsedTooling
                ? parsedTooling
                : null;
            var tooling = toolingNode is null ? "unknown" : Scalar(toolingNode, "version") ?? "unknown";
            var templateName = toolingNode is null
                ? null
                : toolingNode.Children.TryGetValue(new YamlScalarNode("template"), out var templateValue) &&
                  templateValue is YamlMappingNode templateMapping
                    ? Scalar(templateMapping, "name")
                    : null;

            var relations = new List<ArtifactRelation>();
            if (metadataNode is not null &&
                metadataNode.Children.TryGetValue(new YamlScalarNode("relates"), out var relationsNode) &&
                relationsNode is YamlSequenceNode sequence)
            {
                foreach (var node in sequence.Children.OfType<YamlMappingNode>())
                {
                    var relation = Scalar(node, "relation") ?? "related";
                    var relationPath = Scalar(node, "target");
                    var relationKind = Scalar(node, "kind") ?? "artifact";
                    var relationType = Scalar(node, "type") ?? "unknown";
                    var role = Scalar(node, "role") ?? "Relacionado";
                    var recommendation = Scalar(node, "recommendation");
                    if (!string.IsNullOrWhiteSpace(relationPath))
                    {
                        relations.Add(new ArtifactRelation(
                            relation.Trim(),
                            relationPath.Trim(),
                            relationKind.Trim(),
                            relationType.Trim(),
                            role.Trim(),
                            string.IsNullOrWhiteSpace(recommendation) ? null : recommendation.Trim()));
                    }
                }
            }

            return KnowledgeArtifactMetadataResult.Success(
                new KnowledgeArtifactMetadata(
                    kind.Trim(),
                    type.Trim(),
                    scope?.Trim(),
                    target?.Trim(),
                    status.Trim(),
                    tooling.Trim(),
                    templateName?.Trim(),
                    relations,
                    closing));
        }
        catch (Exception ex)
        {
            return KnowledgeArtifactMetadataResult.Failure($"RELART006: Could not parse artifact front-matter: {ex.Message}");
        }
    }

    public static string Render(
        string kind,
        string type,
        string? scope,
        string? target,
        string status,
        string? templateName,
        IReadOnlyList<ArtifactRelation> relations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("artifact:");
        sb.AppendLine($"  kind: {YamlScalar(kind)}");
        sb.AppendLine($"  type: {YamlScalar(type)}");
        if (!string.IsNullOrWhiteSpace(scope)) sb.AppendLine($"  scope: {YamlScalar(scope!)}");
        if (!string.IsNullOrWhiteSpace(target)) sb.AppendLine($"  target: {YamlScalar(target!)}");
        sb.AppendLine();
        sb.AppendLine("metadata:");
        sb.AppendLine($"  status: {YamlScalar(status)}");
        if (relations.Count == 0)
        {
            sb.AppendLine("  relates: []");
        }
        else
        {
            sb.AppendLine("  relates:");
            foreach (var relation in relations)
            {
                sb.AppendLine($"    - relation: {YamlScalar(relation.Relation)}");
                sb.AppendLine($"      target: {YamlScalar(relation.Path)}");
                sb.AppendLine($"      kind: {YamlScalar(relation.Kind)}");
                sb.AppendLine($"      type: {YamlScalar(relation.Type)}");
                sb.AppendLine($"      role: {YamlScalar(relation.Role)}");
                if (!string.IsNullOrWhiteSpace(relation.RecommendationId))
                    sb.AppendLine($"      recommendation: {YamlScalar(relation.RecommendationId!)}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("tooling:");
        sb.AppendLine($"  version: {YamlScalar(CliVersion.Display)}");
        sb.AppendLine("  schema:");
        sb.AppendLine("    version: 0.1.0");
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            sb.AppendLine("  template:");
            sb.AppendLine($"    name: {YamlScalar(templateName!)}");
            sb.AppendLine("    version: 0.1.0");
        }
        sb.AppendLine("---");
        return sb.ToString().TrimEnd();
    }

    public static string WithMetadata(
        string source,
        KnowledgeArtifactMetadata metadata,
        IReadOnlyList<ArtifactRelation> relations)
    {
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var current = Read(normalized);
        var body = current.IsSuccess
            ? string.Join("\n", normalized.Split('\n').Skip(current.Metadata!.ClosingLine + 1)).TrimStart('\n')
            : normalized.TrimStart();
        return Render(
            metadata.Kind,
            metadata.Type,
            metadata.Scope,
            metadata.Target,
            metadata.Status,
            metadata.TemplateName,
            relations) + "\n\n" + body;
    }

    private static string? Scalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar ? scalar.Value : null;

    private static string YamlScalar(string value)
    {
        var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}

internal sealed record RelationalQuestionSurface(
    string SelectionPath,
    string Id,
    string Text,
    bool IsAnswered,
    string? Answer,
    string? ParentSelectionPath,
    IReadOnlyList<RelationalRecommendation> Recommendations);

internal sealed record RelationalArtifactState(KnowledgeArtifactMetadata Metadata, IReadOnlyList<RelationalQuestionSurface> Questions, string Source);

internal sealed record RelationalArtifactStateResult(RelationalArtifactState? State, string? Error)
{
    public bool IsSuccess => State is not null && Error is null;
    public static RelationalArtifactStateResult Success(RelationalArtifactState state) => new(state, null);
    public static RelationalArtifactStateResult Failure(string error) => new(null, error);
}

internal static class RelationalArtifact
{
    private const string QuestionPrefix = "<!-- vslices:artifact-question id=";
    private const string QuestionSuffix = " -->";
    private const string QuestionEnd = "<!-- /vslices:artifact-question -->";
    private const string AssociatedStart = "<!-- vslices:associated-artifacts -->";
    private const string AssociatedEnd = "<!-- /vslices:associated-artifacts -->";

    public static string CreateNexus(
        NexusDefinition definition,
        string target,
        string? scope)
    {
        var effectiveScope = scope ?? definition.Scopes.FirstOrDefault();
        var sb = new StringBuilder();
        sb.AppendLine(KnowledgeArtifactFrontMatter.Render(
            "nexus",
            definition.Type,
            effectiveScope,
            target: target,
            status: "draft",
            templateName: $"{definition.Type}.nexus",
            relations: []));
        sb.AppendLine();
        sb.AppendLine($"# Nexus {definition.Type} de {target}");
        sb.AppendLine();
        if (definition.Questions.Count > 0)
        {
            sb.AppendLine("## Preguntas abiertas");
            sb.AppendLine();
            foreach (var question in definition.Questions)
                RenderQuestionTree(sb, question, 3);
        }
        RenderAssociatedArtifacts(sb, []);
        return sb.ToString();
    }

    public static string CreateContinuityPath(
        ContinuityPathDefinition definition,
        string target,
        string? scope)
    {
        var sb = new StringBuilder();
        sb.AppendLine(KnowledgeArtifactFrontMatter.Render(
            "continuity-path",
            definition.Type,
            scope ?? definition.Type,
            target: target,
            status: "draft",
            templateName: $"{definition.Type}.continuity-path",
            relations: []));
        sb.AppendLine();
        sb.AppendLine($"# Camino de continuidad \"{definition.Type}\" de {target}");
        sb.AppendLine();
        sb.AppendLine("## Propósito del recorrido");
        RenderQuestionBlock(sb, "__purpose", definition.Purpose);
        sb.AppendLine();
        sb.AppendLine("## Preguntas de continuidad");
        sb.AppendLine();
        RenderQuestionTree(sb, definition.RootQuestion, 3);
        sb.AppendLine("## Diagrama de continuidad");
        sb.AppendLine();
        sb.AppendLine("### Recorrido recomendado");
        RenderQuestionBlock(sb, "__recommended-traversal", definition.RecommendedTraversal);
        sb.AppendLine();
        sb.AppendLine("### Grafo");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TD");
        var graph = new List<string>();
        BuildMermaid(definition.RootQuestion, graph);
        foreach (var line in graph) sb.AppendLine(line);
        sb.AppendLine("```");
        sb.AppendLine();
        RenderAssociatedArtifacts(sb, []);
        return sb.ToString();
    }

    public static RelationalArtifactStateResult ReadNexus(string source, NexusDefinition definition)
    {
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!metadata.IsSuccess) return RelationalArtifactStateResult.Failure(metadata.Error!);
        if (metadata.Metadata!.Kind != "nexus" || metadata.Metadata.Type != definition.Type)
            return RelationalArtifactStateResult.Failure("RELART010: Nexus metadata does not match requested definition.");
        var answers = ReadQuestionAnswers(source);
        return RelationalArtifactStateResult.Success(
            new RelationalArtifactState(metadata.Metadata, BuildNexusSurface(definition, answers), source));
    }

    public static RelationalArtifactStateResult ReadContinuityPath(string source, ContinuityPathDefinition definition)
    {
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!metadata.IsSuccess) return RelationalArtifactStateResult.Failure(metadata.Error!);
        if (metadata.Metadata!.Kind != "continuity-path" || metadata.Metadata.Type != definition.Type)
            return RelationalArtifactStateResult.Failure("RELART011: Continuity Path metadata does not match requested definition.");

        var answers = ReadQuestionAnswers(source);
        var surfaces = new List<RelationalQuestionSurface>
        {
            new("1", "__purpose", "Propósito del recorrido", HasAnswer(answers, "__purpose"), Answer(answers, "__purpose"), null, []),
            new("2", "__recommended-traversal", "Recorrido recomendado", HasAnswer(answers, "__recommended-traversal"), Answer(answers, "__recommended-traversal"), null, [])
        };
        AddProgressiveQuestion(definition.RootQuestion, "3", null, answers, surfaces);
        return RelationalArtifactStateResult.Success(new RelationalArtifactState(metadata.Metadata, surfaces, source));
    }

    public static string UpdateQuestion(RelationalArtifactState state, string selectionPath, string answer, out string? error)
    {
        error = null;
        var selected = state.Questions.FirstOrDefault(x => x.SelectionPath == selectionPath);
        if (selected is null)
        {
            error = $"RELART020: Selection '{selectionPath}' is not available.";
            return state.Source;
        }

        var normalized = state.Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var startMarker = QuestionPrefix + selected.Id + QuestionSuffix;
        var start = normalized.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            error = $"RELART021: Question marker '{selected.Id}' is missing from artifact.";
            return state.Source;
        }

        var contentStart = start + startMarker.Length;
        var end = normalized.IndexOf(QuestionEnd, contentStart, StringComparison.Ordinal);
        if (end < 0)
        {
            error = $"RELART022: Question '{selected.Id}' has no closing marker.";
            return state.Source;
        }

        var replacement = startMarker + "\n" + answer.Trim() + "\n" + QuestionEnd;
        return normalized[..start] + replacement + normalized[(end + QuestionEnd.Length)..];
    }

    public static string AddRelation(string source, ArtifactRelation relation, out string? error)
    {
        error = null;
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!metadata.IsSuccess)
        {
            error = metadata.Error;
            return source;
        }

        var relations = metadata.Metadata!.Relations.ToList();
        var existing = relations.FindIndex(x => x.Path.Equals(relation.Path, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) relations[existing] = relation; else relations.Add(relation);

        var updated = KnowledgeArtifactFrontMatter.WithMetadata(
            source,
            metadata.Metadata,
            relations);
        return ReplaceAssociatedArtifacts(updated, relations);
    }

    public static IReadOnlyList<(string Id, RelationalRecommendation Recommendation)> EnumerateRecommendations(
        NexusDefinition definition,
        RelationalArtifactState state)
    {
        var result = new List<(string, RelationalRecommendation)>();

        foreach (var question in state.Questions)
        {
            for (var index = 0; index < question.Recommendations.Count; index++)
                result.Add(($"{question.SelectionPath}.{index + 1}", question.Recommendations[index]));
        }

        var rootOffset = definition.Questions.Count;
        for (var index = 0; index < definition.Recommendations.Count; index++)
            result.Add(((rootOffset + index + 1).ToString(), definition.Recommendations[index]));

        return result;
    }

    public static IReadOnlyList<(string Id, RelationalRecommendation Recommendation)> EnumerateRecommendations(
        ContinuityPathDefinition definition,
        RelationalArtifactState state)
    {
        var result = new List<(string, RelationalRecommendation)>();

        foreach (var question in state.Questions.Where(question =>
                     question.Id is not "__purpose" and not "__recommended-traversal"))
        {
            for (var index = 0; index < question.Recommendations.Count; index++)
                result.Add(($"{question.SelectionPath}.{index + 1}", question.Recommendations[index]));
        }

        return result;
    }

    private static IReadOnlyList<RelationalQuestionSurface> BuildNexusSurface(
        NexusDefinition definition,
        IReadOnlyDictionary<string, string> answers)
    {
        var result = new List<RelationalQuestionSurface>();
        var top = 1;
        foreach (var question in definition.Questions)
            AddProgressiveQuestion(question, (top++).ToString(), null, answers, result);
        return result;
    }

    private static void AddProgressiveQuestion(
        RelationalQuestionDefinition question,
        string path,
        string? parentPath,
        IReadOnlyDictionary<string, string> answers,
        List<RelationalQuestionSurface> result)
    {
        var answered = HasAnswer(answers, question.Id);
        result.Add(new RelationalQuestionSurface(
            path, question.Id, question.Text, answered, Answer(answers, question.Id), parentPath, question.Recommendations));

        if (!answered) return;
        var childOffset = question.Recommendations.Count;
        for (var index = 0; index < question.Children.Count; index++)
            AddProgressiveQuestion(
                question.Children[index],
                $"{path}.{childOffset + index + 1}",
                path,
                answers,
                result);
    }

    private static Dictionary<string, string> ReadQuestionAnswers(string source)
    {
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var search = 0;
        while (true)
        {
            var start = normalized.IndexOf(QuestionPrefix, search, StringComparison.Ordinal);
            if (start < 0) break;
            var idStart = start + QuestionPrefix.Length;
            var idEnd = normalized.IndexOf(QuestionSuffix, idStart, StringComparison.Ordinal);
            if (idEnd < 0) break;
            var id = normalized[idStart..idEnd];
            var contentStart = idEnd + QuestionSuffix.Length;
            var end = normalized.IndexOf(QuestionEnd, contentStart, StringComparison.Ordinal);
            if (end < 0) break;
            result[id] = normalized[contentStart..end].Trim();
            search = end + QuestionEnd.Length;
        }
        return result;
    }

    private static bool HasAnswer(IReadOnlyDictionary<string, string> answers, string id) =>
        answers.TryGetValue(id, out var answer) && !string.IsNullOrWhiteSpace(answer);
    private static string? Answer(IReadOnlyDictionary<string, string> answers, string id) =>
        answers.TryGetValue(id, out var answer) && !string.IsNullOrWhiteSpace(answer) ? answer : null;

    private static void RenderQuestionTree(StringBuilder sb, RelationalQuestionDefinition question, int level)
    {
        sb.AppendLine(new string('#', Math.Min(level, 6)) + " " + question.Text);
        RenderQuestionBlock(sb, question.Id, question.Default);
        sb.AppendLine();
        foreach (var child in question.Children) RenderQuestionTree(sb, child, level + 1);
    }

    private static void RenderQuestionBlock(StringBuilder sb, string id, string? answer)
    {
        sb.AppendLine(QuestionPrefix + id + QuestionSuffix);
        if (!string.IsNullOrWhiteSpace(answer)) sb.AppendLine(answer.Trim());
        sb.AppendLine(QuestionEnd);
    }

    private static void BuildMermaid(RelationalQuestionDefinition question, List<string> lines)
    {
        var id = MermaidId(question.Id);
        lines.Add($"  {id}[\"{EscapeMermaid(question.Text)}\"]");
        foreach (var child in question.Children)
        {
            var childId = MermaidId(child.Id);
            lines.Add(string.IsNullOrWhiteSpace(child.Connection)
                ? $"  {id} --> {childId}"
                : $"  {id} -- \"{EscapeMermaid(child.Connection!)}\" --> {childId}");
            BuildMermaid(child, lines);
        }
    }

    private static string MermaidId(string id) => "q_" + new string(id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    private static string EscapeMermaid(string value) => value.Replace("\"", "'", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static void RenderAssociatedArtifacts(StringBuilder sb, IReadOnlyList<ArtifactRelation> relations)
    {
        sb.AppendLine("## Artifacts asociados");
        sb.AppendLine();
        sb.AppendLine(AssociatedStart);
        sb.AppendLine("| Artifact | Utilidad | Link |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var relation in relations)
        {
            var name = Path.GetFileNameWithoutExtension(relation.Path);
            sb.AppendLine($"| {EscapeCell(name)} | {EscapeCell(relation.Role)} | [{EscapeCell(relation.Path)}]({relation.Path.Replace('\\', '/')}) |");
        }
        sb.AppendLine(AssociatedEnd);
        sb.AppendLine();
    }

    private static string ReplaceAssociatedArtifacts(string source, IReadOnlyList<ArtifactRelation> relations)
    {
        var start = source.IndexOf(AssociatedStart, StringComparison.Ordinal);
        var end = source.IndexOf(AssociatedEnd, StringComparison.Ordinal);
        if (start < 0 || end < start) return source;

        var sb = new StringBuilder();
        sb.AppendLine(AssociatedStart);
        sb.AppendLine("| Artifact | Utilidad | Link |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var relation in relations)
        {
            var name = Path.GetFileNameWithoutExtension(relation.Path);
            sb.AppendLine($"| {EscapeCell(name)} | {EscapeCell(relation.Role)} | [{EscapeCell(relation.Path)}]({relation.Path.Replace('\\', '/')}) |");
        }
        sb.Append(AssociatedEnd);
        return source[..start] + sb + source[(end + AssociatedEnd.Length)..];
    }

    private static string EscapeCell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
