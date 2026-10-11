using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record QuestionPresentationDefinition(
    string Kind,
    string TextSource,
    string LevelStrategy,
    int RootLevel);

internal sealed record MaterializationTemplateDefinition(
    string Id,
    string ArtifactKind,
    string MediaType,
    string RelativePath,
    QuestionPresentationDefinition? QuestionPresentation,
    string? AnswerRegionStart,
    string? AnswerRegionEnd,
    string? EmptyAnswerState,
    string? RootQuestionIdentityStrategy,
    string? MultipleAnswerStrategy,
    string? AnswerInstanceStrategy,
    string? ScopedChildStrategy,
    string? ScopedQuestionStrategy,
    string? RepeatedAnswerScopeStrategy,
    string? AnswerInstanceScopeStrategy);

internal sealed record TemplateStandardCatalogResult(
    TemplateStandardCatalog? Catalog,
    string? Error)
{
    public bool IsSuccess => Catalog is not null && Error is null;
    public static TemplateStandardCatalogResult Success(TemplateStandardCatalog catalog) => new(catalog, null);
    public static TemplateStandardCatalogResult Failure(string error) => new(null, error);
}

internal sealed class TemplateStandardCatalog
{
    private readonly IReadOnlyDictionary<string, MaterializationTemplateDefinition> templates;

    private TemplateStandardCatalog(IReadOnlyDictionary<string, MaterializationTemplateDefinition> templates) =>
        this.templates = templates;

    public IEnumerable<MaterializationTemplateDefinition> Templates => templates.Values;

    public bool TryGetTemplate(string id, out MaterializationTemplateDefinition? definition) =>
        templates.TryGetValue(id, out definition);

    public static TemplateStandardCatalogResult Load(string root)
    {
        try
        {
            var manifestPath = Path.Combine(root, "manifest.yaml");
            if (!File.Exists(manifestPath))
                return TemplateStandardCatalogResult.Failure($"TMPL001: Installed Template Standard at '{root}' does not contain manifest.yaml.");

            var manifest = LoadMapping(manifestPath, "TMPL002", "Template Standard manifest");
            if (!manifest.IsSuccess)
                return TemplateStandardCatalogResult.Failure(manifest.Error!);

            var manifestRoot = manifest.Mapping!;
            var unknown = FirstUnknownKey(manifestRoot, "kind", "version", "templates");
            if (unknown is not null)
                return TemplateStandardCatalogResult.Failure($"TMPL003: Template Standard manifest contains unknown key '{unknown}'.");

            if (!TryRequiredScalar(manifestRoot, "kind", out var kind) ||
                !kind.Equals("vslices-template-standard", StringComparison.Ordinal))
                return TemplateStandardCatalogResult.Failure("TMPL004: Template Standard manifest kind must be 'vslices-template-standard'.");

            if (!TryRequiredScalar(manifestRoot, "version", out var version) ||
                !version.Equals("0.1", StringComparison.Ordinal))
                return TemplateStandardCatalogResult.Failure("TMPL005: Template Standard manifest version must currently be '0.1'.");

            if (!manifestRoot.Children.TryGetValue(new YamlScalarNode("templates"), out var node) ||
                node is not YamlSequenceNode paths ||
                paths.Children.Count == 0)
                return TemplateStandardCatalogResult.Failure("TMPL006: Template Standard manifest must declare at least one materialization template.");

            var loaded = new Dictionary<string, MaterializationTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in paths.Children)
            {
                if (entry is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                    return TemplateStandardCatalogResult.Failure("TMPL007: Every Template Standard entry must be a non-empty relative path.");

                var relativePath = scalar.Value.Trim();
                var templatePath = ResolveContainedPath(root, relativePath);
                if (templatePath is null)
                    return TemplateStandardCatalogResult.Failure($"TMPL008: Template path '{relativePath}' escapes the installed Template Standard root.");
                if (!File.Exists(templatePath))
                    return TemplateStandardCatalogResult.Failure($"TMPL009: Materialization template '{relativePath}' does not exist in the installed Template Standard.");

                var parsed = ParseTemplateDefinition(templatePath, relativePath);
                if (!parsed.IsSuccess)
                    return TemplateStandardCatalogResult.Failure(parsed.Error!);
                if (!loaded.TryAdd(parsed.Definition!.Id, parsed.Definition))
                    return TemplateStandardCatalogResult.Failure($"TMPL010: Template Standard declares duplicate template id '{parsed.Definition.Id}'.");
            }

            return TemplateStandardCatalogResult.Success(new TemplateStandardCatalog(loaded));
        }
        catch (Exception ex)
        {
            return TemplateStandardCatalogResult.Failure($"TMPL099: Could not load installed Template Standard: {ex.Message}");
        }
    }

