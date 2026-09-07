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
        string? classification = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return VsirTemplateResult.Failure("NEW001: VSIR concept name is required.");

        var hasKind = !string.IsNullOrWhiteSpace(kind);

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

        if (classification is not null)
            lines.Add($"classification: {classification}");

        return VsirTemplateResult.Success(string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }
}
