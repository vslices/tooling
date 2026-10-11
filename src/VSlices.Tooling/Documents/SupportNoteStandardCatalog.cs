using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record SupportNoteQuestionDefinition(
    string Id,
    string Text,
    IReadOnlyList<SupportNoteQuestionDefinition> Children);

internal sealed record SupportNoteDefinition(
    string Type,
    SupportNoteQuestionDefinition RootQuestion);

internal sealed record SupportNoteStandardCatalogResult(
    SupportNoteStandardCatalog? Catalog,
    string? Error)
{
    public bool IsSuccess => Catalog is not null && Error is null;

    public static SupportNoteStandardCatalogResult Success(SupportNoteStandardCatalog catalog) =>
        new(catalog, null);

    public static SupportNoteStandardCatalogResult Failure(string error) =>
        new(null, error);
}

internal sealed class SupportNoteStandardCatalog
{
    private readonly IReadOnlyDictionary<string, SupportNoteDefinition> supportNotes;

    private SupportNoteStandardCatalog(IReadOnlyDictionary<string, SupportNoteDefinition> supportNotes)
    {
        this.supportNotes = supportNotes;
    }

    public IEnumerable<SupportNoteDefinition> SupportNotes => supportNotes.Values;

    public bool TryGetSupportNote(string type, out SupportNoteDefinition? definition) =>
        supportNotes.TryGetValue(type, out definition);

