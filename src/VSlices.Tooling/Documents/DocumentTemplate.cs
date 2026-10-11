namespace VSlices.Tooling;

internal sealed record DocumentTemplateResult(string? Source, string? Error)
{
    public bool IsSuccess => Source is not null && Error is null;
    public static DocumentTemplateResult Success(string source) => new(source, null);
    public static DocumentTemplateResult Failure(string error) => new(null, error);
}

internal static class DocumentTemplate
{
    public static DocumentTemplateResult Create(
        string name, string? kind, string? target, string? scope,
        IReadOnlyList<ArtifactRelation> relations, DocsStandardCatalog catalog,
        MaterializationTemplateDefinition materializationTemplate)
    {
        if (string.IsNullOrWhiteSpace(name)) return DocumentTemplateResult.Failure("NEW101: Document name is required.");
        if (string.IsNullOrWhiteSpace(kind)) return DocumentTemplateResult.Failure("NEW102: Document kind is required. Use --kind <type>.");
        if (string.IsNullOrWhiteSpace(target)) return DocumentTemplateResult.Failure("NEW105: Document target is required. Use --target <target> or a recommendation.");
        if (!catalog.TryGetDocument(kind.Trim(), out var definition) || definition is null)
            return DocumentTemplateResult.Failure($"NEW103: Document kind '{kind}' is not defined by the installed Docs Standard.");
        var root = DocumentMaterialization.RenderQuestion(definition.RootQuestion.Text, semanticDepth: 0, materializationTemplate);
        if (!root.IsSuccess) return DocumentTemplateResult.Failure(root.Error!);
        var resolvedScope = KnowledgeArtifactCommandSupport.ResolveScope(scope, definition.Scopes, null);
        var frontMatter = KnowledgeArtifactFrontMatter.Render("document", definition.Type, resolvedScope, target.Trim(), "draft", materializationTemplate.Id, relations);
        var newline = Environment.NewLine;
        return DocumentTemplateResult.Success(frontMatter.Replace("\n", newline, StringComparison.Ordinal) + $"{newline}{newline}{root.Source}{newline}");
    }
}
