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
        DocsStandardCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DocumentTemplateResult.Failure("NEW101: Document name is required.");

        if (string.IsNullOrWhiteSpace(kind))
            return DocumentTemplateResult.Failure("NEW102: Document kind is required. Use --kind <type>.");

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

        var newline = Environment.NewLine;
        var root = definition.RootQuestion;
        var source =
            $"---{newline}" +
            $"artifact:{newline}" +
            $"  kind: document{newline}" +
            $"  type: {definition.Type}{newline}" +
            $"---{newline}{newline}" +
            $"# {root.Text}{newline}{newline}" +
            $"<!-- vslices:placeholder question={root.Id} -->{newline}";

        return DocumentTemplateResult.Success(source);
    }
}
