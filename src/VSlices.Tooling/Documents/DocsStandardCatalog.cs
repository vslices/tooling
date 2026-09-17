using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record DocumentQuestionDefinition(
    string Id,
    string Text,
    IReadOnlyList<DocumentQuestionDefinition> Children);

internal sealed record DocumentDefinition(
    string Type,
    IReadOnlyList<string> Scopes,
    DocumentQuestionDefinition RootQuestion);

internal sealed record DocsStandardCatalogResult(
    DocsStandardCatalog? Catalog,
    string? Error)
{
    public bool IsSuccess => Catalog is not null && Error is null;

    public static DocsStandardCatalogResult Success(DocsStandardCatalog catalog) =>
        new(catalog, null);

    public static DocsStandardCatalogResult Failure(string error) =>
        new(null, error);
}

internal sealed class DocsStandardCatalog
{
    private readonly IReadOnlyDictionary<string, DocumentDefinition> documents;

    private DocsStandardCatalog(IReadOnlyDictionary<string, DocumentDefinition> documents)
    {
        this.documents = documents;
    }

    public IReadOnlyCollection<DocumentDefinition> Documents => documents.Values;

    public bool TryGetDocument(string kind, out DocumentDefinition? definition) =>
        documents.TryGetValue(kind, out definition);

    public static string? FindInstalledRoot(string start)
    {
        var fullPath = Path.GetFullPath(start);
        var current = File.Exists(fullPath)
            ? new DirectoryInfo(Path.GetDirectoryName(fullPath)!)
            : new DirectoryInfo(fullPath);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".vslices", "docs-standard");
            if (File.Exists(Path.Combine(candidate, "manifest.yaml")))
                return candidate;

