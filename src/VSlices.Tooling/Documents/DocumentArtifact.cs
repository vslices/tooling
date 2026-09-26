namespace VSlices.Tooling;

internal sealed record DocumentQuestionAffordance(
    int Selection,
    string QuestionId,
    string Text,
    int Depth,
    DocumentQuestionCardinality Cardinality,
    bool IsMaterialized,
    bool IsAnswered,
    string? AnswerInstanceId,
    string? AnswerPreview);

internal sealed record DocumentArtifactReadResult(
    DocumentArtifact? Artifact,
    string? Error)
{
    public bool IsSuccess => Artifact is not null && Error is null;

    public static DocumentArtifactReadResult Success(DocumentArtifact artifact) =>
        new(artifact, null);

    public static DocumentArtifactReadResult Failure(string error) =>
        new(null, error);
}

internal sealed record DocumentArtifactMutationResult(
    string? Source,
    DocumentQuestionAffordance? Question,
    string? Error)
{
    public bool IsSuccess => Source is not null && Question is not null && Error is null;

    public static DocumentArtifactMutationResult Success(
        string source,
        DocumentQuestionAffordance question) =>
        new(source, question, null);

    public static DocumentArtifactMutationResult Failure(string error) =>
        new(null, null, error);
}

internal sealed class DocumentArtifact
{
    private const string PlaceholderPrefix = "<!-- vslices:placeholder question=";
    private const string LegacyPlaceholderPrefix = "<!-- vslices:placeholder document=";
    private const string PlaceholderStem = "<!-- vslices:placeholder";
    private const string QuestionPrefix = "<!-- vslices:question question=";
    private const string LegacyQuestionPrefix = "<!-- vslices:question document=";
    private const string QuestionStem = "<!-- vslices:question";
    private const string MarkerSuffix = " -->";
    private const string QuestionSeparator = " question=";
    private const string QuestionEndMarker = "<!-- /vslices:question -->";
    private const string AnswerInstancePrefix = "<!-- vslices:answer-instance question=";
    private const string AnswerInstanceStem = "<!-- vslices:answer-instance";
    private const string AnswerInstanceIdSeparator = " id=";
    private const string AnswerInstanceEndMarker = "<!-- /vslices:answer-instance -->";

    private readonly string normalizedSource;
    private readonly string newline;
    private readonly DocumentDefinition definition;
    private readonly IReadOnlyDictionary<string, QuestionBlock> blocks;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AnswerInstanceBlock>> answerInstances;
    private readonly UnansweredRoot? unansweredRoot;
    private readonly bool usesFrontMatterIdentity;

    private DocumentArtifact(
        string normalizedSource,
        string newline,
        DocumentDefinition definition,
        IReadOnlyDictionary<string, QuestionBlock> blocks,
        IReadOnlyDictionary<string, IReadOnlyList<AnswerInstanceBlock>> answerInstances,
        UnansweredRoot? unansweredRoot,
        bool usesFrontMatterIdentity,
        IReadOnlyList<DocumentQuestionAffordance> surface)
    {
        this.normalizedSource = normalizedSource;
        this.newline = newline;
        this.definition = definition;
        this.blocks = blocks;
        this.answerInstances = answerInstances;
        this.unansweredRoot = unansweredRoot;
        this.usesFrontMatterIdentity = usesFrontMatterIdentity;
        Surface = surface;
    }

    public string DocumentType => definition.Type;
    public IReadOnlyList<DocumentQuestionAffordance> Surface { get; }

    public static DocumentArtifactReadResult Read(
        string source,
        DocsStandardCatalog catalog,
        MaterializationTemplateDefinition materializationTemplate)
    {
        var newline = source.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n";
        var normalized = NormalizeNewlines(source);
        var lines = normalized.Split('\n').ToList();

        var frontMatter = DocumentFrontMatter.Read(normalized);
        if (!frontMatter.IsSuccess)
            return DocumentArtifactReadResult.Failure(frontMatter.Error!);

        UnansweredRoot? unansweredRoot = null;
        var blocks = new Dictionary<string, QuestionBlock>(StringComparer.Ordinal);
        var answerInstances = new Dictionary<string, List<AnswerInstanceBlock>>(StringComparer.Ordinal);
        var answerInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        string? documentType = frontMatter.DocumentType;

        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].Trim();

            if (TryParseQuestionIdentityMarker(trimmed, PlaceholderPrefix, out var placeholderQuestion))
            {
                if (unansweredRoot is not null)
                {
                    return DocumentArtifactReadResult.Failure(
                        "DOCART001: Document contains more than one VSlices root placeholder.");
                }

                unansweredRoot = new UnansweredRoot(placeholderQuestion, null, index);
                continue;
            }

