namespace VSlices.Tooling;

internal sealed record ReadQuestionItem(
    string SelectionPath,
    string Text,
    string? Answer,
    string? ParentSelectionPath);

internal sealed record QuestionReadResult(
    IReadOnlyList<ReadQuestionItem>? Questions,
    string? Error)
{
    public bool IsSuccess => Questions is not null && Error is null;

    public static QuestionReadResult Success(IReadOnlyList<ReadQuestionItem> questions) =>
        new(questions, null);

    public static QuestionReadResult Failure(string error) =>
        new(null, error);
}

/// <summary>
/// Reuses admitted artifact state instead of interpreting Markdown headings or
/// manufacturing identities from question numbering.
/// </summary>
internal static class QuestionReadOperation
{
    public static async Task<QuestionReadResult> Document(
        string artifact, string questionId, bool deep, CancellationToken cancellationToken)
    {
        var resolved = DocumentPathResolver.Resolve(artifact, Environment.CurrentDirectory);
        if (!resolved.IsSuccess)
            return QuestionReadResult.Failure(resolved.Error!);
        var path = resolved.Path!;
        if (!File.Exists(path))
            return QuestionReadResult.Failure($"READ001: Document '{path}' does not exist.");

        var standards = KnowledgeArtifactCommandSupport.LoadStandards(path);
        if (!standards.IsSuccess)
            return QuestionReadResult.Failure(standards.Error!);

        var template = DocumentMaterializationEnvironment.Resolve(path);
        if (!template.IsSuccess)
            return QuestionReadResult.Failure(template.Error!);

        var source = await File.ReadAllTextAsync(path, cancellationToken);
        var parsed = DocumentArtifact.Read(source, standards.Standards!.Documents, template.Template!);
        if (!parsed.IsSuccess)
            return QuestionReadResult.Failure(parsed.Error!);

        var document = parsed.Artifact!;
        return Select(document.Surface.Select(question => new ReadQuestionItem(
            question.SelectionPath,
            question.Text,
            document.AnswerFor(question),
            question.ParentSelectionPath)).ToArray(), questionId, deep);
    }

    public static QuestionReadResult Relational(
        string artifact, string kind, string questionId, bool deep)
    {
        var existing = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(
            artifact, Environment.CurrentDirectory);
        if (!existing.IsSuccess)
            return QuestionReadResult.Failure(existing.Error!);
        var resolved = existing.Resolution!;
        if (!resolved.Metadata.Kind.Equals(kind, StringComparison.Ordinal))
            return QuestionReadResult.Failure(
                $"READ002: Artifact '{resolved.Path}' is '{resolved.Metadata.Kind}', not '{kind}'.");

        var installed = KnowledgeArtifactCommandSupport.LoadStandards(resolved.Path);
        if (!installed.IsSuccess)
            return QuestionReadResult.Failure(installed.Error!);
        var catalog = installed.Standards!.Relational;

        RelationalArtifactStateResult parsed;
        if (kind == "nexus")
        {
            if (!catalog.TryGetNexus(resolved.Metadata.Type, out var definition) || definition is null)
                return QuestionReadResult.Failure(
                    $"READ003: Nexus type '{resolved.Metadata.Type}' is not admitted by the installed Docs Standard.");
            parsed = RelationalArtifact.ReadNexus(resolved.Source, definition);
        }
        else if (kind == "continuity-path")
        {
            if (!catalog.TryGetContinuityPath(resolved.Metadata.Type, out var definition) || definition is null)
                return QuestionReadResult.Failure(
                    $"READ004: Continuity Path type '{resolved.Metadata.Type}' is not admitted by the installed Docs Standard.");
            parsed = RelationalArtifact.ReadContinuityPath(resolved.Source, definition);
        }
        else return QuestionReadResult.Failure($"READ005: Unsupported relational kind '{kind}'.");

        if (!parsed.IsSuccess)
            return QuestionReadResult.Failure(parsed.Error!);

        return Select(parsed.State!.Questions.Select(question => new ReadQuestionItem(
            question.SelectionPath,
            question.Text,
            question.Answer,
            question.ParentSelectionPath)).ToArray(), questionId, deep);
    }

    public static QuestionReadResult SupportNote(
        string artifact, string questionId, bool deep)
    {
        var existing = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(
            artifact, Environment.CurrentDirectory);
        if (!existing.IsSuccess)
            return QuestionReadResult.Failure(existing.Error!);
        var resolved = existing.Resolution!;
        if (!resolved.Metadata.Kind.Equals("support-note", StringComparison.Ordinal))
            return QuestionReadResult.Failure(
                $"READ002: Artifact '{resolved.Path}' is '{resolved.Metadata.Kind}', not 'support-note'.");

        var installed = KnowledgeArtifactCommandSupport.LoadStandards(resolved.Path);
        if (!installed.IsSuccess)
            return QuestionReadResult.Failure(installed.Error!);
        if (!installed.Standards!.SupportNotes.TryGetSupportNote(
            resolved.Metadata.Type, out var definition) || definition is null)
        {
            return QuestionReadResult.Failure(
                $"READ006: Support Note type '{resolved.Metadata.Type}' is not admitted by the installed Docs Standard.");
        }
        var parsed = SupportNoteArtifact.Read(resolved.Source, definition);
        if (!parsed.IsSuccess)
            return QuestionReadResult.Failure(parsed.Error!);
        return Select(parsed.State!.Questions.Select(question => new ReadQuestionItem(
            question.SelectionPath,
            question.Text,
            question.Answer,
            question.ParentSelectionPath)).ToArray(), questionId, deep);
    }

    private static QuestionReadResult Select(
        IReadOnlyList<ReadQuestionItem> questions, string questionId, bool deep)
    {
        if (string.IsNullOrWhiteSpace(questionId))
            return QuestionReadResult.Failure("READ007: Specify --question-id <selection> from the current discovery surface.");

        // The public --question-id identifies a current selection path, as on update.
        // Source-defined question ids are not numeric paths and cannot be inferred from them.
        var selected = questions.FirstOrDefault(
            question => question.SelectionPath.Equals(questionId, StringComparison.Ordinal));
        if (selected is null)
            return QuestionReadResult.Failure(
                $"READ008: Question selection '{questionId}' is not available on this artifact. Run 'vslices discovery <family> <artifact>' to inspect current selections.");

        if (!deep)
            return QuestionReadResult.Success([selected]);

        var bySelection = questions.ToDictionary(question => question.SelectionPath, StringComparer.Ordinal);
        var chain = new List<ReadQuestionItem>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        ReadQuestionItem? current = selected;
        while (current is not null)
        {
            if (!visited.Add(current.SelectionPath))
                return QuestionReadResult.Failure(
                    $"READ009: Cyclic question ancestry found at '{current.SelectionPath}'.");
            chain.Add(current);
            if (current.ParentSelectionPath is null)
                break;
            if (!bySelection.TryGetValue(current.ParentSelectionPath, out var parent))
                return QuestionReadResult.Failure(
                    $"READ010: Parent selection '{chain[^1].ParentSelectionPath}' is not present in this artifact.");
            current = parent;
        }

        chain.Reverse();
        return QuestionReadResult.Success(chain);
    }
}