    private static TemplateDefinitionParseResult ParseTemplateDefinition(string path, string relativePath)
    {
        var loaded = LoadMapping(path, "TMPL011", $"Materialization template '{relativePath}'");
        if (!loaded.IsSuccess)
            return TemplateDefinitionParseResult.Failure(loaded.Error!);

        var root = loaded.Mapping!;
        if (!TryRequiredScalar(root, "kind", out var kind) ||
            !kind.Equals("vslices-materialization-template", StringComparison.Ordinal))
            return TemplateDefinitionParseResult.Failure($"TMPL012: Materialization template '{relativePath}' kind must be 'vslices-materialization-template'.");

        if (!TryRequiredScalar(root, "version", out var version) ||
            !version.Equals("0.1", StringComparison.Ordinal))
            return TemplateDefinitionParseResult.Failure($"TMPL013: Materialization template '{relativePath}' version must currently be '0.1'.");

        if (!root.Children.TryGetValue(new YamlScalarNode("template"), out var templateNode) ||
            templateNode is not YamlMappingNode template)
            return TemplateDefinitionParseResult.Failure($"TMPL014: Materialization template '{relativePath}' must contain a template mapping.");

        if (!TryRequiredScalar(template, "id", out var id) || !IsTemplateIdentifier(id))
            return TemplateDefinitionParseResult.Failure($"TMPL015: Materialization template '{relativePath}' must declare a stable template.id.");
        if (!TryRequiredScalar(template, "artifact-kind", out var artifactKind))
            return TemplateDefinitionParseResult.Failure($"TMPL016: Materialization template '{id}' must declare template.artifact-kind.");
        if (!TryRequiredScalar(template, "media-type", out var mediaType))
            return TemplateDefinitionParseResult.Failure($"TMPL017: Materialization template '{id}' must declare template.media-type.");

        var questionPresentation = ParseQuestionPresentation(root, id);
        if (!questionPresentation.IsSuccess)
            return TemplateDefinitionParseResult.Failure(questionPresentation.Error!);

        var answerRegionStart = ParseAnswerRegionBoundary(root, id, "starts", "TMPL026");
        if (!answerRegionStart.IsSuccess)
            return TemplateDefinitionParseResult.Failure(answerRegionStart.Error!);

        var answerRegionEnd = ParseAnswerRegionBoundary(root, id, "ends", "TMPL027");
        if (!answerRegionEnd.IsSuccess)
            return TemplateDefinitionParseResult.Failure(answerRegionEnd.Error!);

        var emptyAnswerState = ParseEmptyAnswerState(root, id);
        if (!emptyAnswerState.IsSuccess)
            return TemplateDefinitionParseResult.Failure(emptyAnswerState.Error!);

        var rootIdentityStrategy = ParseRootQuestionIdentityStrategy(root, id);
        if (!rootIdentityStrategy.IsSuccess)
            return TemplateDefinitionParseResult.Failure(rootIdentityStrategy.Error!);

        var multipleAnswerStrategy = ParseMultipleAnswerStrategy(root, id);
        if (!multipleAnswerStrategy.IsSuccess)
            return TemplateDefinitionParseResult.Failure(multipleAnswerStrategy.Error!);

        var answerInstanceStrategy = ParseAnswerInstanceStrategy(root, id);
        if (!answerInstanceStrategy.IsSuccess)
            return TemplateDefinitionParseResult.Failure(answerInstanceStrategy.Error!);

        var scopedChildStrategy = ParseScopedChildStrategy(root, id);
        if (!scopedChildStrategy.IsSuccess)
            return TemplateDefinitionParseResult.Failure(scopedChildStrategy.Error!);

        var scopedQuestionStrategy = ParseScopedQuestionStrategy(root, id);
        if (!scopedQuestionStrategy.IsSuccess)
            return TemplateDefinitionParseResult.Failure(scopedQuestionStrategy.Error!);

        var repeatedAnswerScopeStrategy = ParseRepeatedAnswerScopeStrategy(root, id);
        if (!repeatedAnswerScopeStrategy.IsSuccess)
            return TemplateDefinitionParseResult.Failure(repeatedAnswerScopeStrategy.Error!);

        var answerInstanceScopeStrategy = ParseAnswerInstanceScopeStrategy(root, id);
        if (!answerInstanceScopeStrategy.IsSuccess)
            return TemplateDefinitionParseResult.Failure(answerInstanceScopeStrategy.Error!);

        return TemplateDefinitionParseResult.Success(
            new MaterializationTemplateDefinition(
                id,
                artifactKind,
                mediaType,
                relativePath,
                questionPresentation.Definition,
                answerRegionStart.Value,
                answerRegionEnd.Value,
                emptyAnswerState.Value,
                rootIdentityStrategy.Value,
                multipleAnswerStrategy.Value,
                answerInstanceStrategy.Value,
                scopedChildStrategy.Value,
                scopedQuestionStrategy.Value,
                repeatedAnswerScopeStrategy.Value,
                answerInstanceScopeStrategy.Value));
    }

