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

    // Public authoring advertises only shapes that the canonical parser,
    // validator and lowering pipeline can currently consume end-to-end.
    public static IReadOnlyList<string> DomainTypeShapes { get; } =
    [
        "product"
    ];

    // Classification and traits are independent semantic axes. TicketId
    // witnesses identifier classification, while TicketCode witnesses a
    // value-object classification with explicit identifier capability.
    public static IReadOnlyList<string> DomainTypeClassifications { get; } =
    [
        "value-object",
        "identifier"
    ];

    public static IReadOnlyList<string> ExplicitDomainTypeTraits { get; } =
    [
        "transform",
        "identifier",
        "refined"
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
            "Declares how one valid Domain Type instance is structurally composed. The current canonical end-to-end surface admits product shape.",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set },
            DomainTypeShapes));

        if (string.IsNullOrWhiteSpace(shape) || !DomainTypeShapes.Contains(shape, StringComparer.Ordinal))
            return result;

        result.Add(new(
            "state",
            "map<property, declaration>",
            VsirFrontierStatus.Required,
            "Declares the observable semantic properties that constitute a valid instance. Establish a named coordinate such as state.Value with set over state.<property>; derived coordinates may declare semantic provenance through state.<property>.from.",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set }));

        result.Add(new(
            "representation",
            "map<property, declaration>",
            VsirFrontierStatus.Required,
            "Declares the observable representation. Establish a named coordinate such as representation.Value with set over representation.<property>; direct reuse uses representation.<property>.from and semantic transformation uses representation.<property>.mapping.",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set }));

        result.Add(new(
            "classification",
            "enum",
            VsirFrontierStatus.Required,
            "Declares the semantic class of the Domain Type. The currently evidenced end-to-end surface admits value-object and identifier.",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set },
            DomainTypeClassifications));

        if (string.IsNullOrWhiteSpace(classification) ||
            !DomainTypeClassifications.Contains(classification, StringComparer.Ordinal))
        {
            return result;
        }

        var hasTransform = explicitTraits.Contains("transform", StringComparer.Ordinal);
        var hasIdentifierCapability =
            classification.Equals("identifier", StringComparison.Ordinal) ||
            explicitTraits.Contains("identifier", StringComparer.Ordinal);

        result.Add(new(
            "traits",
            "set<string>",
            hasTransform ? VsirFrontierStatus.Optional : VsirFrontierStatus.Required,
            hasTransform
                ? "Declares additional semantic capabilities beyond the required transform capability. Identifier and refined are currently evidenced end-to-end."
                : "Declares semantic capabilities. The current canonical Domain Type surface requires transform; identifier and refined may add further obligations.",
            new HashSet<VsirMutationKind>
            {
                VsirMutationKind.Add,
                VsirMutationKind.Remove,
                VsirMutationKind.Set
            },
            ExplicitDomainTypeTraits));

        if (hasIdentifierCapability)
        {
            result.Add(new(
                "equality",
                "strategy",
                VsirFrontierStatus.Required,
                "Declares the authoritative equality strategy required by identifier semantics, whether established by classification or explicit trait.",
                new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
        }

        if (explicitTraits.Contains("refined", StringComparer.Ordinal))
        {
            result.Add(new(
                "refined-from",
                "semantic-type",
                VsirFrontierStatus.Required,
                "Declares the semantic base type refined by this Domain Type. Canonical VSIR uses kebab-case 'refined-from'.",
                new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
        }

        if (hasTransform)
        {
            if (!hasInput)
            {
                result.Add(new(
                    "input",
                    explicitTraits.Contains("refined", StringComparer.Ordinal)
                        ? "scalar semantic type"
                        : "map<property, declaration> | scalar semantic type",
                    VsirFrontierStatus.Required,
                    explicitTraits.Contains("refined", StringComparer.Ordinal)
                        ? "Declares the scalar base value entering refined construction. It must match refined-from for a conforming refined Domain Type."
                        : "Declares what enters the transform before Domain Type validity has been established. Establish scalar input with set input=<type>, or establish a named product field with set over input.<property>.",
                    new HashSet<VsirMutationKind> { VsirMutationKind.Set }));
            }

            if (!hasConstruction)
            {
                result.Add(new(
                    "construction",
                    "sequence<step>",
                    VsirFrontierStatus.Optional,
                    "Declares ordered semantic conditions or steps beyond direct deterministic input-to-state construction. Omit it when product input already establishes state directly, as in TicketId.",
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
                $"UPDATE034: Shape '{value}' is not part of the current end-to-end authoring/lowering surface for kind 'domain-type'. Supported values: {string.Join(", ", DomainTypeShapes)}.",
            "classification" when !string.Equals(currentKind, DomainTypeKind, StringComparison.Ordinal) =>
                "UPDATE007: 'classification' is not writable until kind 'domain-type' is established.",
            "classification" when !DomainTypeClassifications.Contains(value, StringComparer.Ordinal) =>
                $"UPDATE008: Classification '{value}' is not part of the current end-to-end authoring/lowering surface for kind 'domain-type'. Supported values: {string.Join(", ", DomainTypeClassifications)}.",
            _ => null
        };
    }
}
