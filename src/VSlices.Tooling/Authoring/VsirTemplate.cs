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
    public static VsirTemplateResult Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return VsirTemplateResult.Failure("NEW001: VSIR concept name is required.");

        return VsirTemplateResult.Success(
            $"vsir: {VsirAuthoringContract.CurrentVsirVersion}{Environment.NewLine}" +
            $"name: {name}{Environment.NewLine}");
    }
}
