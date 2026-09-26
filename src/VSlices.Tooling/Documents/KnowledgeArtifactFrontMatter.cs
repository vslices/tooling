using System.Text;
using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record ArtifactRelation(
    string Relation, string Path, string Kind, string Type, string Role,
    string? RecommendationId, string? RecommendationContext = null);
internal sealed record KnowledgeArtifactMetadata(
    string Kind, string Type, string? Scope, string? Target, string Status,
    string ToolingVersion, string? TemplateName,
    IReadOnlyList<ArtifactRelation> Relations, int ClosingLine,
    string? TemplateVersion = null);
internal sealed record KnowledgeArtifactMetadataResult(KnowledgeArtifactMetadata? Metadata, string? Error)
{
    public bool IsSuccess => Metadata is not null && Error is null;
    public static KnowledgeArtifactMetadataResult Success(KnowledgeArtifactMetadata metadata) => new(metadata, null);
    public static KnowledgeArtifactMetadataResult Failure(string error) => new(null, error);
}

internal static class KnowledgeArtifactFrontMatter
{
    public static bool IsKnownKind(string? kind) => kind is "document" or "nexus" or "continuity-path";
    public static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public static string Normalize(string source) => source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    public static KnowledgeArtifactMetadataResult Read(string source)
    {
        var lines = Normalize(source).Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
            return KnowledgeArtifactMetadataResult.Failure("RELART001: Artifact requires YAML front-matter.");
        var closing = Array.FindIndex(lines, 1, line => line.Trim() == "---");
        if (closing < 0) return KnowledgeArtifactMetadataResult.Failure("RELART002: Artifact front-matter has no closing delimiter.");
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(string.Join("\n", lines.Skip(1).Take(closing - 1))));
            if (yaml.Documents.Count != 1)
                return KnowledgeArtifactMetadataResult.Failure("RELART003: Front-matter must contain one YAML document.");
            var reader = new KnowledgeYamlReader("RELART", "artifact front-matter");
            var root = reader.Mapping(yaml.Documents[0].RootNode, "root", "artifact", "metadata", "tooling");
            if (root is null) return KnowledgeArtifactMetadataResult.Failure(reader.Error!);
            var artifact = reader.ChildMapping(root, "artifact", true, "kind", "type", "scope", "target");
            if (artifact is null) return KnowledgeArtifactMetadataResult.Failure(reader.Error!);
            var kind = reader.Scalar(artifact, "kind", true);
            var type = reader.Scalar(artifact, "type", true);
            var scope = reader.Scalar(artifact, "scope");
            var target = reader.Scalar(artifact, "target");
            if (!IsKnownKind(kind)) reader.Fail($"Unsupported artifact.kind '{kind}'.");
            if (!IsIdentifier(type)) reader.Fail("artifact.type must be a stable identifier.");

            // Legacy Documents with only kind/type remain readable. Creation has the stronger target requirement.
            var metadata = reader.ChildMapping(root, "metadata", false, "status", "relates");
            var status = metadata is null ? "draft" : reader.Scalar(metadata, "status") ?? "draft";
            var relations = new List<ArtifactRelation>();
            var paths = new HashSet<string>(PathComparer);
            var selections = new HashSet<string>(StringComparer.Ordinal);
            if (metadata is not null)
            {
                foreach (var node in reader.Sequence(metadata, "relates"))
                {
                    var relation = reader.Mapping(node, "relation", "relation", "target", "kind", "type", "role", "recommendation", "recommendation-context");
                    if (relation is null) continue;
                    var semantics = reader.Scalar(relation, "relation", true);
                    var path = reader.Scalar(relation, "target", true);
                    var relatedKind = reader.Scalar(relation, "kind", true);
                    var relatedType = reader.Scalar(relation, "type", true);
                    var role = reader.Scalar(relation, "role", true);
                    var selection = reader.Scalar(relation, "recommendation");
                    var context = reader.Scalar(relation, "recommendation-context");
                    if (semantics != "related") reader.Fail($"Unsupported relation semantics '{semantics}'.");
                    if (!IsKnownKind(relatedKind) || !IsIdentifier(relatedType)) reader.Fail("Relation kind/type is invalid or unsupported.");
                    if (path is not null && !paths.Add(path)) reader.Fail($"Duplicate related path '{path}'.");
                    if (selection is not null && (!IsSelection(selection) || !selections.Add(selection)))
                        reader.Fail($"Invalid or duplicate recommendation selection '{selection}'.");
                    if (context is not null && (selection is null || context.Length != 64 || !context.All(Uri.IsHexDigit)))
                        reader.Fail("recommendation-context requires a recommendation and a SHA-256 context fingerprint.");
                    if (reader.Error is null)
                        relations.Add(new ArtifactRelation(semantics!, path!, relatedKind!, relatedType!, role!, selection, context));
                }
            }