            current = current.Parent;
        }

        return null;
    }

    public static DocsStandardCatalogResult Load(string root)
    {
        try
        {
            var manifestPath = Path.Combine(root, "manifest.yaml");
            if (!File.Exists(manifestPath))
            {
                return DocsStandardCatalogResult.Failure(
                    $"DOCS001: Installed Docs Standard at '{root}' does not contain manifest.yaml.");
            }

            var manifest = LoadMapping(manifestPath, "DOCS002", "Docs Standard manifest");
            if (!manifest.IsSuccess)
                return DocsStandardCatalogResult.Failure(manifest.Error!);

            var manifestRoot = manifest.Mapping!;
            var unknownManifestKey = FirstUnknownKey(manifestRoot, "kind", "version", "documents");
            if (unknownManifestKey is not null)
            {
                return DocsStandardCatalogResult.Failure(
                    $"DOCS003: Docs Standard manifest contains unknown key '{unknownManifestKey}'.");
            }

            if (!TryRequiredScalar(manifestRoot, "kind", out var manifestKind) ||
                !manifestKind.Equals("vslices-docs-standard", StringComparison.Ordinal))
            {
                return DocsStandardCatalogResult.Failure(
                    "DOCS004: Docs Standard manifest kind must be 'vslices-docs-standard'.");
            }

            if (!TryRequiredScalar(manifestRoot, "version", out var manifestVersion) ||
                !manifestVersion.Equals("0.1", StringComparison.Ordinal))
            {
                return DocsStandardCatalogResult.Failure(
                    "DOCS005: Docs Standard manifest version must currently be '0.1'.");
            }

            if (!manifestRoot.Children.TryGetValue(new YamlScalarNode("documents"), out var documentsNode) ||
                documentsNode is not YamlSequenceNode documentPaths ||
                documentPaths.Children.Count == 0)
            {
                return DocsStandardCatalogResult.Failure(
                    "DOCS006: Docs Standard manifest must declare at least one document definition.");
            }

            var loaded = new Dictionary<string, DocumentDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in documentPaths.Children)
            {
                if (entry is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                {
                    return DocsStandardCatalogResult.Failure(
                        "DOCS007: Every Docs Standard document entry must be a non-empty relative path.");
                }

                var definitionPath = ResolveContainedPath(root, scalar.Value!);
                if (definitionPath is null)
                {
                    return DocsStandardCatalogResult.Failure(
                        $"DOCS008: Document definition path '{scalar.Value}' escapes the installed Docs Standard root.");
                }

                if (!File.Exists(definitionPath))
                {
                    return DocsStandardCatalogResult.Failure(
                        $"DOCS009: Document definition '{scalar.Value}' does not exist in the installed Docs Standard.");
                }

                var parsed = ParseDocumentDefinition(definitionPath);
                if (!parsed.IsSuccess)
                    return DocsStandardCatalogResult.Failure(parsed.Error!);

                var definition = parsed.Definition!;
                if (!loaded.TryAdd(definition.Type, definition))
                {
                    return DocsStandardCatalogResult.Failure(
                        $"DOCS010: Docs Standard declares duplicate document type '{definition.Type}'.");
                }
            }

            return DocsStandardCatalogResult.Success(new DocsStandardCatalog(loaded));
        }
        catch (Exception ex)
        {
            return DocsStandardCatalogResult.Failure(
                $"DOCS099: Could not load installed Docs Standard: {ex.Message}");
        }
    }

    private static DocumentDefinitionParseResult ParseDocumentDefinition(string path)
    {
        var loaded = LoadMapping(path, "DOCS011", $"Document definition '{path}'");
        if (!loaded.IsSuccess)
            return DocumentDefinitionParseResult.Failure(loaded.Error!);

        var root = loaded.Mapping!;
        var unknownRootKey = FirstUnknownKey(root, "kind", "version", "document");
        if (unknownRootKey is not null)
        {
            return DocumentDefinitionParseResult.Failure(
                $"DOCS012: Document definition contains unknown key '{unknownRootKey}'.");
        }

        if (!TryRequiredScalar(root, "kind", out var kind) ||
            !kind.Equals("vslices-document-definition", StringComparison.Ordinal))
        {
            return DocumentDefinitionParseResult.Failure(
                "DOCS013: Document definition kind must be 'vslices-document-definition'.");
        }

        if (!TryRequiredScalar(root, "version", out var version) ||
            !version.Equals("0.1", StringComparison.Ordinal))
        {
            return DocumentDefinitionParseResult.Failure(
                "DOCS014: Document definition version must currently be '0.1'.");
        }

        if (!root.Children.TryGetValue(new YamlScalarNode("document"), out var documentNode) ||
            documentNode is not YamlMappingNode document)
        {
            return DocumentDefinitionParseResult.Failure(
                "DOCS015: Document definition must contain a document mapping.");
        }

        var unknownDocumentKey = FirstUnknownKey(document, "type", "scopes", "question");
        if (unknownDocumentKey is not null)
        {
            return DocumentDefinitionParseResult.Failure(
                $"DOCS016: Document definition contains unknown document key '{unknownDocumentKey}'.");
        }

        if (!TryRequiredScalar(document, "type", out var type) || !IsStableIdentifier(type))
        {
            return DocumentDefinitionParseResult.Failure(
                "DOCS017: document.type must be a stable identifier beginning with a letter and containing only letters, digits, '-' or '_'.");
        }

        if (!document.Children.TryGetValue(new YamlScalarNode("scopes"), out var scopesNode) ||
            scopesNode is not YamlSequenceNode scopesSequence)
        {
            return DocumentDefinitionParseResult.Failure(
                $"DOCS018: Document type '{type}' must declare scopes as a sequence.");
        }

        var scopes = new List<string>();
        foreach (var scopeNode in scopesSequence.Children)
        {
            if (scopeNode is not YamlScalarNode scopeScalar || string.IsNullOrWhiteSpace(scopeScalar.Value))
            {
                return DocumentDefinitionParseResult.Failure(
                    $"DOCS019: Document type '{type}' contains a non-scalar or empty scope.");
            }

            scopes.Add(scopeScalar.Value.Trim());
        }

        if (!document.Children.TryGetValue(new YamlScalarNode("question"), out var questionNode))
        {
            return DocumentDefinitionParseResult.Failure(
                $"DOCS020: Document type '{type}' must declare a root question.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var question = ParseQuestion(questionNode, type, ids);
        if (!question.IsSuccess)
            return DocumentDefinitionParseResult.Failure(question.Error!);

        return DocumentDefinitionParseResult.Success(
            new DocumentDefinition(type, scopes, question.Question!));
    }

    private static DocumentQuestionParseResult ParseQuestion(
        YamlNode node,
        string documentType,
        HashSet<string> ids)
    {
        if (node is not YamlMappingNode mapping)
        {
            return DocumentQuestionParseResult.Failure(
                $"DOCS021: A question in document type '{documentType}' must be a mapping.");
        }

        var unknownKey = FirstUnknownKey(mapping, "id", "text", "children");
        if (unknownKey is not null)
        {
            return DocumentQuestionParseResult.Failure(
                $"DOCS022: Question in document type '{documentType}' contains unknown key '{unknownKey}'.");
        }

        if (!TryRequiredScalar(mapping, "id", out var id) || !IsStableIdentifier(id))
        {
            return DocumentQuestionParseResult.Failure(
                $"DOCS023: Question id in document type '{documentType}' must be a stable identifier.");
        }

        if (!ids.Add(id))
        {
            return DocumentQuestionParseResult.Failure(
                $"DOCS024: Document type '{documentType}' declares duplicate question id '{id}'.");
        }

        if (!TryRequiredScalar(mapping, "text", out var text))
        {
            return DocumentQuestionParseResult.Failure(
                $"DOCS025: Question '{id}' in document type '{documentType}' must declare non-empty text.");
        }

        var children = new List<DocumentQuestionDefinition>();
        if (mapping.Children.TryGetValue(new YamlScalarNode("children"), out var childrenNode))
        {
            if (childrenNode is not YamlSequenceNode sequence)
            {
                return DocumentQuestionParseResult.Failure(
                    $"DOCS026: children for question '{id}' must be a sequence.");
            }

            foreach (var childNode in sequence.Children)
            {
                var child = ParseQuestion(childNode, documentType, ids);
                if (!child.IsSuccess)
                    return child;

                children.Add(child.Question!);
            }
        }

        return DocumentQuestionParseResult.Success(
            new DocumentQuestionDefinition(id, text, children));
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

    private sealed record MappingLoadResult(YamlMappingNode? Mapping, string? Error)
    {
        public bool IsSuccess => Mapping is not null && Error is null;
        public static MappingLoadResult Success(YamlMappingNode mapping) => new(mapping, null);
        public static MappingLoadResult Failure(string error) => new(null, error);
    }

    private sealed record DocumentDefinitionParseResult(DocumentDefinition? Definition, string? Error)
    {
        public bool IsSuccess => Definition is not null && Error is null;
        public static DocumentDefinitionParseResult Success(DocumentDefinition definition) => new(definition, null);
        public static DocumentDefinitionParseResult Failure(string error) => new(null, error);
    }

    private sealed record DocumentQuestionParseResult(DocumentQuestionDefinition? Question, string? Error)
    {
        public bool IsSuccess => Question is not null && Error is null;
        public static DocumentQuestionParseResult Success(DocumentQuestionDefinition question) => new(question, null);
        public static DocumentQuestionParseResult Failure(string error) => new(null, error);
    }
}
