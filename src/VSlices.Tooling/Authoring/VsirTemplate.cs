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

    // Internal fixture helper only. The public `vslices new vsir` command never calls this overload.
    internal static VsirTemplateResult Create(
        string name,
        string? kind,
        string? shape,
        string? classification)
    {
        var created = Create(name);
        if (!created.IsSuccess)
            return created;

        var mutations = new List<VsirMutation>();
        if (!string.IsNullOrWhiteSpace(kind))
            mutations.Add(new(VsirMutationKind.Set, "kind", kind));
        if (!string.IsNullOrWhiteSpace(shape))
            mutations.Add(new(VsirMutationKind.Set, "shape", shape));
        if (!string.IsNullOrWhiteSpace(classification))
            mutations.Add(new(VsirMutationKind.Set, "classification", classification));

        if (mutations.Count == 0)
            return created;

        var advanced = VsirMutationPipeline.Apply(created.Source!, [.. mutations]);
        return advanced.IsSuccess
            ? VsirTemplateResult.Success(advanced.Source!)
            : VsirTemplateResult.Failure(advanced.Error!);
    }
}
