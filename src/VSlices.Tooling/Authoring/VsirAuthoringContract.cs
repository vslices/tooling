namespace VSlices.Tooling;

internal enum VsirMutationKind
{
    Add,
    Remove,
    Set
}

internal enum VsirFrontierStatus
{
    Required,
    Optional
}

internal sealed record VsirPathContract(
    string Path,
    string ValueKind,
    VsirFrontierStatus Status,
    string Meaning,
    IReadOnlySet<VsirMutationKind> Operations,
    IReadOnlyList<string>? AllowedValues = null);

internal static class VsirAuthoringContract
{
    public const string CurrentVsirVersion = "0.1";
    public const string DomainTypeKind = "domain-type";

    public static IReadOnlyList<string> Kinds { get; } = [DomainTypeKind];

    public static IReadOnlyList<string> DomainTypeShapes { get; } =
    [
        "product",
        "sum"
    ];

    public static IReadOnlyList<string> DomainTypeClassifications { get; } =
    [
        "value-object",
        "entity",
        "identifier",
        "maintained",
        "aggregate-root"
    ];

    public static IReadOnlyList<string> ExplicitDomainTypeTraits { get; } =
    [
        "transform"
    ];

    public static IReadOnlyList<VsirPathContract> Discover(
        string? kind,
        string? shape,
        string? classification,
        IReadOnlyList<string> explicitTraits,
        bool hasState,
        bool hasRepresentation,
        bool hasInput,
        bool hasConstruction)
    {
        var result = new List<VsirPathContract>();

        if (string.IsNullOrWhiteSpace(kind) || !Kinds.Contains(kind, StringComparer.Ordinal))
        {
            result.Add(new(
                "kind",
                "enum",
                VsirFrontierStatus.Required,
                "Selects the VSIR artifact family and determines which declarations are meaningful for the artifact.",
                new HashSet<VsirMutationKind> { VsirMutationKind.Set },
                Kinds));
            return result;
        }

        if (!kind.Equals(DomainTypeKind, StringComparison.Ordinal))
            return result;

        result.Add(new(
            "shape",
            "enum",
            VsirFrontierStatus.Required,
            "Declares how one valid Domain Type instance is structurally composed. Product means all state coordinates coexist; sum means shared state plus exactly one named variant is active.",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set },
            DomainTypeShapes));

        if (string.IsNullOrWhiteSpace(shape) || !DomainTypeShapes.Contains(shape, StringComparer.Ordinal))
            return result;

        var sumShape = string.Equals(shape, "sum", StringComparison.Ordinal);

        result.Add(new(
            "state",
            "map<property, declaration>",
            VsirFrontierStatus.Required,
            sumShape
                ? "Declares state shared by every variant of the sum-shaped Domain Type. The map may be empty; establish a named shared coordinate with set over state.<property>."
                : "Declares the observable semantic properties that constitute a valid instance. Establish a named coordinate such as state.Value with set over state.<property>; derived coordinates may declare semantic provenance through state.<property>.from.",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set }));

        result.Add(new(
            "representation",
            "map<property, declaration>",
            VsirFrontierStatus.Required,
            sumShape
                ? "Declares representation shared by every variant. Establish a named shared coordinate with set over representation.<property>; the effective representation must preserve the active variant."
                : "Declares the observable representation. Establish a named coordinate such as representation.Value with set over representation.<property>; direct reuse uses representation.<property>.from and semantic transformation uses representation.<property>.mapping.",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set }));

        if (sumShape)
        {
            result.Add(new(
                "variants",
                "map<variant, declaration>",
                VsirFrontierStatus.Required,
                "Declares mutually exclusive alternatives. Establish a named alternative with set over variants.<variant>; existing variants are exposed separately when removable or replaceable.",
                new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
        }

        result.Add(new(
            "classification",
            "enum",
            VsirFrontierStatus.Required,
            "Declares the base semantic class of the Domain Type and activates classification-specific obligations.",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set },
            DomainTypeClassifications));

        if (string.IsNullOrWhiteSpace(classification) ||
            !DomainTypeClassifications.Contains(classification, StringComparer.Ordinal))
        {
            return result;
        }

        if (classification == "maintained")
        {
            result.Add(new(
                "values",
                "map<member, state>",
                VsirFrontierStatus.Required,
                "Declares maintained members and their semantic state. Establish a named member with set over values.<member>; existing members are exposed separately when replaceable or removable.",
                new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
        }

        if (classification is "identifier" or "maintained")
        {
            result.Add(new(
                "equality",
                "strategy",
                VsirFrontierStatus.Required,
                classification == "identifier"
                    ? "Declares the authoritative equality strategy for the identifier."
                    : "Declares the authoritative equality strategy for maintained members. IdentityType evidences intrinsic ordinal-equals over state.Name.",
                new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
        }

        result.Add(new(
            "traits",
            "set<string>",
            VsirFrontierStatus.Optional,
            "Declares additional semantic capabilities that are not already implied by the Domain Type classification.",
            new HashSet<VsirMutationKind>
            {
                VsirMutationKind.Add,
                VsirMutationKind.Remove,
                VsirMutationKind.Set
            },
            ExplicitDomainTypeTraits));

        if (explicitTraits.Contains("transform", StringComparer.Ordinal))
        {
            if (!hasInput)
            {
                result.Add(new(
                    "input",
                    "map<property, declaration> | scalar semantic type",
                    VsirFrontierStatus.Required,
                    "Declares what enters the transform before Domain Type validity has been established. Establish scalar input with set input=<type>, or establish a named product field with set over input.<property>.",
                    new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
            }

            if (!hasConstruction)
            {
                result.Add(new(
                    "construction",
                    "sequence<step>",
                    VsirFrontierStatus.Required,
                    "Declares the ordered semantic conditions and steps that establish a valid Domain Type from transform input. Establish the complete ordered sequence with set.",
                    new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
            }
        }

        return result;
    }

    public static string? ValidateScalar(string path, string value, string? currentKind)
    {
        if (string.IsNullOrWhiteSpace(value))
            return $"UPDATE005: Value for semantic path '{path}' must not be empty.";

        return path switch
        {
            "kind" when !Kinds.Contains(value, StringComparer.Ordinal) =>
                $"UPDATE006: Unsupported VSIR kind '{value}'. Supported values: {string.Join(", ", Kinds)}.",
            "shape" when !string.Equals(currentKind, DomainTypeKind, StringComparison.Ordinal) =>
                "UPDATE033: 'shape' is not writable until kind 'domain-type' is established.",
            "shape" when !DomainTypeShapes.Contains(value, StringComparer.Ordinal) =>
                $"UPDATE034: Shape '{value}' is not valid for kind 'domain-type'. Supported values: {string.Join(", ", DomainTypeShapes)}.",
            "classification" when !string.Equals(currentKind, DomainTypeKind, StringComparison.Ordinal) =>
                "UPDATE007: 'classification' is not writable until kind 'domain-type' is established.",
            "classification" when !DomainTypeClassifications.Contains(value, StringComparer.Ordinal) =>
                $"UPDATE008: Classification '{value}' is not valid for kind 'domain-type'. Supported values: {string.Join(", ", DomainTypeClassifications)}.",
            _ => null
        };
    }
}
