namespace VSlices.Tooling;

internal sealed record VsirTemplateResult(
    string? Source,
    string? Error)
{
    public bool IsSuccess => Error is null;

    public static VsirTemplateResult Success(string source) =>
        new(source, null);

    public static VsirTemplateResult Failure(string error) =>
        new(null, error);
}

internal static class VsirTemplate
{
    public static VsirTemplateResult Create(
        string name,
        string? kind = null,
        string? shape = null,
        string? classification = null,
        IReadOnlyList<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return VsirTemplateResult.Failure("NEW001: VSIR concept name is required.");

        var suppliedTags = tags ?? [];
        var explicitTags = suppliedTags
            .Where(value => !string.IsNullOrWhiteSpace(value) && !ContainsLineBreak(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (explicitTags.Length != suppliedTags.Count)
            return VsirTemplateResult.Failure("NEW008: Tags must be non-empty, single-line and unique.");

        var hasKind = !string.IsNullOrWhiteSpace(kind);

        if (!hasKind && shape is not null)
        {
            return VsirTemplateResult.Failure(
                "NEW009: --shape requires --kind because its validity is kind-specific.");
        }

        if (!hasKind && classification is not null)
        {
            return VsirTemplateResult.Failure(
                "NEW002: --classification requires --kind because its validity is kind-specific.");
        }

        if (hasKind && !VsirAuthoringContract.Kinds.Contains(kind!, StringComparer.Ordinal))
        {
            return VsirTemplateResult.Failure(
                $"NEW003: Unsupported VSIR kind '{kind}'. Current supported values: {string.Join(", ", VsirAuthoringContract.Kinds)}.");
        }

        if (shape is not null &&
            !VsirAuthoringContract.DomainTypeShapes.Contains(shape, StringComparer.Ordinal))
        {
            return VsirTemplateResult.Failure(
                $"NEW010: Shape '{shape}' is not valid for kind 'domain-type'. " +
                $"Supported shapes: {string.Join(", ", VsirAuthoringContract.DomainTypeShapes)}.");
        }

        if (classification is not null &&
            !VsirAuthoringContract.DomainTypeClassifications.Contains(classification, StringComparer.Ordinal))
        {
            return VsirTemplateResult.Failure(
                $"NEW004: Classification '{classification}' is not valid for kind 'domain-type'. " +
                $"Supported classifications: {string.Join(", ", VsirAuthoringContract.DomainTypeClassifications)}.");
        }

        var lines = new List<string>
        {
            $"vsir: {VsirAuthoringContract.CurrentVsirVersion}"
        };

        if (hasKind)
            lines.Add($"kind: {kind}");

        lines.Add($"name: {name}");

        if (explicitTags.Length > 0)
            lines.Add($"tags: [{string.Join(", ", explicitTags.Select(QuoteYamlScalar))}]");

        if (shape is not null)
            lines.Add($"shape: {shape}");

        if (classification is not null)
            lines.Add($"classification: {classification}");

        return VsirTemplateResult.Success(string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static bool ContainsLineBreak(string value) =>
        value.Contains('\r') || value.Contains('\n');

    private static string QuoteYamlScalar(string value) =>
        $"'{value.Replace("'", "''")}'";
}
