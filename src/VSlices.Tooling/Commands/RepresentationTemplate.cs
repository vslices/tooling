namespace VSlices.Tooling;

internal sealed record RepresentationTemplateResult(
    string? Source,
    string? Error)
{
    public bool IsSuccess => Error is null;

    public static RepresentationTemplateResult Success(string source) =>
        new(source, null);

    public static RepresentationTemplateResult Failure(string error) =>
        new(null, error);
}

internal static class RepresentationTemplate
{
    private const string CurrentVsirVersion = "0.1";

    private static readonly HashSet<string> DomainTypeClassifications =
        new(StringComparer.Ordinal)
        {
            "value-object",
            "identifier",
            "maintained",
            "aggregate-root"
        };

    private static readonly HashSet<string> DomainTypeShapes =
        new(StringComparer.Ordinal)
        {
            "product",
            "sum"
        };

    public static RepresentationTemplateResult Create(
        string name,
        string kind,
        string? classification = null,
        string? shape = null,
        IReadOnlyList<string>? traits = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RepresentationTemplateResult.Failure("NEW001: Representation name is required.");

        if (string.IsNullOrWhiteSpace(kind))
            return RepresentationTemplateResult.Failure("NEW002: Representation kind is required.");

        if (!kind.Equals("domain-type", StringComparison.Ordinal))
            return RepresentationTemplateResult.Failure(
                $"NEW003: Unsupported VSIR kind '{kind}'. Current supported kind: domain-type.");

        if (classification is not null && !DomainTypeClassifications.Contains(classification))
            return RepresentationTemplateResult.Failure(
                $"NEW004: Classification '{classification}' is not valid for kind 'domain-type'. " +
                $"Supported classifications: {string.Join(", ", DomainTypeClassifications.Order())}.");

        if (shape is not null && !DomainTypeShapes.Contains(shape))
            return RepresentationTemplateResult.Failure(
                $"NEW005: Shape '{shape}' is not valid for kind 'domain-type'. " +
                $"Supported shapes: {string.Join(", ", DomainTypeShapes.Order())}.");

        var explicitTraits = (traits ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (explicitTraits.Length != (traits ?? []).Count)
            return RepresentationTemplateResult.Failure("NEW006: Explicit traits must be non-empty and unique.");

        var inferredTraits = InferredTraits(classification);
        var redundant = explicitTraits.FirstOrDefault(inferredTraits.Contains);
        if (redundant is not null)
            return RepresentationTemplateResult.Failure(
                $"NEW007: Trait '{redundant}' is already implied by classification '{classification}'.");

        var lines = new List<string>
        {
            $"vsir: {CurrentVsirVersion}",
            $"kind: {kind}",
            $"name: {name}"
        };

        if (classification is not null)
            lines.Add($"classification: {classification}");

        if (shape is not null)
            lines.Add($"shape: {shape}");

        if (explicitTraits.Length > 0)
            lines.Add($"traits: [{string.Join(", ", explicitTraits)}]");

        return RepresentationTemplateResult.Success(string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static HashSet<string> InferredTraits(string? classification) =>
        classification switch
        {
            "identifier" => new(StringComparer.Ordinal) { "identifier" },
            "maintained" => new(StringComparer.Ordinal) { "maintained" },
            "aggregate-root" => new(StringComparer.Ordinal) { "aggregate-root", "entity" },
            _ => new(StringComparer.Ordinal)
        };
}