    private static QuestionPresentationParseResult ParseQuestionPresentation(
        YamlMappingNode root,
        string templateId)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode("representation"), out var representationNode))
            return QuestionPresentationParseResult.Success(null);

        if (representationNode is not YamlMappingNode representation)
            return QuestionPresentationParseResult.Failure(
                $"TMPL018: Materialization template '{templateId}' representation must be a mapping.");

        if (!representation.Children.TryGetValue(new YamlScalarNode("question"), out var questionNode))
            return QuestionPresentationParseResult.Success(null);

        if (questionNode is not YamlMappingNode question)
            return QuestionPresentationParseResult.Failure(
                $"TMPL019: Materialization template '{templateId}' representation.question must be a mapping.");

        if (!question.Children.TryGetValue(new YamlScalarNode("presentation"), out var presentationNode))
            return QuestionPresentationParseResult.Success(null);

        if (presentationNode is not YamlMappingNode presentation)
            return QuestionPresentationParseResult.Failure(
                $"TMPL020: Materialization template '{templateId}' representation.question.presentation must be a mapping.");

        if (!TryRequiredScalar(presentation, "kind", out var kind))
            return QuestionPresentationParseResult.Failure(
                $"TMPL021: Materialization template '{templateId}' question presentation must declare kind.");

        if (!presentation.Children.TryGetValue(new YamlScalarNode("text"), out var textNode) ||
            textNode is not YamlMappingNode text ||
            !TryRequiredScalar(text, "source", out var textSource))
        {
            return QuestionPresentationParseResult.Failure(
                $"TMPL022: Materialization template '{templateId}' question presentation must declare text.source.");
        }

        if (!presentation.Children.TryGetValue(new YamlScalarNode("level"), out var levelNode) ||
            levelNode is not YamlMappingNode level ||
            !TryRequiredScalar(level, "strategy", out var levelStrategy) ||
            !TryRequiredScalar(level, "root", out var rootLevelText) ||
            !int.TryParse(rootLevelText, out var rootLevel))
        {
            return QuestionPresentationParseResult.Failure(
                $"TMPL023: Materialization template '{templateId}' question presentation must declare level.strategy and numeric level.root.");
        }

        return QuestionPresentationParseResult.Success(
            new QuestionPresentationDefinition(
                kind,
                textSource,
                levelStrategy,
                rootLevel));
    }

    private static OptionalScalarParseResult ParseAnswerRegionBoundary(
        YamlMappingNode root,
        string templateId,
        string key,
        string diagnosticCode)
    {
        if (!TryNestedMapping(root, out var region, "representation", "question", "answer", "region"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(region, key, out var value))
        {
            return OptionalScalarParseResult.Failure(
                $"{diagnosticCode}: Materialization template '{templateId}' question answer region must declare region.{key}.");
        }

        return OptionalScalarParseResult.Success(value);
    }

    private static OptionalScalarParseResult ParseEmptyAnswerState(
        YamlMappingNode root,
        string templateId)
    {
        if (!TryNestedMapping(root, out var answer, "representation", "question", "answer"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(answer, "empty", out var empty))
        {
            return OptionalScalarParseResult.Failure(
                $"TMPL024: Materialization template '{templateId}' question answer contract must declare answer.empty.");
        }

        return OptionalScalarParseResult.Success(empty);
    }

    private static OptionalScalarParseResult ParseMultipleAnswerStrategy(
        YamlMappingNode root,
        string templateId)
    {
        if (!TryNestedMapping(root, out var multiple, "representation", "question", "answer", "multiple"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(multiple, "strategy", out var strategy))
        {
            return OptionalScalarParseResult.Failure(
                $"TMPL028: Materialization template '{templateId}' multiple-answer contract must declare strategy.");
        }

        return OptionalScalarParseResult.Success(strategy);
    }

    private static OptionalScalarParseResult ParseRepeatedAnswerScopeStrategy(
        YamlMappingNode root,
        string templateId)
    {
        if (!TryNestedMapping(root, out var scoped, "representation", "question", "answer", "multiple", "scoped"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(scoped, "strategy", out var strategy))
        {
            return OptionalScalarParseResult.Failure(
                $"TMPL032: Materialization template '{templateId}' repeated-answer scoped contract must declare strategy.");
        }

        return OptionalScalarParseResult.Success(strategy);
    }

    private static OptionalScalarParseResult ParseAnswerInstanceScopeStrategy(
        YamlMappingNode root,
        string templateId)
    {
        if (!TryNestedMapping(root, out var scope, "reconstruction", "answer-instance", "scope"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(scope, "strategy", out var strategy))
        {
            return OptionalScalarParseResult.Failure(
                $"TMPL033: Materialization template '{templateId}' answer-instance scope contract must declare strategy.");
        }

        return OptionalScalarParseResult.Success(strategy);
    }

    private static OptionalScalarParseResult ParseScopedChildStrategy(
        YamlMappingNode root,
        string templateId)
    {
        if (!TryNestedMapping(root, out var scopedChild, "representation", "question", "scoped-child"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(scopedChild, "strategy", out var strategy))
        {
            return OptionalScalarParseResult.Failure(
                $"TMPL030: Materialization template '{templateId}' scoped-child contract must declare strategy.");
        }

        return OptionalScalarParseResult.Success(strategy);
    }

    private static OptionalScalarParseResult ParseScopedQuestionStrategy(
        YamlMappingNode root,
        string templateId)
    {
        if (!TryNestedMapping(root, out var scopedQuestion, "reconstruction", "scoped-question"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(scopedQuestion, "strategy", out var strategy))
        {
            return OptionalScalarParseResult.Failure(
                $"TMPL031: Materialization template '{templateId}' scoped-question reconstruction must declare strategy.");
        }

        return OptionalScalarParseResult.Success(strategy);
    }

    private static OptionalScalarParseResult ParseAnswerInstanceStrategy(
        YamlMappingNode root,
        string templateId)
    {
        if (!TryNestedMapping(root, out var answerInstance, "reconstruction", "answer-instance"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(answerInstance, "strategy", out var strategy))
        {
            return OptionalScalarParseResult.Failure(
                $"TMPL029: Materialization template '{templateId}' answer-instance reconstruction must declare strategy.");
        }

        return OptionalScalarParseResult.Success(strategy);
    }

    private static OptionalScalarParseResult ParseRootQuestionIdentityStrategy(
        YamlMappingNode root,
        string templateId)
    {
        if (!TryNestedMapping(root, out var rootIdentity, "reconstruction", "question-identity", "root"))
            return OptionalScalarParseResult.Success(null);

        if (!TryRequiredScalar(rootIdentity, "strategy", out var strategy))
        {
            return OptionalScalarParseResult.Failure(
                $"TMPL025: Materialization template '{templateId}' root question reconstruction must declare strategy.");
        }

        return OptionalScalarParseResult.Success(strategy);
    }

    private static bool TryNestedMapping(
        YamlMappingNode root,
        out YamlMappingNode mapping,
        params string[] path)
    {
        mapping = root;
        foreach (var segment in path)
        {
            if (!mapping.Children.TryGetValue(new YamlScalarNode(segment), out var node) ||
                node is not YamlMappingNode child)
            {
                mapping = null!;
                return false;
            }

            mapping = child;
        }

        return true;
    }

    private static MappingLoadResult LoadMapping(string path, string code, string subject)
    {
        try
        {
            using var reader = File.OpenText(path);
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
                return MappingLoadResult.Failure($"{code}: {subject} must contain exactly one YAML mapping document.");
            return MappingLoadResult.Success(mapping);
        }
        catch (Exception ex)
        {
            return MappingLoadResult.Failure($"{code}: Could not parse {subject}: {ex.Message}");
        }
    }

    private static string? FirstUnknownKey(YamlMappingNode mapping, params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                return "<non-scalar>";
            if (!set.Contains(scalar.Value))
                return scalar.Value;
        }
        return null;
    }

    private static bool TryRequiredScalar(YamlMappingNode mapping, string key, out string value)
    {
        value = string.Empty;
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) ||
            node is not YamlScalarNode scalar ||
            string.IsNullOrWhiteSpace(scalar.Value))
            return false;
        value = scalar.Value.Trim();
        return true;
    }

    private static string? ResolveContainedPath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return null;
        var rootFull = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(rootFull, relativePath));
        var relative = Path.GetRelativePath(rootFull, full);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            return null;
        return full;
    }

    private static bool IsTemplateIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !char.IsLetter(value[0]))
            return false;
        for (var i = 1; i < value.Length; i++)
        {
            var ch = value[i];
            if (!char.IsLetterOrDigit(ch) && ch is not '-' and not '_' and not '.')
                return false;
        }
        return true;
    }

    private sealed record MappingLoadResult(YamlMappingNode? Mapping, string? Error)
    {
        public bool IsSuccess => Mapping is not null && Error is null;
        public static MappingLoadResult Success(YamlMappingNode mapping) => new(mapping, null);
        public static MappingLoadResult Failure(string error) => new(null, error);
    }

    private sealed record TemplateDefinitionParseResult(MaterializationTemplateDefinition? Definition, string? Error)
    {
        public bool IsSuccess => Definition is not null && Error is null;
        public static TemplateDefinitionParseResult Success(MaterializationTemplateDefinition definition) => new(definition, null);
        public static TemplateDefinitionParseResult Failure(string error) => new(null, error);
    }

    private sealed record QuestionPresentationParseResult(
        QuestionPresentationDefinition? Definition,
        string? Error)
    {
        public bool IsSuccess => Error is null;

        public static QuestionPresentationParseResult Success(
            QuestionPresentationDefinition? definition) =>
            new(definition, null);

        public static QuestionPresentationParseResult Failure(string error) =>
            new(null, error);
    }

    private sealed record OptionalScalarParseResult(
        string? Value,
        string? Error)
    {
        public bool IsSuccess => Error is null;
        public static OptionalScalarParseResult Success(string? value) => new(value, null);
        public static OptionalScalarParseResult Failure(string error) => new(null, error);
    }
}