            var tooling = reader.ChildMapping(root, "tooling", false, "version", "schema", "template");
            var toolingVersion = tooling is null ? "unknown" : reader.Scalar(tooling, "version") ?? "unknown";
            string? templateName = null;
            string? templateVersion = null;
            if (tooling is not null)
            {
                var schema = reader.ChildMapping(tooling, "schema", false, "version");
                if (schema is not null) reader.Expect(reader.Scalar(schema, "version", true), "0.1.0", "tooling.schema.version");
                var template = reader.ChildMapping(tooling, "template", false, "name", "version");
                if (template is not null)
                {
                    templateName = reader.Scalar(template, "name", true);
                    templateVersion = reader.Scalar(template, "version", true);
                }
            }
            return reader.Error is not null
                ? KnowledgeArtifactMetadataResult.Failure(reader.Error)
                : KnowledgeArtifactMetadataResult.Success(new KnowledgeArtifactMetadata(
                    kind!, type!, scope, target, status, toolingVersion, templateName, relations, closing, templateVersion));
        }
        catch (Exception ex) when (ex is YamlDotNet.Core.YamlException or ArgumentException)
        {
            return KnowledgeArtifactMetadataResult.Failure($"RELART006: Could not parse artifact front-matter: {ex.Message}");
        }
    }

    public static string Render(
        string kind, string type, string? scope, string? target, string status,
        string? templateName, IReadOnlyList<ArtifactRelation> relations,
        string? templateVersion = "0.1.0")
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("artifact:");
        sb.AppendLine($"  kind: {YamlScalar(kind)}");
        sb.AppendLine($"  type: {YamlScalar(type)}");
        if (!string.IsNullOrWhiteSpace(scope)) sb.AppendLine($"  scope: {YamlScalar(scope)}");
        if (!string.IsNullOrWhiteSpace(target)) sb.AppendLine($"  target: {YamlScalar(target)}");
        sb.AppendLine();
        sb.AppendLine("metadata:");
        sb.AppendLine($"  status: {YamlScalar(status)}");
        if (relations.Count == 0) sb.AppendLine("  relates: []");
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
                if (relation.RecommendationId is not null) sb.AppendLine($"      recommendation: {YamlScalar(relation.RecommendationId)}");
                if (relation.RecommendationContext is not null) sb.AppendLine($"      recommendation-context: {YamlScalar(relation.RecommendationContext)}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("tooling:");
        sb.AppendLine($"  version: {YamlScalar(CliVersion.Display)}");
        sb.AppendLine("  schema:");
        sb.AppendLine("    version: 0.1.0");
        if (templateName is not null)
        {
            sb.AppendLine("  template:");
            sb.AppendLine($"    name: {YamlScalar(templateName)}");
            sb.AppendLine($"    version: {YamlScalar(templateVersion ?? "0.1.0")}");
        }
        sb.AppendLine("---");
        return Normalize(sb.ToString()).TrimEnd();
    }

    public static string WithMetadata(string source, KnowledgeArtifactMetadata metadata, IReadOnlyList<ArtifactRelation> relations)
    {
        var normalized = Normalize(source);
        var body = string.Join("\n", normalized.Split('\n').Skip(metadata.ClosingLine + 1)).TrimStart('\n');
        return Render(metadata.Kind, metadata.Type, metadata.Scope, metadata.Target, metadata.Status,
            metadata.TemplateName, relations, metadata.TemplateVersion) + "\n\n" + body;
    }

    private static bool IsIdentifier(string? value) => !string.IsNullOrWhiteSpace(value) && char.IsLetter(value[0]) &&
        value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    private static bool IsSelection(string value) => value.Split('.').All(part =>
        part.Length > 0 && part[0] != '0' && part.All(char.IsAsciiDigit) && int.TryParse(part, out var number) && number > 0);

    private static string YamlScalar(string value)
    {
        if (value.Length > 0 && char.IsLetter(value[0]) && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.') &&
            value.ToLowerInvariant() is not ("true" or "false" or "null" or "yes" or "no" or "on" or "off")) return value;
        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            sb.Append(c switch
            {
                '\\' => "\\\\", '"' => "\\\"", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                _ when char.IsControl(c) => "\\u" + ((int)c).ToString("x4"),
                _ => c.ToString()
            });
        }
        return sb.Append('"').ToString();
    }
}
