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
        string? classification = null,
        string? shape = null,
        IReadOnlyList<string>? traits = null,
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
        var hasKindSpecificChoices =
            classification is not null ||
            shape is not null ||
            (traits?.Count ?? 0) > 0;

        if (!hasKind && hasKindSpecificChoices)
        {
            return VsirTemplateResult.Failure(
                "NEW002: --classification, --shape and --traits require --kind because their validity is kind-specific.");
        }

        if (hasKind && !VsirAuthoringContract.Kinds.Contains(kind!, StringComparer.Ordinal))
        {
            return VsirTemplateResult.Failure(
                $"NEW003: Unsupported VSIR kind '{kind}'. Current supported values: {string.Join(", ", VsirAuthoringContract.Kinds)}.");
        }

        if (classification is not null &&
            !VsirAuthoringContract.DomainTypeClassifications.Contains(classification, StringComparer.Ordinal))
        {
            return VsirTemplateResult.Failure(
                $"NEW004: Classification '{classification}' is not valid for kind 'domain-type'. " +
                $"Supported classifications: {string.Join(", ", VsirAuthoringContract.DomainTypeClassifications)}.");
        }

        if (shape is not null &&
            !VsirAuthoringContract.DomainTypeShapes.Contains(shape, StringComparer.Ordinal))
        {
            return VsirTemplateResult.Failure(
                $"NEW005: Shape '{shape}' is not valid for kind 'domain-type'. " +
                $"Supported shapes: {string.Join(", ", VsirAuthoringContract.DomainTypeShapes)}.");
        }

        var suppliedTraits = traits ?? [];
        var explicitTraits = suppliedTraits
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (explicitTraits.Length != suppliedTraits.Count)
            return VsirTemplateResult.Failure("NEW006: Explicit traits must be non-empty and unique.");

        var inferredTraits = VsirAuthoringContract.InferredTraits(classification);
        var redundant = explicitTraits.FirstOrDefault(inferredTraits.Contains);
        if (redundant is not null)
        {
            return VsirTemplateResult.Failure(
                $"NEW007: Trait '{redundant}' is already implied by classification '{classification}'.");
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

        if (classification is not null)
            lines.Add($"classification: {classification}");

        if (shape is not null)
            lines.Add($"shape: {shape}");

        if (explicitTraits.Length > 0)
            lines.Add($"traits: [{string.Join(", ", explicitTraits)}]");

        return VsirTemplateResult.Success(string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static bool ContainsLineBreak(string value) =>
        value.Contains('\r') || value.Contains('\n');

    private static string QuoteYamlScalar(string value) =>
        $"'{value.Replace("'", "''")}'";
}