            if (TryParseLegacyMarker(
                    trimmed,
                    LegacyPlaceholderPrefix,
                    out var placeholderDocument,
                    out placeholderQuestion))
            {
                if (unansweredRoot is not null)
                {
                    return DocumentArtifactReadResult.Failure(
                        "DOCART001: Document contains more than one VSlices root placeholder.");
                }

                if (!TryAcceptDocumentType(ref documentType, placeholderDocument))
                {
                    return DocumentArtifactReadResult.Failure(
                        "DOCART002: Document metadata refers to more than one document type.");
                }

                unansweredRoot = new UnansweredRoot(placeholderQuestion, null, index);
                continue;
            }

            if (trimmed.StartsWith(PlaceholderStem, StringComparison.Ordinal))
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART003: Malformed VSlices placeholder metadata at line {index + 1}.");
            }

            if (TryParseAnswerInstanceMarker(
                    trimmed,
                    out var answerInstanceQuestion,
                    out var answerInstanceId))
            {
                if (!answerInstanceIds.Add(answerInstanceId))
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART028: Duplicate AnswerInstance id '{answerInstanceId}'.");
                }

                var instance = ReadAnswerInstanceBlock(
                    lines,
                    index,
                    answerInstanceQuestion,
                    answerInstanceId);
                if (!instance.IsSuccess)
                    return DocumentArtifactReadResult.Failure(instance.Error!);

                if (!answerInstances.TryGetValue(answerInstanceQuestion, out var instances))
                {
                    instances = new List<AnswerInstanceBlock>();
                    answerInstances.Add(answerInstanceQuestion, instances);
                }

                instances.Add(instance.Block!);
                index = instance.Block!.EndLine;
                continue;
            }

            if (trimmed.StartsWith(AnswerInstanceStem, StringComparison.Ordinal))
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART029: Malformed VSlices AnswerInstance metadata at line {index + 1}.");
            }

            if (trimmed.Equals(AnswerInstanceEndMarker, StringComparison.Ordinal))
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART030: Orphan VSlices AnswerInstance closing marker at line {index + 1}.");
            }

            string blockQuestion;
            if (TryParseQuestionIdentityMarker(trimmed, QuestionPrefix, out blockQuestion))
            {
                var block = ReadQuestionBlock(lines, index, blockQuestion, blocks);
                if (!block.IsSuccess)
                    return DocumentArtifactReadResult.Failure(block.Error!);

                blocks.Add(blockQuestion, block.Block!);
                index = block.Block!.EndLine;
                continue;
            }

            if (TryParseLegacyMarker(
                    trimmed,
                    LegacyQuestionPrefix,
                    out var blockDocument,
                    out blockQuestion))
            {
                if (!TryAcceptDocumentType(ref documentType, blockDocument))
                {
                    return DocumentArtifactReadResult.Failure(
                        "DOCART002: Document metadata refers to more than one document type.");
                }

                var block = ReadQuestionBlock(lines, index, blockQuestion, blocks);
                if (!block.IsSuccess)
                    return DocumentArtifactReadResult.Failure(block.Error!);

                blocks.Add(blockQuestion, block.Block!);
                index = block.Block!.EndLine;
                continue;
            }

            if (trimmed.StartsWith(QuestionStem, StringComparison.Ordinal))
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART008: Malformed VSlices question metadata at line {index + 1}.");
            }

            if (trimmed.Equals(QuestionEndMarker, StringComparison.Ordinal))
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART009: Orphan VSlices question closing marker at line {index + 1}.");
            }
        }

        if (documentType is null)
        {
            return DocumentArtifactReadResult.Failure(
                "DOCART010: Markdown artifact does not contain VSlices Document identity metadata.");
        }

        if (!catalog.TryGetDocument(documentType, out var definition) || definition is null)
        {
            return DocumentArtifactReadResult.Failure(
                $"DOCART011: Document type '{documentType}' is not defined by the installed Docs Standard.");
        }

        var questionIndex = BuildQuestionIndex(definition.RootQuestion);

        if (unansweredRoot is not null)
        {
            if (!unansweredRoot.QuestionId.Equals(definition.RootQuestion.Id, StringComparison.Ordinal))
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART012: Root placeholder question '{unansweredRoot.QuestionId}' does not match Docs Standard root '{definition.RootQuestion.Id}'.");
            }

            if (blocks.Count > 0 || answerInstances.Count > 0)
            {
                return DocumentArtifactReadResult.Failure(
                    "DOCART013: A Document with an unanswered root cannot already contain answered question blocks.");
            }
        }
        else if (!blocks.ContainsKey(definition.RootQuestion.Id))
        {
            if (!frontMatter.IsPresent || frontMatter.ClosingLine is null)
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART014: Answered Document must materialize root question '{definition.RootQuestion.Id}'.");
            }

            var inferredRoot = ReadMarkerlessRoot(
                lines,
                frontMatter.ClosingLine.Value,
                definition.RootQuestion,
                materializationTemplate,
                blocks,
                answerInstances,
                questionIndex);
            if (!inferredRoot.IsSuccess)
                return DocumentArtifactReadResult.Failure(inferredRoot.Error!);

            if (inferredRoot.AnsweredBlock is not null)
                blocks.Add(definition.RootQuestion.Id, inferredRoot.AnsweredBlock);
            else
                unansweredRoot = inferredRoot.UnansweredRoot;

            if (unansweredRoot is not null && (blocks.Count > 0 || answerInstances.Count > 0))
            {
                return DocumentArtifactReadResult.Failure(
                    "DOCART013: A Document with an unanswered root cannot already contain answered question blocks.");
            }
        }

        if (unansweredRoot is null)
        {
            foreach (var questionId in blocks.Keys)
            {
                if (!questionIndex.TryGetValue(questionId, out var info))
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART015: Materialized question '{questionId}' is not defined by the installed Docs Standard.");
                }

                if (info.Definition.Cardinality == DocumentQuestionCardinality.Many)
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART031: Question '{questionId}' has cardinality 'many' and must be materialized as AnswerInstances.");
                }

                if (info.ParentId is not null)
                {
                    var parent = questionIndex[info.ParentId];
                    if (parent.Definition.Cardinality == DocumentQuestionCardinality.Many)
                    {
                        return DocumentArtifactReadResult.Failure(
                            $"DOCART032: Child question '{questionId}' is scoped through repeated parent '{info.ParentId}'. Child scoping through AnswerInstances is not supported by the current preview.");
                    }

                    if (!blocks.ContainsKey(info.ParentId))
                    {
                        return DocumentArtifactReadResult.Failure(
                            $"DOCART016: Materialized question '{questionId}' requires answered parent '{info.ParentId}'.");
                    }
                }
            }

            foreach (var pair in answerInstances)
            {
                if (!questionIndex.TryGetValue(pair.Key, out var info))
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART033: AnswerInstances refer to question '{pair.Key}', which is not defined by the installed Docs Standard.");
                }

                if (info.Definition.Cardinality != DocumentQuestionCardinality.Many)
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART034: Question '{pair.Key}' has cardinality 'one' and cannot contain repeated AnswerInstances.");
                }

                if (info.ParentId is null)
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART035: Repeated root question '{pair.Key}' is not supported by the current preview.");
                }

                var parent = questionIndex[info.ParentId];
                if (parent.Definition.Cardinality == DocumentQuestionCardinality.Many)
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART032: Question '{pair.Key}' is scoped through repeated parent '{info.ParentId}'. Child scoping through AnswerInstances is not supported by the current preview.");
                }

                if (!blocks.ContainsKey(info.ParentId))
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART016: Materialized question '{pair.Key}' requires answered parent '{info.ParentId}'.");
                }
            }
        }

        var readonlyAnswerInstances = answerInstances.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<AnswerInstanceBlock>)pair.Value
                .OrderBy(instance => instance.StartLine)
                .ToArray(),
            StringComparer.Ordinal);

        var surface = BuildSurface(definition, blocks, readonlyAnswerInstances, unansweredRoot);
        return DocumentArtifactReadResult.Success(
            new DocumentArtifact(
                normalized,
                newline,
                definition,
                blocks,
                readonlyAnswerInstances,
                unansweredRoot,
                frontMatter.IsPresent,
                surface));
    }

    public DocumentArtifactMutationResult Update(
        int selection,
        string answer,
        MaterializationTemplateDefinition materializationTemplate)
    {
        if (selection < 1 || selection > Surface.Count)
        {
            return DocumentArtifactMutationResult.Failure(
                $"UPDATE105: Question selection {selection} is not available. Current surface contains selections 1..{Surface.Count}.");
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            return DocumentArtifactMutationResult.Failure(
                "UPDATE106: --answer must contain non-whitespace text.");
        }

        if (ContainsReservedMetadata(answer))
        {
            return DocumentArtifactMutationResult.Failure(
                "UPDATE107: Answer contains reserved VSlices document metadata markers.");
        }

        var selected = Surface[selection - 1];
        var lines = normalizedSource.Split('\n').ToList();
        var answerLines = NormalizeNewlines(answer.Trim()).Split('\n').ToArray();

        if (selected.Cardinality == DocumentQuestionCardinality.Many)
        {
            if (selected.AnswerInstanceId is not null)
            {
                return DocumentArtifactMutationResult.Failure(
                    $"UPDATE110: AnswerInstance '{selected.AnswerInstanceId}' already exists for question '{selected.QuestionId}'. Editing repeated AnswerInstances is not supported by the current preview yet.");
            }

            if (!string.Equals(
                    materializationTemplate.MultipleAnswerStrategy,
                    "marked-instances",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    materializationTemplate.AnswerInstanceStrategy,
                    "explicit-marker",
                    StringComparison.Ordinal))
            {
                return DocumentArtifactMutationResult.Failure(
                    $"UPDATE111: Configured template '{materializationTemplate.Id}' does not define the repeated AnswerInstance materialization contract required for cardinality 'many'.");
            }

            var renderedQuestion = DocumentMaterialization.RenderQuestion(
                selected.Text,
                selected.Depth,
                materializationTemplate);
            if (!renderedQuestion.IsSuccess)
                return DocumentArtifactMutationResult.Failure(renderedQuestion.Error!);

            var answerInstanceId = "answer-" + Guid.NewGuid().ToString("N");
            var renderedInstance = RenderAnswerInstanceBlock(
                selected.QuestionId,
                answerInstanceId,
                answerLines);

            if (!answerInstances.TryGetValue(selected.QuestionId, out var existing) ||
                existing.Count == 0)
            {
                AppendManyQuestion(
                    lines,
                    renderedQuestion.Source!,
                    renderedInstance);
            }
            else
            {
                InsertAnswerInstanceAfter(
                    lines,
                    existing[^1].EndLine,
                    renderedInstance);
            }

            var repeatedCandidate = string.Join("\n", lines);
            if (!newline.Equals("\n", StringComparison.Ordinal))
                repeatedCandidate = repeatedCandidate.Replace("\n", newline, StringComparison.Ordinal);

            return DocumentArtifactMutationResult.Success(repeatedCandidate, selected);
        }

        if (unansweredRoot is not null)
        {
            if (!selected.QuestionId.Equals(unansweredRoot.QuestionId, StringComparison.Ordinal))
            {
                return DocumentArtifactMutationResult.Failure(
                    "UPDATE108: The unanswered root is the only writable question in the current Document state.");
            }

            if (unansweredRoot.PlaceholderLine is int placeholderLine)
            {
                lines.RemoveAt(placeholderLine);
                lines.InsertRange(
                    placeholderLine,
                    RenderAnswerBlock(
                        definition.Type,
                        selected.QuestionId,
                        answerLines,
                        usesFrontMatterIdentity));
            }
            else
            {
                var headingLine = unansweredRoot.HeadingLine!.Value;
                if (lines.Count > headingLine + 1)
                    lines.RemoveRange(headingLine + 1, lines.Count - headingLine - 1);

                lines.Add(string.Empty);
                lines.AddRange(answerLines);
                lines.Add(string.Empty);
            }
        }
        else if (blocks.TryGetValue(selected.QuestionId, out var block))
        {
            if (block.IsMarkerless)
            {
                var answerLineCount = block.EndLine - block.StartLine - 1;
                if (answerLineCount > 0)
                    lines.RemoveRange(block.StartLine + 1, answerLineCount);

                var replacement = new List<string> { string.Empty };
                replacement.AddRange(answerLines);
                replacement.Add(string.Empty);
                lines.InsertRange(block.StartLine + 1, replacement);
            }
            else
            {
                var answerLineCount = block.EndLine - block.StartLine - 1;
                if (answerLineCount > 0)
                    lines.RemoveRange(block.StartLine + 1, answerLineCount);
                lines.InsertRange(block.StartLine + 1, answerLines);
            }
        }
        else
        {
            var renderedQuestion = DocumentMaterialization.RenderQuestion(
                selected.Text,
                selected.Depth,
                materializationTemplate);
            if (!renderedQuestion.IsSuccess)
                return DocumentArtifactMutationResult.Failure(renderedQuestion.Error!);

            AppendQuestion(
                lines,
                selected,
                renderedQuestion.Source!,
                answerLines);
        }

        var candidate = string.Join("\n", lines);
        if (!newline.Equals("\n", StringComparison.Ordinal))
            candidate = candidate.Replace("\n", newline, StringComparison.Ordinal);

        return DocumentArtifactMutationResult.Success(candidate, selected);
    }

    private static MarkerlessRootReadResult ReadMarkerlessRoot(
        IReadOnlyList<string> lines,
        int frontMatterClosingLine,
        DocumentQuestionDefinition rootQuestion,
        MaterializationTemplateDefinition materializationTemplate,
        IReadOnlyDictionary<string, QuestionBlock> blocks,
        IReadOnlyDictionary<string, List<AnswerInstanceBlock>> answerInstances,
        IReadOnlyDictionary<string, QuestionInfo> questionIndex)
    {
        var validationError = DocumentMaterialization.ValidateTemplate(materializationTemplate);
        if (validationError is not null)
            return MarkerlessRootReadResult.Failure(validationError);

        var rootLine = lines
            .Select((line, index) => new { Line = line, Index = index })
            .Skip(frontMatterClosingLine + 1)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Line));
        if (rootLine is null)
        {
            return MarkerlessRootReadResult.Failure(
                "DOCART025: Markerless Document must materialize its root question after front matter.");
        }

        if (!TryReadHeadingLevel(rootLine.Line, out var headingLevel) ||
            headingLevel != materializationTemplate.QuestionPresentation!.RootLevel)
        {
            return MarkerlessRootReadResult.Failure(
                $"DOCART026: Markerless root must use heading level {materializationTemplate.QuestionPresentation!.RootLevel} from the configured Template Standard.");
        }

        var answerEndLine = lines.Count;
        foreach (var pair in blocks)
        {
            if (!questionIndex.TryGetValue(pair.Key, out var info))
            {
                return MarkerlessRootReadResult.Failure(
                    $"DOCART015: Materialized question '{pair.Key}' is not defined by the installed Docs Standard.");
            }

            if (info.Depth == 0)
                continue;

            var expectedLevel = materializationTemplate.QuestionPresentation.RootLevel + info.Depth;
            if (expectedLevel > 6)
            {
                return MarkerlessRootReadResult.Failure(
                    $"TMPL107: Configured template '{materializationTemplate.Id}' maps semantic depth {info.Depth} to Markdown heading level {expectedLevel}, beyond the supported maximum of 6.");
            }

            var heading = FindQuestionHeadingLine(
                lines,
                pair.Value.StartLine,
                expectedLevel,
                frontMatterClosingLine + 1);
            if (heading is null)
            {
                return MarkerlessRootReadResult.Failure(
                    $"DOCART027: Materialized question '{pair.Key}' must be preceded by its configured heading level {expectedLevel}.");
            }

            answerEndLine = Math.Min(answerEndLine, heading.Value);
        }

        foreach (var pair in answerInstances)
        {
            if (pair.Value.Count == 0)
                continue;

            if (!questionIndex.TryGetValue(pair.Key, out var info))
            {
                return MarkerlessRootReadResult.Failure(
                    $"DOCART033: AnswerInstances refer to question '{pair.Key}', which is not defined by the installed Docs Standard.");
            }

            if (info.Depth == 0)
                continue;

            var expectedLevel = materializationTemplate.QuestionPresentation.RootLevel + info.Depth;
            if (expectedLevel > 6)
            {
                return MarkerlessRootReadResult.Failure(
                    $"TMPL107: Configured template '{materializationTemplate.Id}' maps semantic depth {info.Depth} to Markdown heading level {expectedLevel}, beyond the supported maximum of 6.");
            }

            var first = pair.Value.OrderBy(instance => instance.StartLine).First();
            var heading = FindQuestionHeadingLine(
                lines,
                first.StartLine,
                expectedLevel,
                frontMatterClosingLine + 1);
            if (heading is null)
            {
                return MarkerlessRootReadResult.Failure(
                    $"DOCART036: Repeated question '{pair.Key}' must be preceded by its configured heading level {expectedLevel}.");
            }

            answerEndLine = Math.Min(answerEndLine, heading.Value);
        }

        var hasAnswer = lines
            .Skip(rootLine.Index + 1)
            .Take(Math.Max(0, answerEndLine - rootLine.Index - 1))
            .Any(line => !string.IsNullOrWhiteSpace(line));

        if (!hasAnswer)
        {
            return MarkerlessRootReadResult.Unanswered(
                new UnansweredRoot(
                    rootQuestion.Id,
                    rootLine.Index,
                    PlaceholderLine: null));
        }

        return MarkerlessRootReadResult.Answered(
            new QuestionBlock(
                rootLine.Index,
                answerEndLine,
                IsMarkerless: true));
    }

    private static int? FindQuestionHeadingLine(
        IReadOnlyList<string> lines,
        int markerLine,
        int expectedLevel,
        int minimumLine)
    {
        for (var index = markerLine - 1; index >= minimumLine; index--)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                continue;

            return TryReadHeadingLevel(lines[index], out var level) &&
                   level == expectedLevel
                ? index
                : null;
        }

        return null;
    }

    private static bool TryReadHeadingLevel(string line, out int level)
    {
        level = 0;
        var trimmed = line.TrimStart();
        while (level < trimmed.Length && trimmed[level] == '#')
            level++;

        if (level is < 1 or > 6 ||
            level >= trimmed.Length ||
            trimmed[level] != ' ' ||
            string.IsNullOrWhiteSpace(trimmed[(level + 1)..]))
        {
            level = 0;
            return false;
        }

        return true;
    }

    private static QuestionBlockReadResult ReadQuestionBlock(
        IReadOnlyList<string> lines,
        int startLine,
        string questionId,
        IReadOnlyDictionary<string, QuestionBlock> blocks)
    {
        if (blocks.ContainsKey(questionId))
        {
            return QuestionBlockReadResult.Failure(
                $"DOCART004: Document contains duplicate materialization for question '{questionId}'.");
        }

        var endLine = -1;
        for (var candidate = startLine + 1; candidate < lines.Count; candidate++)
        {
            var nested = lines[candidate].Trim();
            if (nested.Equals(QuestionEndMarker, StringComparison.Ordinal))
            {
                endLine = candidate;
                break;
            }

            if (nested.StartsWith(QuestionStem, StringComparison.Ordinal) ||
                nested.StartsWith(PlaceholderStem, StringComparison.Ordinal))
            {
                return QuestionBlockReadResult.Failure(
                    $"DOCART005: Question '{questionId}' contains nested VSlices document metadata before its closing marker.");
            }
        }

        if (endLine < 0)
        {
            return QuestionBlockReadResult.Failure(
                $"DOCART006: Question '{questionId}' is missing '{QuestionEndMarker}'.");
        }

        var answer = string.Join(
            "\n",
            lines.Skip(startLine + 1).Take(endLine - startLine - 1));
        if (string.IsNullOrWhiteSpace(answer))
        {
            return QuestionBlockReadResult.Failure(
                $"DOCART007: Materialized question '{questionId}' must contain a non-empty answer.");
        }

        return QuestionBlockReadResult.Success(
            new QuestionBlock(
                startLine,
                endLine,
                IsMarkerless: false));
    }

    private static IReadOnlyList<DocumentQuestionAffordance> BuildSurface(
        DocumentDefinition definition,
        IReadOnlyDictionary<string, QuestionBlock> blocks,
        IReadOnlyDictionary<string, IReadOnlyList<AnswerInstanceBlock>> answerInstances,
        UnansweredRoot? unansweredRoot)
    {
        var surface = new List<DocumentQuestionAffordance>();

        if (unansweredRoot is not null)
        {
            surface.Add(new DocumentQuestionAffordance(
                1,
                definition.RootQuestion.Id,
                definition.RootQuestion.Text,
                0,
                definition.RootQuestion.Cardinality,
                IsMaterialized: true,
                IsAnswered: false,
                AnswerInstanceId: null,
                AnswerPreview: null));
            return surface;
        }

        Add(definition.RootQuestion, 0);
        return surface;

        void Add(DocumentQuestionDefinition question, int depth)
        {
            if (question.Cardinality == DocumentQuestionCardinality.Many)
            {
                if (answerInstances.TryGetValue(question.Id, out var instances))
                {
                    foreach (var instance in instances)
                    {
                        surface.Add(new DocumentQuestionAffordance(
                            surface.Count + 1,
                            question.Id,
                            question.Text,
                            depth,
                            question.Cardinality,
                            IsMaterialized: true,
                            IsAnswered: true,
                            instance.Id,
                            instance.AnswerPreview));
                    }
                }

                surface.Add(new DocumentQuestionAffordance(
                    surface.Count + 1,
                    question.Id,
                    question.Text,
                    depth,
                    question.Cardinality,
                    IsMaterialized: false,
                    IsAnswered: false,
                    AnswerInstanceId: null,
                    AnswerPreview: null));

                // Children of a repeated question are scoped through a concrete
                // AnswerInstance. That is the next deliberate authoring slice.
                return;
            }

            var materialized = blocks.ContainsKey(question.Id);
            surface.Add(new DocumentQuestionAffordance(
                surface.Count + 1,
                question.Id,
                question.Text,
                depth,
                question.Cardinality,
                materialized,
                IsAnswered: materialized,
                AnswerInstanceId: null,
                AnswerPreview: null));

            if (!materialized)
                return;

            foreach (var child in question.Children)
                Add(child, depth + 1);
        }
    }

    private static IReadOnlyDictionary<string, QuestionInfo> BuildQuestionIndex(
        DocumentQuestionDefinition root)
    {
        var index = new Dictionary<string, QuestionInfo>(StringComparer.Ordinal);
        Add(root, parentId: null, depth: 0);
        return index;

        void Add(DocumentQuestionDefinition question, string? parentId, int depth)
        {
            index.Add(question.Id, new QuestionInfo(parentId, depth, question));
            foreach (var child in question.Children)
                Add(child, question.Id, depth + 1);
        }
    }

    private static IReadOnlyList<string> RenderAnswerBlock(
        string documentType,
        string questionId,
        IReadOnlyList<string> answerLines,
        bool usesFrontMatterIdentity)
    {
        var marker = usesFrontMatterIdentity
            ? $"<!-- vslices:question question={questionId} -->"
            : $"<!-- vslices:question document={documentType} question={questionId} -->";

        var result = new List<string> { marker };
        result.AddRange(answerLines);
        result.Add(QuestionEndMarker);
        return result;
    }

    private static IReadOnlyList<string> RenderAnswerInstanceBlock(
        string questionId,
        string answerInstanceId,
        IReadOnlyList<string> answerLines)
    {
        var result = new List<string>
        {
            $"<!-- vslices:answer-instance question={questionId} id={answerInstanceId} -->"
        };
        result.AddRange(answerLines);
        result.Add(AnswerInstanceEndMarker);
        return result;
    }

    private static void AppendManyQuestion(
        List<string> lines,
        string renderedQuestion,
        IReadOnlyList<string> renderedInstance)
    {
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        if (lines.Count > 0)
            lines.Add(string.Empty);

        lines.Add(renderedQuestion);
        lines.Add(string.Empty);
        lines.AddRange(renderedInstance);
        lines.Add(string.Empty);
    }

    private static void InsertAnswerInstanceAfter(
        List<string> lines,
        int endLine,
        IReadOnlyList<string> renderedInstance)
    {
        var insertion = new List<string> { string.Empty };
        insertion.AddRange(renderedInstance);
        insertion.Add(string.Empty);
        lines.InsertRange(endLine + 1, insertion);
    }

    private void AppendQuestion(
        List<string> lines,
        DocumentQuestionAffordance question,
        string renderedQuestion,
        IReadOnlyList<string> answerLines)
    {
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        if (lines.Count > 0)
            lines.Add(string.Empty);

        lines.Add(renderedQuestion);
        lines.Add(string.Empty);
        lines.AddRange(
            RenderAnswerBlock(
                definition.Type,
                question.QuestionId,
                answerLines,
                usesFrontMatterIdentity));
        lines.Add(string.Empty);
    }

    private static bool ContainsReservedMetadata(string answer)
    {
        foreach (var line in NormalizeNewlines(answer).Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(PlaceholderStem, StringComparison.Ordinal) ||
                trimmed.StartsWith(QuestionStem, StringComparison.Ordinal) ||
                trimmed.StartsWith(AnswerInstanceStem, StringComparison.Ordinal) ||
                trimmed.Equals(QuestionEndMarker, StringComparison.Ordinal) ||
                trimmed.Equals(AnswerInstanceEndMarker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseAnswerInstanceMarker(
        string value,
        out string questionId,
        out string answerInstanceId)
    {
        questionId = string.Empty;
        answerInstanceId = string.Empty;

        if (!value.StartsWith(AnswerInstancePrefix, StringComparison.Ordinal) ||
            !value.EndsWith(MarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = value[AnswerInstancePrefix.Length..^MarkerSuffix.Length];
        var separator = payload.IndexOf(AnswerInstanceIdSeparator, StringComparison.Ordinal);
        if (separator <= 0)
            return false;

        questionId = payload[..separator];
        answerInstanceId = payload[(separator + AnswerInstanceIdSeparator.Length)..];

        return IsStableIdentifier(questionId) &&
               IsStableIdentifier(answerInstanceId);
    }

    private static bool TryParseQuestionIdentityMarker(
        string value,
        string prefix,
        out string questionId)
    {
        questionId = string.Empty;

        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            !value.EndsWith(MarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        questionId = value[prefix.Length..^MarkerSuffix.Length];
        return IsStableIdentifier(questionId);
    }

    private static bool TryParseLegacyMarker(
        string value,
        string prefix,
        out string documentType,
        out string questionId)
    {
        documentType = string.Empty;
        questionId = string.Empty;

        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            !value.EndsWith(MarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = value[prefix.Length..^MarkerSuffix.Length];
        var separator = payload.IndexOf(QuestionSeparator, StringComparison.Ordinal);
        if (separator <= 0)
            return false;

        documentType = payload[..separator];
        questionId = payload[(separator + QuestionSeparator.Length)..];
        return IsStableIdentifier(documentType) && IsStableIdentifier(questionId);
    }

    private static bool TryAcceptDocumentType(
        ref string? current,
        string candidate)
    {
        if (current is null)
        {
            current = candidate;
            return true;
        }

        return current.Equals(candidate, StringComparison.Ordinal);
    }

    private static bool IsStableIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !char.IsLetter(value[0]))
            return false;

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsLetterOrDigit(character) && character is not '-' and not '_')
                return false;
        }

        return true;
    }

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private sealed record UnansweredRoot(
        string QuestionId,
        int? HeadingLine,
        int? PlaceholderLine);
    private sealed record QuestionBlock(
        int StartLine,
        int EndLine,
        bool IsMarkerless);
    private sealed record AnswerInstanceBlock(
        string QuestionId,
        string Id,
        int StartLine,
        int EndLine,
        string AnswerPreview);
    private sealed record QuestionInfo(
        string? ParentId,
        int Depth,
        DocumentQuestionDefinition Definition);

    private sealed record MarkerlessRootReadResult(
        UnansweredRoot? UnansweredRoot,
        QuestionBlock? AnsweredBlock,
        string? Error)
    {
        public bool IsSuccess =>
            Error is null &&
            ((UnansweredRoot is not null) != (AnsweredBlock is not null));

        public static MarkerlessRootReadResult Unanswered(UnansweredRoot root) =>
            new(root, null, null);

        public static MarkerlessRootReadResult Answered(QuestionBlock block) =>
            new(null, block, null);

        public static MarkerlessRootReadResult Failure(string error) =>
            new(null, null, error);
    }

    private sealed record QuestionBlockReadResult(
        QuestionBlock? Block,
        string? Error)
    {
        public bool IsSuccess => Block is not null && Error is null;
        public static QuestionBlockReadResult Success(QuestionBlock block) => new(block, null);
        public static QuestionBlockReadResult Failure(string error) => new(null, error);
    }

    private static AnswerInstanceBlockReadResult ReadAnswerInstanceBlock(
        IReadOnlyList<string> lines,
        int startLine,
        string questionId,
        string answerInstanceId)
    {
        var endLine = -1;
        for (var candidate = startLine + 1; candidate < lines.Count; candidate++)
        {
            var nested = lines[candidate].Trim();
            if (nested.Equals(AnswerInstanceEndMarker, StringComparison.Ordinal))
            {
                endLine = candidate;
                break;
            }

            if (nested.StartsWith(QuestionStem, StringComparison.Ordinal) ||
                nested.StartsWith(PlaceholderStem, StringComparison.Ordinal) ||
                nested.StartsWith(AnswerInstanceStem, StringComparison.Ordinal))
            {
                return AnswerInstanceBlockReadResult.Failure(
                    $"DOCART037: AnswerInstance '{answerInstanceId}' for question '{questionId}' contains nested VSlices metadata before its closing marker.");
            }
        }

        if (endLine < 0)
        {
            return AnswerInstanceBlockReadResult.Failure(
                $"DOCART038: AnswerInstance '{answerInstanceId}' for question '{questionId}' is missing '{AnswerInstanceEndMarker}'.");
        }

        var answerLines = lines
            .Skip(startLine + 1)
            .Take(endLine - startLine - 1)
            .ToArray();
        if (!answerLines.Any(line => !string.IsNullOrWhiteSpace(line)))
        {
            return AnswerInstanceBlockReadResult.Failure(
                $"DOCART039: AnswerInstance '{answerInstanceId}' for question '{questionId}' must contain a non-empty answer.");
        }

        var preview = answerLines
            .First(line => !string.IsNullOrWhiteSpace(line))
            .Trim();

        return AnswerInstanceBlockReadResult.Success(
            new AnswerInstanceBlock(
                questionId,
                answerInstanceId,
                startLine,
                endLine,
                preview));
    }

    private sealed record AnswerInstanceBlockReadResult(
        AnswerInstanceBlock? Block,
        string? Error)
    {
        public bool IsSuccess => Block is not null && Error is null;
        public static AnswerInstanceBlockReadResult Success(AnswerInstanceBlock block) =>
            new(block, null);
        public static AnswerInstanceBlockReadResult Failure(string error) =>
            new(null, error);
    }
}
