namespace VSlices.Tooling;

internal sealed record DocumentMaterializationResult(
    string? Source,
    string? Error)
{
    public bool IsSuccess => Source is not null && Error is null;

    public static DocumentMaterializationResult Success(string source) =>
        new(source, null);

    public static DocumentMaterializationResult Failure(string error) =>
        new(null, error);
}

internal static class DocumentMaterialization
{
    public static DocumentMaterializationResult RenderRootQuestion(
        DocumentQuestionDefinition question,
        MaterializationTemplateDefinition template)
    {
        if (!template.ArtifactKind.Equals("document", StringComparison.Ordinal))
        {
            return DocumentMaterializationResult.Failure(
                $"TMPL100: Configured template '{template.Id}' materializes artifact kind '{template.ArtifactKind}', not 'document'.");
        }

        if (!template.MediaType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentMaterializationResult.Failure(
                $"TMPL101: Configured template '{template.Id}' uses media type '{template.MediaType}'. Document authoring currently supports 'text/markdown'.");
        }

        var presentation = template.QuestionPresentation;
        if (presentation is null)
        {
            return DocumentMaterializationResult.Failure(
                $"TMPL102: Configured template '{template.Id}' does not define representation.question.presentation.");
        }

        if (!presentation.Kind.Equals("heading", StringComparison.Ordinal))
        {
            return DocumentMaterializationResult.Failure(
                $"TMPL103: Configured template '{template.Id}' uses question presentation kind '{presentation.Kind}'. The current executable materialization slice supports 'heading'.");
        }

        if (!presentation.TextSource.Equals("question.text", StringComparison.Ordinal))
        {
            return DocumentMaterializationResult.Failure(
                $"TMPL104: Configured template '{template.Id}' uses question text source '{presentation.TextSource}'. The current executable materialization slice requires 'question.text'.");
        }

        if (!presentation.LevelStrategy.Equals("semantic-depth", StringComparison.Ordinal))
        {
            return DocumentMaterializationResult.Failure(
                $"TMPL105: Configured template '{template.Id}' uses question level strategy '{presentation.LevelStrategy}'. The current executable materialization slice requires 'semantic-depth'.");
        }

        if (presentation.RootLevel is < 1 or > 6)
        {
            return DocumentMaterializationResult.Failure(
                $"TMPL106: Configured template '{template.Id}' declares root heading level {presentation.RootLevel}; Markdown heading levels must be between 1 and 6.");
        }

        return DocumentMaterializationResult.Success(
            new string('#', presentation.RootLevel) + " " + question.Text);
    }
}
