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
    string? AnswerPreview,
    string? ScopeAnswerInstanceId,
    bool HasChildren);

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
    private const string AnswerInstanceParentSeparator = " parent-answer-instance=";
    private const string AnswerInstanceEndMarker = "<!-- /vslices:answer-instance -->";
    private const string ScopedQuestionPrefix = "<!-- vslices:scoped-question question=";
    private const string ScopedQuestionStem = "<!-- vslices:scoped-question";
    private const string ScopedQuestionParentSeparator = " parent-answer-instance=";
    private const string ScopedQuestionEndMarker = "<!-- /vslices:scoped-question -->";

    private readonly string normalizedSource;
    private readonly string newline;
    private readonly DocumentDefinition definition;
    private readonly IReadOnlyDictionary<string, QuestionBlock> blocks;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AnswerInstanceBlock>> answerInstances;
    private readonly IReadOnlyDictionary<ScopedQuestionKey, ScopedQuestionBlock> scopedBlocks;
    private readonly UnansweredRoot? unansweredRoot;
    private readonly bool usesFrontMatterIdentity;

    private DocumentArtifact(
        string normalizedSource,
        string newline,
        DocumentDefinition definition,
        IReadOnlyDictionary<string, QuestionBlock> blocks,
        IReadOnlyDictionary<string, IReadOnlyList<AnswerInstanceBlock>> answerInstances,
        IReadOnlyDictionary<ScopedQuestionKey, ScopedQuestionBlock> scopedBlocks,
        UnansweredRoot? unansweredRoot,
        bool usesFrontMatterIdentity,
        IReadOnlyList<DocumentQuestionAffordance> surface)
    {
        this.normalizedSource = normalizedSource;
        this.newline = newline;
        this.definition = definition;
        this.blocks = blocks;
        this.answerInstances = answerInstances;
        this.scopedBlocks = scopedBlocks;
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
        var scopedBlocks = new Dictionary<ScopedQuestionKey, ScopedQuestionBlock>();
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
                    out var answerInstanceId,
                    out var answerInstanceParentId))
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
                    answerInstanceId,
                    answerInstanceParentId);
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

            if (TryParseScopedQuestionMarker(
                    trimmed,
                    out var scopedQuestionId,
                    out var scopedParentAnswerInstanceId))
            {
                var key = new ScopedQuestionKey(scopedParentAnswerInstanceId, scopedQuestionId);
                if (scopedBlocks.ContainsKey(key))
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART040: Duplicate scoped materialization for question '{scopedQuestionId}' under AnswerInstance '{scopedParentAnswerInstanceId}'.");
                }

                var scoped = ReadScopedQuestionBlock(
                    lines,
                    index,
                    scopedQuestionId,
                    scopedParentAnswerInstanceId);
                if (!scoped.IsSuccess)
                    return DocumentArtifactReadResult.Failure(scoped.Error!);

                scopedBlocks.Add(key, scoped.Block!);
                index = scoped.Block!.EndLine;
                continue;
            }

            if (trimmed.StartsWith(ScopedQuestionStem, StringComparison.Ordinal))
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART041: Malformed scoped question metadata at line {index + 1}.");
            }

            if (trimmed.Equals(ScopedQuestionEndMarker, StringComparison.Ordinal))
            {
                return DocumentArtifactReadResult.Failure(
                    $"DOCART042: Orphan scoped question closing marker at line {index + 1}.");
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

            if (blocks.Count > 0 || answerInstances.Count > 0 || scopedBlocks.Count > 0)
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

            if (unansweredRoot is not null && (blocks.Count > 0 || answerInstances.Count > 0 || scopedBlocks.Count > 0))
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
                foreach (var instance in pair.Value)
                {
                    if (instance.ParentAnswerInstanceId is null)
                    {
                        if (parent.Definition.Cardinality == DocumentQuestionCardinality.Many)
                        {
                            return DocumentArtifactReadResult.Failure(
                                $"DOCART052: Repeated question '{pair.Key}' under repeated parent '{info.ParentId}' must preserve parent AnswerInstance scope.");
                        }

                        if (!blocks.ContainsKey(info.ParentId))
                        {
                            return DocumentArtifactReadResult.Failure(
                                $"DOCART016: Materialized question '{pair.Key}' requires answered parent '{info.ParentId}'.");
                        }

                        continue;
                    }

                    if (!answerInstanceIds.Contains(instance.ParentAnswerInstanceId))
                    {
                        return DocumentArtifactReadResult.Failure(
                            $"DOCART053: AnswerInstance '{instance.Id}' for question '{pair.Key}' refers to unknown parent AnswerInstance '{instance.ParentAnswerInstanceId}'.");
                    }

                    if (parent.Definition.Cardinality == DocumentQuestionCardinality.Many)
                    {
                        var repeatedParent = answerInstances
                            .SelectMany(candidate => candidate.Value)
                            .FirstOrDefault(candidate =>
                                candidate.Id.Equals(instance.ParentAnswerInstanceId, StringComparison.Ordinal));
                        if (repeatedParent is null ||
                            !repeatedParent.QuestionId.Equals(info.ParentId, StringComparison.Ordinal))
                        {
                            return DocumentArtifactReadResult.Failure(
                                $"DOCART054: AnswerInstance '{instance.Id}' for question '{pair.Key}' is not scoped to an AnswerInstance of repeated parent '{info.ParentId}'.");
                        }
                    }
                    else
                    {
                        var scopedParentKey = new ScopedQuestionKey(
                            instance.ParentAnswerInstanceId,
                            info.ParentId);
                        if (!scopedBlocks.ContainsKey(scopedParentKey))
                        {
                            return DocumentArtifactReadResult.Failure(
                                $"DOCART055: AnswerInstance '{instance.Id}' for question '{pair.Key}' requires answered scoped parent '{info.ParentId}' under AnswerInstance '{instance.ParentAnswerInstanceId}'.");
                        }
                    }
                }
            }

            var instancesById = answerInstances
                .SelectMany(pair => pair.Value)
                .ToDictionary(instance => instance.Id, StringComparer.Ordinal);

            foreach (var pair in scopedBlocks)
            {
                var key = pair.Key;
                if (!questionIndex.TryGetValue(key.QuestionId, out var info))
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART043: Scoped question '{key.QuestionId}' is not defined by the installed Docs Standard.");
                }

                if (info.Definition.Cardinality != DocumentQuestionCardinality.One)
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART044: Scoped question '{key.QuestionId}' has cardinality 'many'. Nested repeated authoring is not supported by the current preview.");
                }

                if (info.ParentId is null)
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART045: Root question '{key.QuestionId}' cannot be scoped through an AnswerInstance.");
                }

                if (!instancesById.TryGetValue(key.AnswerInstanceId, out var parentInstance))
                {
                    return DocumentArtifactReadResult.Failure(
                        $"DOCART046: Scoped question '{key.QuestionId}' refers to unknown AnswerInstance '{key.AnswerInstanceId}'.");
                }

                var parentInfo = questionIndex[info.ParentId];
                if (parentInfo.Definition.Cardinality == DocumentQuestionCardinality.Many)
                {
                    if (!parentInstance.QuestionId.Equals(info.ParentId, StringComparison.Ordinal))
                    {
                        return DocumentArtifactReadResult.Failure(
                            $"DOCART047: Scoped question '{key.QuestionId}' belongs to repeated parent question '{info.ParentId}', not AnswerInstance '{key.AnswerInstanceId}' of question '{parentInstance.QuestionId}'.");
                    }
                }
                else
                {
                    var scopedParentKey = new ScopedQuestionKey(
                        key.AnswerInstanceId,
                        info.ParentId);

                    if (!scopedBlocks.ContainsKey(scopedParentKey))
                    {
                        return DocumentArtifactReadResult.Failure(
                            $"DOCART051: Scoped question '{key.QuestionId}' requires answered scoped parent '{info.ParentId}' under AnswerInstance '{key.AnswerInstanceId}'.");
                    }
                }
            }
        }

        var readonlyAnswerInstances = answerInstances.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<AnswerInstanceBlock>)pair.Value
                .OrderBy(instance => instance.StartLine)
                .ToArray(),
            StringComparer.Ordinal);

        var readonlyScopedBlocks = scopedBlocks.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);

        var surface = BuildSurface(
            definition,
            blocks,
            readonlyAnswerInstances,
            readonlyScopedBlocks,
            unansweredRoot);
        return DocumentArtifactReadResult.Success(
            new DocumentArtifact(
                normalized,
                newline,
                definition,
                blocks,
                readonlyAnswerInstances,
                readonlyScopedBlocks,
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
                selected.ScopeAnswerInstanceId,
                answerLines);

            var matchingInstances = answerInstances.TryGetValue(selected.QuestionId, out var existing)
                ? existing
                    .Where(instance =>
                        string.Equals(
                            instance.ParentAnswerInstanceId,
                            selected.ScopeAnswerInstanceId,
                            StringComparison.Ordinal))
                    .OrderBy(instance => instance.StartLine)
                    .ToArray()
                : [];

            if (selected.ScopeAnswerInstanceId is null)
            {
                if (matchingInstances.Length == 0)
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
                        matchingInstances[^1].EndLine,
                        renderedInstance);
                }
            }
            else
            {
                if (matchingInstances.Length == 0)
                {
                    var questionIndex = BuildQuestionIndex(definition.RootQuestion);
                    var selectedInfo = questionIndex[selected.QuestionId];
                    var parentQuestionId = selectedInfo.ParentId
                        ?? throw new InvalidOperationException(
                            $"Scoped repeated question '{selected.QuestionId}' must have a parent.");

                    int insertionBase;
                    if (questionIndex[parentQuestionId].Definition.Cardinality ==
                        DocumentQuestionCardinality.Many)
                    {
                        var parentInstance = answerInstances
                            .SelectMany(pair => pair.Value)
                            .Single(instance => instance.Id.Equals(
                                selected.ScopeAnswerInstanceId,
                                StringComparison.Ordinal));
                        insertionBase = parentInstance.EndLine;
                    }
                    else
                    {
                        var scopedParentKey = new ScopedQuestionKey(
                            selected.ScopeAnswerInstanceId,
                            parentQuestionId);
                        if (!scopedBlocks.TryGetValue(scopedParentKey, out var scopedParent))
                        {
                            return DocumentArtifactMutationResult.Failure(
                                $"UPDATE114: Repeated scoped question '{selected.QuestionId}' requires answered scoped parent '{parentQuestionId}' under AnswerInstance '{selected.ScopeAnswerInstanceId}'.");
                        }

                        insertionBase = scopedParent.EndLine;
                    }

                    InsertScopedQuestionAfter(
                        lines,
                        insertionBase,
                        renderedQuestion.Source!,
                        renderedInstance);
                }
                else
                {
                    InsertAnswerInstanceAfter(
                        lines,
                        matchingInstances[^1].EndLine,
                        renderedInstance);
                }
            }

            var repeatedCandidate = string.Join("\n", lines);
            if (!newline.Equals("\n", StringComparison.Ordinal))
                repeatedCandidate = repeatedCandidate.Replace("\n", newline, StringComparison.Ordinal);

            return DocumentArtifactMutationResult.Success(repeatedCandidate, selected);
        }

        if (selected.ScopeAnswerInstanceId is not null)
        {
            if (!string.Equals(
                    materializationTemplate.ScopedChildStrategy,
                    "parent-answer-instance-marker",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    materializationTemplate.ScopedQuestionStrategy,
                    "explicit-parent-answer-instance-marker",
                    StringComparison.Ordinal))
            {
                return DocumentArtifactMutationResult.Failure(
                    $"UPDATE112: Configured template '{materializationTemplate.Id}' does not define the scoped-child materialization contract required below a repeated AnswerInstance.");
            }

            var key = new ScopedQuestionKey(
                selected.ScopeAnswerInstanceId,
                selected.QuestionId);

            if (scopedBlocks.TryGetValue(key, out var scopedBlock))
            {
                var answerLineCount = scopedBlock.EndLine - scopedBlock.StartLine - 1;
                if (answerLineCount > 0)
                    lines.RemoveRange(scopedBlock.StartLine + 1, answerLineCount);
                lines.InsertRange(scopedBlock.StartLine + 1, answerLines);
            }
            else
            {
                var renderedQuestion = DocumentMaterialization.RenderQuestion(
                    selected.Text,
                    selected.Depth,
                    materializationTemplate);
                if (!renderedQuestion.IsSuccess)
                    return DocumentArtifactMutationResult.Failure(renderedQuestion.Error!);

                var renderedScoped = RenderScopedQuestionBlock(
                    selected.QuestionId,
                    selected.ScopeAnswerInstanceId,
                    answerLines);

                var parentInstance = answerInstances
                    .SelectMany(pair => pair.Value)
                    .Single(instance => instance.Id.Equals(
                        selected.ScopeAnswerInstanceId,
                        StringComparison.Ordinal));

                var questionIndex = BuildQuestionIndex(definition.RootQuestion);
                var selectedInfo = questionIndex[selected.QuestionId];
                var parentQuestionId = selectedInfo.ParentId
                    ?? throw new InvalidOperationException(
                        $"Scoped question '{selected.QuestionId}' must have a parent.");

                int insertionBase;
                if (questionIndex[parentQuestionId].Definition.Cardinality ==
                    DocumentQuestionCardinality.Many)
                {
                    insertionBase = parentInstance.EndLine;
                }
                else
                {
                    var scopedParentKey = new ScopedQuestionKey(
                        selected.ScopeAnswerInstanceId,
                        parentQuestionId);
                    if (!scopedBlocks.TryGetValue(scopedParentKey, out var scopedParent))
                    {
                        return DocumentArtifactMutationResult.Failure(
                            $"UPDATE114: Scoped question '{selected.QuestionId}' requires answered scoped parent '{parentQuestionId}' under AnswerInstance '{selected.ScopeAnswerInstanceId}'.");
                    }

                    insertionBase = scopedParent.EndLine;
                }

                var insertionAfter = scopedBlocks
                    .Where(pair =>
                        pair.Key.AnswerInstanceId.Equals(
                            selected.ScopeAnswerInstanceId,
                            StringComparison.Ordinal) &&
                        IsDescendantOf(
                            pair.Key.QuestionId,
                            parentQuestionId,
                            questionIndex))
                    .Select(pair => pair.Value.EndLine)
                    .Append(insertionBase)
                    .Max();

                InsertScopedQuestionAfter(
                    lines,
                    insertionAfter,
                    renderedQuestion.Source!,
                    renderedScoped);
            }

            var scopedCandidate = string.Join("\n", lines);
            if (!newline.Equals("\n", StringComparison.Ordinal))
                scopedCandidate = scopedCandidate.Replace("\n", newline, StringComparison.Ordinal);

            return DocumentArtifactMutationResult.Success(scopedCandidate, selected);
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
        IReadOnlyDictionary<ScopedQuestionKey, ScopedQuestionBlock> scopedBlocks,
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
                AnswerPreview: null,
                ScopeAnswerInstanceId: null,
                HasChildren: definition.RootQuestion.Children.Count > 0));
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
                    foreach (var instance in instances.Where(instance => instance.ParentAnswerInstanceId is null))
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
                            instance.AnswerPreview,
                            ScopeAnswerInstanceId: null,
                            HasChildren: question.Children.Count > 0));

                        foreach (var child in question.Children)
                            AddScoped(child, depth + 1, instance.Id);
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
                    AnswerPreview: null,
                    ScopeAnswerInstanceId: null,
                    HasChildren: question.Children.Count > 0));

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
                AnswerPreview: null,
                ScopeAnswerInstanceId: null,
                HasChildren: question.Children.Count > 0));

            if (!materialized)
                return;

            foreach (var child in question.Children)
                Add(child, depth + 1);
        }

        void AddScoped(
            DocumentQuestionDefinition question,
            int depth,
            string answerInstanceId)
        {
            if (question.Cardinality == DocumentQuestionCardinality.Many)
            {
                if (answerInstances.TryGetValue(question.Id, out var instances))
                {
                    foreach (var instance in instances.Where(instance =>
                                 string.Equals(
                                     instance.ParentAnswerInstanceId,
                                     answerInstanceId,
                                     StringComparison.Ordinal)))
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
                            instance.AnswerPreview,
                            ScopeAnswerInstanceId: answerInstanceId,
                            HasChildren: question.Children.Count > 0));

                        foreach (var child in question.Children)
                            AddScoped(child, depth + 1, instance.Id);
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
                    AnswerPreview: null,
                    ScopeAnswerInstanceId: answerInstanceId,
                    HasChildren: question.Children.Count > 0));
                return;
            }

            var key = new ScopedQuestionKey(answerInstanceId, question.Id);
            var materialized = scopedBlocks.ContainsKey(key);
            surface.Add(new DocumentQuestionAffordance(
                surface.Count + 1,
                question.Id,
                question.Text,
                depth,
                question.Cardinality,
                IsMaterialized: materialized,
                IsAnswered: materialized,
                AnswerInstanceId: null,
                AnswerPreview: null,
                ScopeAnswerInstanceId: answerInstanceId,
                HasChildren: question.Children.Count > 0));

            if (!materialized)
                return;

            foreach (var child in question.Children)
                AddScoped(child, depth + 1, answerInstanceId);
        }
    }

    private static bool IsDescendantOf(
        string candidateQuestionId,
        string ancestorQuestionId,
        IReadOnlyDictionary<string, QuestionInfo> questionIndex)
    {
        if (!questionIndex.TryGetValue(candidateQuestionId, out var current))
            return false;

        var parentId = current.ParentId;
        while (parentId is not null)
        {
            if (parentId.Equals(ancestorQuestionId, StringComparison.Ordinal))
                return true;

            if (!questionIndex.TryGetValue(parentId, out current))
                return false;

            parentId = current.ParentId;
        }

        return false;
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

    private static IReadOnlyList<string> RenderScopedQuestionBlock(
        string questionId,
        string answerInstanceId,
        IReadOnlyList<string> answerLines)
    {
        var result = new List<string>
        {
            $"<!-- vslices:scoped-question question={questionId} parent-answer-instance={answerInstanceId} -->"
        };
        result.AddRange(answerLines);
        result.Add(ScopedQuestionEndMarker);
        return result;
    }

    private static IReadOnlyList<string> RenderAnswerInstanceBlock(
        string questionId,
        string answerInstanceId,
        string? parentAnswerInstanceId,
        IReadOnlyList<string> answerLines)
    {
        var parentScope = parentAnswerInstanceId is null
            ? string.Empty
            : $" parent-answer-instance={parentAnswerInstanceId}";
        var result = new List<string>
        {
            $"<!-- vslices:answer-instance question={questionId} id={answerInstanceId}{parentScope} -->"
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

    private static void InsertScopedQuestionAfter(
        List<string> lines,
        int endLine,
        string renderedQuestion,
        IReadOnlyList<string> renderedBlock)
    {
        var insertion = new List<string>
        {
            string.Empty,
            renderedQuestion,
            string.Empty
        };
        insertion.AddRange(renderedBlock);
        insertion.Add(string.Empty);
        lines.InsertRange(endLine + 1, insertion);
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
                trimmed.StartsWith(ScopedQuestionStem, StringComparison.Ordinal) ||
                trimmed.Equals(QuestionEndMarker, StringComparison.Ordinal) ||
                trimmed.Equals(AnswerInstanceEndMarker, StringComparison.Ordinal) ||
                trimmed.Equals(ScopedQuestionEndMarker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseScopedQuestionMarker(
        string value,
        out string questionId,
        out string answerInstanceId)
    {
        questionId = string.Empty;
        answerInstanceId = string.Empty;

        if (!value.StartsWith(ScopedQuestionPrefix, StringComparison.Ordinal) ||
            !value.EndsWith(MarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = value[ScopedQuestionPrefix.Length..^MarkerSuffix.Length];
        var separator = payload.IndexOf(ScopedQuestionParentSeparator, StringComparison.Ordinal);
        if (separator <= 0)
            return false;

        questionId = payload[..separator];
        answerInstanceId = payload[(separator + ScopedQuestionParentSeparator.Length)..];

        return IsStableIdentifier(questionId) &&
               IsStableIdentifier(answerInstanceId);
    }

    private static bool TryParseAnswerInstanceMarker(
        string value,
        out string questionId,
        out string answerInstanceId,
        out string? parentAnswerInstanceId)
    {
        questionId = string.Empty;
        answerInstanceId = string.Empty;
        parentAnswerInstanceId = null;

        if (!value.StartsWith(AnswerInstancePrefix, StringComparison.Ordinal) ||
            !value.EndsWith(MarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = value[AnswerInstancePrefix.Length..^MarkerSuffix.Length];
        var idSeparator = payload.IndexOf(AnswerInstanceIdSeparator, StringComparison.Ordinal);
        if (idSeparator <= 0)
            return false;

        questionId = payload[..idSeparator];
        var remainder = payload[(idSeparator + AnswerInstanceIdSeparator.Length)..];
        var parentSeparator = remainder.IndexOf(AnswerInstanceParentSeparator, StringComparison.Ordinal);
        if (parentSeparator >= 0)
        {
            answerInstanceId = remainder[..parentSeparator];
            parentAnswerInstanceId = remainder[(parentSeparator + AnswerInstanceParentSeparator.Length)..];
        }
        else
        {
            answerInstanceId = remainder;
        }

        return IsStableIdentifier(questionId) &&
               IsStableIdentifier(answerInstanceId) &&
               (parentAnswerInstanceId is null || IsStableIdentifier(parentAnswerInstanceId));
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
        string? ParentAnswerInstanceId,
        int StartLine,
        int EndLine,
        string AnswerPreview);
    private sealed record ScopedQuestionKey(
        string AnswerInstanceId,
        string QuestionId);
    private sealed record ScopedQuestionBlock(
        string QuestionId,
        string AnswerInstanceId,
        int StartLine,
        int EndLine);
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

    private static ScopedQuestionBlockReadResult ReadScopedQuestionBlock(
        IReadOnlyList<string> lines,
        int startLine,
        string questionId,
        string answerInstanceId)
    {
        var endLine = -1;
        for (var candidate = startLine + 1; candidate < lines.Count; candidate++)
        {
            var nested = lines[candidate].Trim();
            if (nested.Equals(ScopedQuestionEndMarker, StringComparison.Ordinal))
            {
                endLine = candidate;
                break;
            }

            if (nested.StartsWith(QuestionStem, StringComparison.Ordinal) ||
                nested.StartsWith(PlaceholderStem, StringComparison.Ordinal) ||
                nested.StartsWith(AnswerInstanceStem, StringComparison.Ordinal) ||
                nested.StartsWith(ScopedQuestionStem, StringComparison.Ordinal))
            {
                return ScopedQuestionBlockReadResult.Failure(
                    $"DOCART048: Scoped question '{questionId}' under AnswerInstance '{answerInstanceId}' contains nested VSlices metadata before its closing marker.");
            }
        }

        if (endLine < 0)
        {
            return ScopedQuestionBlockReadResult.Failure(
                $"DOCART049: Scoped question '{questionId}' under AnswerInstance '{answerInstanceId}' is missing '{ScopedQuestionEndMarker}'.");
        }

        var answer = string.Join(
            "\n",
            lines.Skip(startLine + 1).Take(endLine - startLine - 1));
        if (string.IsNullOrWhiteSpace(answer))
        {
            return ScopedQuestionBlockReadResult.Failure(
                $"DOCART050: Scoped question '{questionId}' under AnswerInstance '{answerInstanceId}' must contain a non-empty answer.");
        }

        return ScopedQuestionBlockReadResult.Success(
            new ScopedQuestionBlock(
                questionId,
                answerInstanceId,
                startLine,
                endLine));
    }

    private sealed record ScopedQuestionBlockReadResult(
        ScopedQuestionBlock? Block,
        string? Error)
    {
        public bool IsSuccess => Block is not null && Error is null;
        public static ScopedQuestionBlockReadResult Success(ScopedQuestionBlock block) =>
            new(block, null);
        public static ScopedQuestionBlockReadResult Failure(string error) =>
            new(null, error);
    }

    private static AnswerInstanceBlockReadResult ReadAnswerInstanceBlock(
        IReadOnlyList<string> lines,
        int startLine,
        string questionId,
        string answerInstanceId,
        string? parentAnswerInstanceId)
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
                parentAnswerInstanceId,
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
