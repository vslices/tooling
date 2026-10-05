using System.Text;

namespace VSlices.Tooling;

internal sealed record SupportNoteQuestionSurface(
    string SelectionPath,
    string Id,
    string Text,
    bool IsAnswered,
    string? Answer,
    string? ParentSelectionPath);

internal sealed record SupportNoteArtifactState(
    KnowledgeArtifactMetadata Metadata,
    IReadOnlyList<SupportNoteQuestionSurface> Questions,
    string Source);

internal sealed record SupportNoteArtifactStateResult(
    SupportNoteArtifactState? State,
    string? Error)
{
    public bool IsSuccess => State is not null && Error is null;
    public static SupportNoteArtifactStateResult Success(SupportNoteArtifactState state) => new(state, null);
    public static SupportNoteArtifactStateResult Failure(string error) => new(null, error);
}

internal static class SupportNoteArtifact
{
    private const string QuestionPrefix = "<!-- vslices:support-note-question id=";
    private const string QuestionSuffix = " -->";
    private const string QuestionEnd = "<!-- /vslices:support-note-question -->";

    public static string Create(
        SupportNoteDefinition definition,
        string target,
        string? scope,
        IReadOnlyList<ArtifactRelation> relations)
    {
        var sb = new StringBuilder();
        sb.AppendLine(KnowledgeArtifactFrontMatter.Render(
            "support-note",
            definition.Type,
            scope,
            target,
            "draft",
            "support-note.question-tree",
            relations));
        sb.AppendLine();
        sb.AppendLine($"# Nota de soporte — {definition.Type} — {target}");
        sb.AppendLine();
        RenderQuestionTree(sb, definition.RootQuestion, 2);
        return KnowledgeArtifactFrontMatter.Normalize(sb.ToString());
    }

    public static SupportNoteArtifactStateResult Read(
        string source,
        SupportNoteDefinition definition)
    {
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!metadata.IsSuccess)
            return SupportNoteArtifactStateResult.Failure(metadata.Error!);

        if (!metadata.Metadata!.Kind.Equals("support-note", StringComparison.Ordinal) ||
            !metadata.Metadata.Type.Equals(definition.Type, StringComparison.Ordinal))
        {
            return SupportNoteArtifactStateResult.Failure(
                "SUPART001: Artifact metadata does not match the requested Support Note definition.");
        }

        var expected = new HashSet<string>(StringComparer.Ordinal);
        CollectQuestionIds(definition.RootQuestion, expected);
        var answers = ReadQuestionAnswers(source, expected, out var error);
        if (error is not null)
            return SupportNoteArtifactStateResult.Failure(error);

        var surfaces = new List<SupportNoteQuestionSurface>();
        AddQuestionSurface(definition.RootQuestion, "1", null, answers, surfaces);

        return SupportNoteArtifactStateResult.Success(
            new SupportNoteArtifactState(metadata.Metadata, surfaces, source));
    }

    public static string UpdateQuestion(
        SupportNoteArtifactState state,
        string selectionPath,
        string answer,
        out string? error)
    {
        error = null;
        var selected = state.Questions.FirstOrDefault(question => question.SelectionPath == selectionPath);
        if (selected is null)
        {
            error = $"SUPART002: Selection '{selectionPath}' is not an available Support Note question.";
            return state.Source;
        }

        if (answer.Contains(QuestionPrefix, StringComparison.Ordinal) ||
            answer.Contains(QuestionEnd, StringComparison.Ordinal))
        {
            error = "SUPART003: Answer contains reserved Support Note reconstruction markers. No artifact was modified.";
            return state.Source;
        }

        var normalized = KnowledgeArtifactFrontMatter.Normalize(state.Source);
        var marker = QuestionPrefix + selected.Id + QuestionSuffix;
        var start = normalized.IndexOf(marker, StringComparison.Ordinal);
        var contentStart = start + marker.Length;
        var end = start < 0 ? -1 : normalized.IndexOf(QuestionEnd, contentStart, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            error = $"SUPART004: Question region '{selected.Id}' is missing or malformed.";
            return state.Source;
        }

        var updated = normalized[..contentStart] + "\n" + answer.Trim() + "\n" + normalized[end..];
        return KnowledgeArtifactFrontMatter.WithMetadata(updated, state.Metadata, state.Metadata.Relations);
    }

    private static void AddQuestionSurface(
        SupportNoteQuestionDefinition question,
        string selectionPath,
        string? parentSelectionPath,
        IReadOnlyDictionary<string, string> answers,
        List<SupportNoteQuestionSurface> surfaces)
    {
        var answer = answers.TryGetValue(question.Id, out var value) ? value : null;
        var answered = !string.IsNullOrWhiteSpace(answer);
        surfaces.Add(new(
            selectionPath,
            question.Id,
            question.Text,
            answered,
            answer,
            parentSelectionPath));

        for (var index = 0; index < question.Children.Count; index++)
        {
            AddQuestionSurface(
                question.Children[index],
                $"{selectionPath}.{index + 1}",
                selectionPath,
                answers,
                surfaces);
        }
    }

    private static Dictionary<string, string> ReadQuestionAnswers(
        string source,
        HashSet<string> expected,
        out string? error)
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
                    error = "SUPART005: Nested or malformed Support Note question marker.";
                    return answers;
                }

                current = trimmed[QuestionPrefix.Length..^QuestionSuffix.Length];
                if (!expected.Contains(current) || answers.ContainsKey(current))
                {
                    error = $"SUPART005: Unknown or duplicate Support Note question marker '{current}'.";
                    return answers;
                }

                body.Clear();
            }
            else if (trimmed == QuestionEnd)
            {
                if (current is null)
                {
                    error = "SUPART005: Support Note question closing marker has no opening marker.";
                    return answers;
                }

                answers.Add(current, body.ToString().Trim());
                current = null;
            }
            else if (current is not null)
            {
                body.AppendLine(line);
            }
        }

        if (current is not null || expected.Any(id => !answers.ContainsKey(id)))
            error = "SUPART005: A required Support Note question region is missing or unclosed.";

        return answers;
    }

    private static void CollectQuestionIds(
        SupportNoteQuestionDefinition question,
        HashSet<string> ids)
    {
        ids.Add(question.Id);
        foreach (var child in question.Children)
            CollectQuestionIds(child, ids);
    }

    private static void RenderQuestionTree(
        StringBuilder sb,
        SupportNoteQuestionDefinition question,
        int headingLevel)
    {
        sb.AppendLine(new string('#', Math.Min(headingLevel, 6)) + " " + question.Text);
        sb.AppendLine(QuestionPrefix + question.Id + QuestionSuffix);
        sb.AppendLine(QuestionEnd);
        sb.AppendLine();

        foreach (var child in question.Children)
            RenderQuestionTree(sb, child, headingLevel + 1);
    }
}
