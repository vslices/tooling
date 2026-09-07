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
        var result = new List<VsirPathContract>
        {
            new(
                "tags",
                "set<string>",
                VsirFrontierStatus.Optional,
                "Organizational labels used to associate and search artifacts. Tags do not imply semantic behavior.",
                new HashSet<VsirMutationKind>
                {
                    VsirMutationKind.Add,
                    VsirMutationKind.Remove,
                    VsirMutationKind.Set
                })
        };

        if (string.IsNullOrWhiteSpace(kind))
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

        var sumShape = string.Equals(shape, "sum", StringComparison.Ordinal);

        result.Add(new(
            "state",
            "map<property, declaration>",
            VsirFrontierStatus.Required,
            sumShape
                ? "Declares state shared by every variant of the sum-shaped Domain Type. The map may be empty when the sum has no shared state; establish named shared coordinates with set over state.<property>."
                : "Declares the observable semantic properties that constitute a valid instance of the Domain Type. Establish or replace child coordinates with set over state.<property>; derived state may declare a direct source through state.<property>.from.",
            new HashSet<VsirMutationKind>
            {
                VsirMutationKind.Remove,
                VsirMutationKind.Set
            }));

        result.Add(new(
            "representation",
            "map<property, declaration>",
            VsirFrontierStatus.Required,
            sumShape
                ? "Declares representation shared by every variant of the sum-shaped Domain Type. The map may be empty; establish named shared coordinates with set over representation.<property>."
                : "Declares the observable form through which a valid Domain Type can be represented. Establish or replace child coordinates with set over representation.<property>; local from or mapping relations are authored through their own semantic paths.",
            new HashSet<VsirMutationKind>
            {
                VsirMutationKind.Remove,
                VsirMutationKind.Set
            }));

        if (sumShape)
        {
            result.Add(new(
                "variants",
                "map<variant, declaration>",
                VsirFrontierStatus.Required,
                "Declares the mutually exclusive alternatives of a sum-shaped Domain Type. Establish or replace a named alternative with set over variants.<variant>; exactly one variant is active in a valid instance.",
                new HashSet<VsirMutationKind>
                {
                    VsirMutationKind.Remove,
                    VsirMutationKind.Set
                }));
        }

        if (string.IsNullOrWhiteSpace(classification))
        {
            result.Add(new(
                "classification",
                "enum",
                VsirFrontierStatus.Required,
                "Declares the base semantic class of the Domain Type and activates classification-specific obligations.",
                new HashSet<VsirMutationKind> { VsirMutationKind.Set },
                DomainTypeClassifications));
            return result;
        }

        if (classification == "maintained")
        {
            result.Add(new(
                "values",
                "map<member, state>",
                VsirFrontierStatus.Required,
                "Declares maintained members and their semantic state. Establish or replace a named member with set over values.<member>; remove retires an existing member when allowed.",
                new HashSet<VsirMutationKind>
                {
                    VsirMutationKind.Remove,
                    VsirMutationKind.Set
                }));
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
                    "Declares what enters the transform before Domain Type validity has been established. Establish complete scalar input with set input=<type>, or establish named product fields with set over input.<property>.",
                    new HashSet<VsirMutationKind>
                    {
                        VsirMutationKind.Remove,
                        VsirMutationKind.Set
                    }));
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