    public static SupportNoteStandardCatalogResult Load(string root)
    {
        try
        {
            var manifestPath = Path.Combine(root, "manifest.yaml");
            if (!File.Exists(manifestPath))
                return SupportNoteStandardCatalogResult.Failure(
                    $"SUPSTD001: Installed Docs Standard at '{root}' does not contain manifest.yaml.");

            using var reader = File.OpenText(manifestPath);
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode manifest)
                return SupportNoteStandardCatalogResult.Failure(
                    "SUPSTD002: Docs Standard manifest must contain exactly one YAML mapping document.");

            if (!manifest.Children.TryGetValue(new YamlScalarNode("support-notes"), out var supportNotesNode))
                return SupportNoteStandardCatalogResult.Success(
                    new SupportNoteStandardCatalog(
                        new Dictionary<string, SupportNoteDefinition>(StringComparer.OrdinalIgnoreCase)));

            if (supportNotesNode is not YamlSequenceNode paths)
                return SupportNoteStandardCatalogResult.Failure(
                    "SUPSTD003: Docs Standard manifest support-notes must be a sequence.");

            var loaded = new Dictionary<string, SupportNoteDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in paths.Children)
            {
                if (entry is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                    return SupportNoteStandardCatalogResult.Failure(
                        "SUPSTD004: Every support-note manifest entry must be a non-empty relative path.");

                var definitionPath = ResolveContainedPath(root, scalar.Value!);
                if (definitionPath is null)
                    return SupportNoteStandardCatalogResult.Failure(
                        $"SUPSTD005: Support Note definition path '{scalar.Value}' escapes the installed Docs Standard root.");

                if (!File.Exists(definitionPath))
                    return SupportNoteStandardCatalogResult.Failure(
                        $"SUPSTD006: Support Note definition '{scalar.Value}' does not exist in the installed Docs Standard.");

                var parsed = ParseDefinition(definitionPath);
                if (!parsed.IsSuccess)
                    return SupportNoteStandardCatalogResult.Failure(parsed.Error!);

                var definition = parsed.Definition!;
                if (!loaded.TryAdd(definition.Type, definition))
                    return SupportNoteStandardCatalogResult.Failure(
                        $"SUPSTD007: Docs Standard declares duplicate Support Note type '{definition.Type}'.");
            }

            return SupportNoteStandardCatalogResult.Success(new SupportNoteStandardCatalog(loaded));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or YamlDotNet.Core.YamlException or ArgumentException)
        {
            return SupportNoteStandardCatalogResult.Failure(
                $"SUPSTD099: Could not load Support Note definitions: {ex.Message}");
        }
    }

    private static SupportNoteDefinitionParseResult ParseDefinition(string path)
    {
        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            return SupportNoteDefinitionParseResult.Failure(
                $"SUPSTD008: Support Note definition '{path}' must contain exactly one YAML mapping document.");

        var unknownRoot = FirstUnknownKey(root, "kind", "version", "support-note");
        if (unknownRoot is not null)
            return SupportNoteDefinitionParseResult.Failure(
                $"SUPSTD009: Support Note definition contains unknown key '{unknownRoot}'.");

        if (!TryRequiredScalar(root, "kind", out var kind) ||
            !kind.Equals("vslices-support-note-definition", StringComparison.Ordinal))
        {
            return SupportNoteDefinitionParseResult.Failure(
                "SUPSTD010: Support Note definition kind must be 'vslices-support-note-definition'.");
        }

        if (!TryRequiredScalar(root, "version", out var version) ||
            !version.Equals("0.1", StringComparison.Ordinal))
        {
            return SupportNoteDefinitionParseResult.Failure(
                "SUPSTD011: Support Note definition version must currently be '0.1'.");
        }

        if (!root.Children.TryGetValue(new YamlScalarNode("support-note"), out var noteNode) ||
            noteNode is not YamlMappingNode note)
        {
            return SupportNoteDefinitionParseResult.Failure(
                "SUPSTD012: Support Note definition must contain a support-note mapping.");
        }

        var unknownNote = FirstUnknownKey(note, "type", "question");
        if (unknownNote is not null)
            return SupportNoteDefinitionParseResult.Failure(
                $"SUPSTD013: Support Note definition contains unknown support-note key '{unknownNote}'.");

        if (!TryRequiredScalar(note, "type", out var type) || !IsStableIdentifier(type))
            return SupportNoteDefinitionParseResult.Failure(
                "SUPSTD014: support-note.type must be a stable identifier beginning with a letter and containing only letters, digits, '-' or '_'.");

        if (!note.Children.TryGetValue(new YamlScalarNode("question"), out var questionNode))
            return SupportNoteDefinitionParseResult.Failure(
                $"SUPSTD015: Support Note type '{type}' must declare a root question.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var question = ParseQuestion(questionNode, type, ids);
        if (!question.IsSuccess)
            return SupportNoteDefinitionParseResult.Failure(question.Error!);

        if (CountQuestions(question.Question!) > 2)
            return SupportNoteDefinitionParseResult.Failure(
                $"SUPSTD020: Support Note type '{type}' declares more than two semantic questions.");

        return SupportNoteDefinitionParseResult.Success(
            new SupportNoteDefinition(type, question.Question!));
    }

    private static SupportNoteQuestionParseResult ParseQuestion(
        YamlNode node,
        string type,
        HashSet<string> ids)
    {
        if (node is not YamlMappingNode mapping)
            return SupportNoteQuestionParseResult.Failure(
                $"SUPSTD016: A question in Support Note type '{type}' must be a mapping.");

        var unknown = FirstUnknownKey(mapping, "id", "text", "children");
        if (unknown is not null)
            return SupportNoteQuestionParseResult.Failure(
                $"SUPSTD017: Question in Support Note type '{type}' contains unknown key '{unknown}'.");

        if (!TryRequiredScalar(mapping, "id", out var id) || !IsStableIdentifier(id))
            return SupportNoteQuestionParseResult.Failure(
                $"SUPSTD018: Question id in Support Note type '{type}' must be a stable identifier.");

        if (!ids.Add(id))
            return SupportNoteQuestionParseResult.Failure(
                $"SUPSTD019: Support Note type '{type}' declares duplicate question id '{id}'.");

        if (!TryRequiredScalar(mapping, "text", out var text))
            return SupportNoteQuestionParseResult.Failure(
                $"SUPSTD021: Question '{id}' in Support Note type '{type}' must declare non-empty text.");

        var children = new List<SupportNoteQuestionDefinition>();
        if (mapping.Children.TryGetValue(new YamlScalarNode("children"), out var childrenNode))
        {
            if (childrenNode is not YamlSequenceNode sequence)
                return SupportNoteQuestionParseResult.Failure(
                    $"SUPSTD022: children for Support Note question '{id}' must be a sequence.");

            foreach (var childNode in sequence.Children)
            {
                var child = ParseQuestion(childNode, type, ids);
                if (!child.IsSuccess)
                    return child;

                children.Add(child.Question!);
            }
        }

        return SupportNoteQuestionParseResult.Success(
            new SupportNoteQuestionDefinition(id, text, children));
    }

    private static int CountQuestions(SupportNoteQuestionDefinition question) =>
        1 + question.Children.Sum(CountQuestions);

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
        {
            return null;
        }

        return full;
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

    private static bool TryRequiredScalar(
        YamlMappingNode mapping,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) ||
            node is not YamlScalarNode scalar ||
            string.IsNullOrWhiteSpace(scalar.Value))
        {
            return false;
        }

        value = scalar.Value.Trim();
        return true;
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

    private sealed record SupportNoteDefinitionParseResult(SupportNoteDefinition? Definition, string? Error)
    {
        public bool IsSuccess => Definition is not null && Error is null;
        public static SupportNoteDefinitionParseResult Success(SupportNoteDefinition definition) => new(definition, null);
        public static SupportNoteDefinitionParseResult Failure(string error) => new(null, error);
    }

    private sealed record SupportNoteQuestionParseResult(SupportNoteQuestionDefinition? Question, string? Error)
    {
        public bool IsSuccess => Question is not null && Error is null;
        public static SupportNoteQuestionParseResult Success(SupportNoteQuestionDefinition question) => new(question, null);
        public static SupportNoteQuestionParseResult Failure(string error) => new(null, error);
    }
}
