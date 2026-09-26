namespace VSlices.Tooling;

internal sealed record DocumentTemplateResult(
    string? Source,
    string? Error)
{
    public bool IsSuccess => Source is not null && Error is null;

    public static DocumentTemplateResult Success(string source) =>
        new(source, null);

    public static DocumentTemplateResult Failure(string error) =>
        new(null, error);
}

internal static class DocumentTemplate
{
    public static DocumentTemplateResult Create(
        string name,
        string? kind,
        string? target,
        string? scope,
        IReadOnlyList<ArtifactRelation> relations,
        DocsStandardCatalog catalog,
        MaterializationTemplateDefinition materializationTemplate)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DocumentTemplateResult.Failure("NEW101: Document name is required.");

        if (string.IsNullOrWhiteSpace(kind))
            return DocumentTemplateResult.Failure("NEW102: Document kind is required. Use --kind <type>.");

        if (string.IsNullOrWhiteSpace(target))
            return DocumentTemplateResult.Failure(
                "NEW105: Document target is required. Use --target <target> or create it from a Nexus/Continuity Path recommendation.");

        var normalizedKind = kind.Trim();
        if (!catalog.TryGetDocument(normalizedKind, out var definition) || definition is null)
        {
            var available = string.Join(
                ", ",
                catalog.Documents
                    .Select(document => document.Type)
                    .OrderBy(type => type, StringComparer.Ordinal));

            return DocumentTemplateResult.Failure(
                $"NEW103: Document kind '{normalizedKind}' is not defined by the installed Docs Standard. " +
                $"Available kinds: {available}.");
        }

        var root = definition.RootQuestion;
        var renderedRoot = DocumentMaterialization.RenderQuestion(
            root.Text,
            semanticDepth: 0,
            materializationTemplate);
        if (!renderedRoot.IsSuccess)
            return DocumentTemplateResult.Failure(renderedRoot.Error!);

        var resolvedScope = string.IsNullOrWhiteSpace(scope)
            ? definition.Scopes.FirstOrDefault()
            : scope.Trim();

        var newline = Environment.NewLine;
        var frontMatter = KnowledgeArtifactFrontMatter.Render(
            "document",
            definition.Type,
            resolvedScope,
            target.Trim(),
            status: "draft",
            templateName: materializationTemplate.Id,
            relations);

        var source =
            frontMatter.Replace("\n", newline, StringComparison.Ordinal) +
            $"{newline}{newline}{renderedRoot.Source}{newline}";

        return DocumentTemplateResult.Success(source);
    }
}
