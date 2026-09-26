using System.Security.Cryptography;
using System.Text;

namespace VSlices.Tooling;

internal sealed record RelationalQuestionSurface(
    string SelectionPath, string Id, string Text, bool IsAnswered, string? Answer,
    string? ParentSelectionPath, IReadOnlyList<RelationalRecommendation> Recommendations,
    string? Connection = null);
internal sealed record RelationalArtifactState(
    KnowledgeArtifactMetadata Metadata, IReadOnlyList<RelationalQuestionSurface> Questions, string Source);
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

    public static string CreateNexus(NexusDefinition definition, string target, string? scope)
    {
        var sb = new StringBuilder();
        sb.AppendLine(KnowledgeArtifactFrontMatter.Render("nexus", definition.Type, scope, target, "draft", $"{definition.Type}.nexus", []));
        sb.AppendLine();
        sb.AppendLine($"# Nexus {definition.Type} de {target}");
        sb.AppendLine();
        if (definition.Questions.Count > 0)
        {
            sb.AppendLine("## Preguntas abiertas");
            sb.AppendLine();
            foreach (var question in definition.Questions) RenderQuestionTree(sb, question, 3);
        }
        RenderAssociatedArtifacts(sb, []);
        return KnowledgeArtifactFrontMatter.Normalize(sb.ToString());
    }

    public static string CreateContinuityPath(ContinuityPathDefinition definition, string target, string? scope)
    {
        var sb = new StringBuilder();
        sb.AppendLine(KnowledgeArtifactFrontMatter.Render("continuity-path", definition.Type, scope, target, "draft", $"{definition.Type}.continuity-path", []));
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
        sb.Append(RenderContinuityGraph(definition));
        sb.AppendLine("```");
        sb.AppendLine();
        RenderAssociatedArtifacts(sb, []);
        return KnowledgeArtifactFrontMatter.Normalize(sb.ToString());
    }

    public static RelationalArtifactStateResult ReadNexus(string source, NexusDefinition definition) =>
        Read(source, "nexus", definition.Type, definition.Questions, false);

    public static RelationalArtifactStateResult ReadContinuityPath(string source, ContinuityPathDefinition definition) =>
        Read(source, "continuity-path", definition.Type, [definition.RootQuestion], true);

    private static RelationalArtifactStateResult Read(
        string source, string kind, string type, IReadOnlyList<RelationalQuestionDefinition> roots, bool isPath)
    {
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!metadata.IsSuccess) return RelationalArtifactStateResult.Failure(metadata.Error!);
        if (metadata.Metadata!.Kind != kind || metadata.Metadata.Type != type)
            return RelationalArtifactStateResult.Failure("RELART010: Artifact metadata does not match the requested definition.");
        var regionError = ValidateAssociatedRegion(source);
        if (regionError is not null) return RelationalArtifactStateResult.Failure(regionError);
        if (isPath && !KnowledgeArtifactFrontMatter.Normalize(source).Contains("\n```mermaid\n", StringComparison.Ordinal))
            return RelationalArtifactStateResult.Failure("RELART026: Continuity Path graph is missing from the current materialization profile.");

        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots) CollectQuestionIds(root, expected);
        if (isPath)
        {
            expected.Add("__purpose");
            expected.Add("__recommended-traversal");
        }
        var answers = ReadQuestionAnswers(source, expected, out var error);
        if (error is not null) return RelationalArtifactStateResult.Failure(error);
        var surfaces = new List<RelationalQuestionSurface>();
        if (isPath)
        {
            surfaces.Add(new("1", "__purpose", "Propósito del recorrido", HasAnswer(answers, "__purpose"), Answer(answers, "__purpose"), null, []));
            surfaces.Add(new("2", "__recommended-traversal", "Recorrido recomendado", HasAnswer(answers, "__recommended-traversal"), Answer(answers, "__recommended-traversal"), null, []));
        }
        for (var index = 0; index < roots.Count; index++)
            AddProgressiveQuestion(roots[index], (index + (isPath ? 3 : 1)).ToString(), null, answers, surfaces);
        return RelationalArtifactStateResult.Success(new RelationalArtifactState(metadata.Metadata, surfaces, source));
    }

    public static string UpdateQuestion(RelationalArtifactState state, string selectionPath, string answer, out string? error)
    {
        error = null;
        var selected = state.Questions.FirstOrDefault(x => x.SelectionPath == selectionPath);
        if (selected is null)
        {
            error = $"RELART020: Selection '{selectionPath}' is not an available question.";
            return state.Source;
        }
        if (new[] { QuestionPrefix, QuestionEnd, AssociatedStart, AssociatedEnd }.Any(marker => answer.Contains(marker, StringComparison.Ordinal)))
        {
            error = "RELART024: Answer contains reserved reconstruction markers. No artifact was modified.";
            return state.Source;
        }
        var normalized = KnowledgeArtifactFrontMatter.Normalize(state.Source);
        var marker = QuestionPrefix + selected.Id + QuestionSuffix;
        var start = normalized.IndexOf(marker, StringComparison.Ordinal);
        var contentStart = start + marker.Length;
        var end = start < 0 ? -1 : normalized.IndexOf(QuestionEnd, contentStart, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            error = $"RELART021: Question region '{selected.Id}' is missing or malformed.";
            return state.Source;
        }
        var updated = normalized[..contentStart] + "\n" + answer.Trim() + "\n" + normalized[end..];
        return KnowledgeArtifactFrontMatter.WithMetadata(updated, state.Metadata, state.Metadata.Relations);
    }

    public static string AddRelation(string source, ArtifactRelation relation, out string? error)
    {
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        error = metadata.Error;
        if (!metadata.IsSuccess) return source;
        error = ValidateAssociatedRegion(source);
        if (error is not null) return source;
        var relations = metadata.Metadata!.Relations.ToList();
        var existing = relations.FindIndex(x => KnowledgeArtifactFrontMatter.PathComparer.Equals(x.Path, relation.Path));
        if (existing >= 0) relations[existing] = relation; else relations.Add(relation);
        var updated = KnowledgeArtifactFrontMatter.WithMetadata(source, metadata.Metadata, relations);
        var start = updated.IndexOf(AssociatedStart, StringComparison.Ordinal);
        var end = updated.IndexOf(AssociatedEnd, StringComparison.Ordinal);
        var table = new StringBuilder();
        RenderTableRegion(table, relations);
        return updated[..start] + KnowledgeArtifactFrontMatter.Normalize(table.ToString()).TrimEnd('\n') + updated[(end + AssociatedEnd.Length)..];
    }

    public static IReadOnlyList<(string Id, RelationalRecommendation Recommendation)> EnumerateRecommendations(
        NexusDefinition definition, RelationalArtifactState state)
    {
        var result = QuestionRecommendations(state);
        for (var index = 0; index < definition.Recommendations.Count; index++)
            result.Add(((definition.Questions.Count + index + 1).ToString(), definition.Recommendations[index]));
        return result;
    }

    public static IReadOnlyList<(string Id, RelationalRecommendation Recommendation)> EnumerateRecommendations(
        ContinuityPathDefinition definition, RelationalArtifactState state) => QuestionRecommendations(state);

    private static List<(string Id, RelationalRecommendation Recommendation)> QuestionRecommendations(RelationalArtifactState state)
    {
        var result = new List<(string Id, RelationalRecommendation Recommendation)>();
        foreach (var question in state.Questions)
            for (var index = 0; index < question.Recommendations.Count; index++)
                result.Add(($"{question.SelectionPath}.{index + 1}", question.Recommendations[index]));
        return result;
    }

    // A context fingerprint guards ephemeral selections; it is not recommendation identity.
    public static string RecommendationContext(NexusDefinition definition)
    {
        var context = new StringBuilder();
        Token(context, "nexus"); Token(context, definition.Type);
        foreach (var question in definition.Questions) AppendQuestionContext(context, question);
        AppendRecommendationContext(context, definition.Recommendations);
        return Fingerprint(context);
    }

    public static string RecommendationContext(ContinuityPathDefinition definition)
    {
        var context = new StringBuilder();
        Token(context, "continuity-path"); Token(context, definition.Type);
        AppendQuestionContext(context, definition.RootQuestion);
        return Fingerprint(context);
    }

    public static string? ValidateRecommendationContext(KnowledgeArtifactMetadata metadata, string context) =>
        metadata.Relations.Any(relation => relation.RecommendationId is not null && relation.RecommendationContext != context)
            ? "RELART030: A stored recommendation selection belongs to an unknown or changed definition context. Reconcile the association explicitly; Tooling will not retarget an ephemeral selection."
            : null;

    private static void AppendQuestionContext(StringBuilder sb, RelationalQuestionDefinition question)
    {
        Token(sb, "question"); Token(sb, question.Id);
        AppendRecommendationContext(sb, question.Recommendations);
        Token(sb, question.Children.Count.ToString());
        foreach (var child in question.Children) AppendQuestionContext(sb, child);
    }
    private static void AppendRecommendationContext(StringBuilder sb, IReadOnlyList<RelationalRecommendation> recommendations)
    {
        Token(sb, "recommendations"); Token(sb, recommendations.Count.ToString());
        foreach (var recommendation in recommendations)
        {
            Token(sb, recommendation.Family); Token(sb, recommendation.Type); Token(sb, recommendation.Role);
        }
    }
    private static void Token(StringBuilder sb, string value) => sb.Append(value.Length).Append(':').Append(value).Append(';');
    private static string Fingerprint(StringBuilder sb) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));

    private static void AddProgressiveQuestion(
        RelationalQuestionDefinition question, string path, string? parentPath,
        IReadOnlyDictionary<string, string> answers, List<RelationalQuestionSurface> result)
    {
        var answered = HasAnswer(answers, question.Id);
        result.Add(new(path, question.Id, question.Text, answered, Answer(answers, question.Id), parentPath, question.Recommendations, question.Connection));
        if (!answered) return;
        for (var index = 0; index < question.Children.Count; index++)
            AddProgressiveQuestion(question.Children[index], $"{path}.{question.Recommendations.Count + index + 1}", path, answers, result);
    }

    private static Dictionary<string, string> ReadQuestionAnswers(string source, HashSet<string> expected, out string? error)
    {
        error = null;
        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        string? current = null;
        var body = new StringBuilder();
        foreach (var line in KnowledgeArtifactFrontMatter.Normalize(source).Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(QuestionPrefix, StringComparison.Ordinal))
            {
                if (current is not null || !trimmed.EndsWith(QuestionSuffix, StringComparison.Ordinal))
                {
                    error = "RELART025: Nested or malformed question marker.";
                    return answers;
                }
                current = trimmed[QuestionPrefix.Length..^QuestionSuffix.Length];
                if (!expected.Contains(current) || answers.ContainsKey(current))
                {
                    error = $"RELART025: Unknown or duplicate question marker '{current}'.";
                    return answers;
                }
                body.Clear();
            }
            else if (trimmed == QuestionEnd)
            {
                if (current is null)
                {
                    error = "RELART025: Question closing marker has no opening marker.";
                    return answers;
                }
                answers.Add(current, body.ToString().Trim());
                current = null;
            }
            else if (current is not null) body.AppendLine(line);
        }
        if (current is not null || expected.Any(id => !answers.ContainsKey(id)))
            error = "RELART025: A required question region is missing or unclosed. No answer was inferred.";
        return answers;
    }

    private static void CollectQuestionIds(RelationalQuestionDefinition question, HashSet<string> ids)
    {
        ids.Add(question.Id);
        foreach (var child in question.Children) CollectQuestionIds(child, ids);
    }
    private static bool HasAnswer(IReadOnlyDictionary<string, string> answers, string id) => !string.IsNullOrWhiteSpace(Answer(answers, id));
    private static string? Answer(IReadOnlyDictionary<string, string> answers, string id) => answers.TryGetValue(id, out var answer) ? answer : null;

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

    public static string RenderContinuityGraph(ContinuityPathDefinition definition)
    {
        var sb = new StringBuilder("flowchart TD\n");
        BuildMermaid(definition.RootQuestion, sb);
        return KnowledgeArtifactFrontMatter.Normalize(sb.ToString());
    }
    private static void BuildMermaid(RelationalQuestionDefinition question, StringBuilder sb)
    {
        var id = MermaidId(question.Id);
        sb.AppendLine($"  {id}[\"{EscapeMermaid(question.Text)}\"]");
        for (var index = 0; index < question.Recommendations.Count; index++)
        {
            var recommendation = question.Recommendations[index];
            var recommendationId = $"{id}_r{index + 1}";
            sb.AppendLine($"  {recommendationId}[\"{EscapeMermaid(recommendation.Family + ": " + recommendation.Type)}\"]");
            sb.AppendLine($"  {id} -. \"{EscapeMermaid(recommendation.Role)}\" .-> {recommendationId}");
        }
        foreach (var child in question.Children)
        {
            var childId = MermaidId(child.Id);
            sb.AppendLine(string.IsNullOrWhiteSpace(child.Connection)
                ? $"  {id} --> {childId}"
                : $"  {id} -- \"{EscapeMermaid(child.Connection)}\" --> {childId}");
            BuildMermaid(child, sb);
        }
    }
    private static string MermaidId(string id) => "q_" + Convert.ToHexString(Encoding.UTF8.GetBytes(id)).ToLowerInvariant();
    private static string EscapeMermaid(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if (c is '\n' or '\r') sb.Append(' ');
            else if (c is '"' or '#' or '&' or '<' or '>' or '[' or ']' or '{' or '}' or '|' or '`' or '\\') sb.Append('#').Append((int)c).Append(';');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static string? ValidateAssociatedRegion(string source)
    {
        var start = source.IndexOf(AssociatedStart, StringComparison.Ordinal);
        var end = source.IndexOf(AssociatedEnd, StringComparison.Ordinal);
        return start < 0 || end < start ||
               source.IndexOf(AssociatedStart, start + AssociatedStart.Length, StringComparison.Ordinal) >= 0 ||
               source.IndexOf(AssociatedEnd, end + AssociatedEnd.Length, StringComparison.Ordinal) >= 0
            ? "RELART027: The associated-artifact table region is missing, duplicated or malformed."
            : null;
    }
    private static void RenderAssociatedArtifacts(StringBuilder sb, IReadOnlyList<ArtifactRelation> relations)
    {
        sb.AppendLine("## Artifacts asociados"); sb.AppendLine();
        RenderTableRegion(sb, relations); sb.AppendLine();
    }
    private static void RenderTableRegion(StringBuilder sb, IReadOnlyList<ArtifactRelation> relations)
    {
        sb.AppendLine(AssociatedStart);
        sb.AppendLine("| Artifact | Utilidad | Link |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var relation in relations)
        {
            var name = EscapeCell(Path.GetFileNameWithoutExtension(relation.Path));
            var href = string.Join("/", relation.Path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
            sb.AppendLine($"| {name} | {EscapeCell(relation.Role)} | [{EscapeCell(relation.Path)}]({href}) |");
        }
        sb.AppendLine(AssociatedEnd);
    }
    private static string EscapeCell(string value) => KnowledgeArtifactFrontMatter.Normalize(value)
        .Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal)
        .Replace("\n", "<br>", StringComparison.Ordinal);
}
