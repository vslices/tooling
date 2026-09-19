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
    public static string? ValidateTemplate(
        MaterializationTemplateDefinition template)
    {
        if (!template.ArtifactKind.Equals("document", StringComparison.Ordinal))
        {
            return $"TMPL100: Configured template '{template.Id}' materializes artifact kind '{template.ArtifactKind}', not 'document'.";
        }

        if (!template.MediaType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase))
        {
            return $"TMPL101: Configured template '{template.Id}' uses media type '{template.MediaType}'. Document authoring currently supports 'text/markdown'.";
        }

        var presentation = template.QuestionPresentation;
        if (presentation is null)
        {
            return $"TMPL102: Configured template '{template.Id}' does not define representation.question.presentation.";
        }

        if (!presentation.Kind.Equals("heading", StringComparison.Ordinal))
        {
            return $"TMPL103: Configured template '{template.Id}' uses question presentation kind '{presentation.Kind}'. The current executable materialization slice supports 'heading'.";
        }

        if (!presentation.TextSource.Equals("question.text", StringComparison.Ordinal))
        {
            return $"TMPL104: Configured template '{template.Id}' uses question text source '{presentation.TextSource}'. The current executable materialization slice requires 'question.text'.";
        }

        if (!presentation.LevelStrategy.Equals("semantic-depth", StringComparison.Ordinal))
        {
            return $"TMPL105: Configured template '{template.Id}' uses question level strategy '{presentation.LevelStrategy}'. The current executable materialization slice requires 'semantic-depth'.";
        }

        if (presentation.RootLevel is < 1 or > 6)
        {
            return $"TMPL106: Configured template '{template.Id}' declares root heading level {presentation.RootLevel}; Markdown heading levels must be between 1 and 6.";
        }

        if (!string.Equals(template.EmptyAnswerState, "unanswered", StringComparison.Ordinal))
        {
            return $"TMPL108: Configured template '{template.Id}' must declare representation.question.answer.empty as 'unanswered' for progressive Document authoring.";
        }

        if (!string.Equals(
                template.RootQuestionIdentityStrategy,
                "document-type-root-question",
                StringComparison.Ordinal))
        {
            return $"TMPL109: Configured template '{template.Id}' must declare reconstruction.question-identity.root.strategy as 'document-type-root-question'.";
        }

        return null;
    }

    public static DocumentMaterializationResult RenderQuestion(
        string questionText,
        int semanticDepth,
        MaterializationTemplateDefinition template)
    {
        var validationError = ValidateTemplate(template);
        if (validationError is not null)
            return DocumentMaterializationResult.Failure(validationError);

        var presentation = template.QuestionPresentation!;
        var headingLevel = presentation.RootLevel + semanticDepth;
        if (headingLevel > 6)
        {
            return DocumentMaterializationResult.Failure(
                $"TMPL107: Configured template '{template.Id}' maps semantic depth {semanticDepth} to Markdown heading level {headingLevel}, beyond the supported maximum of 6.");
        }

        return DocumentMaterializationResult.Success(
            new string('#', headingLevel) + " " + questionText);
    }
}
