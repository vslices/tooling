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

    public static IReadOnlyList<string> DomainTypeClassifications { get; } =
    [
        "value-object",
        "entity",
        "identifier",
        "maintained",
        "aggregate-root"
    ];

    public static IReadOnlyList<VsirPathContract> Discover(
        string? kind,
        string? classification,
        bool hasState,
        bool hasRepresentation)
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

        if (classification is "value-object" or "entity" or "aggregate-root")
        {
            if (!hasState)
            {
                result.Add(new(
                    "state",
                    "mapping",
                    VsirFrontierStatus.Required,
                    "Declares the observable semantic properties that constitute a valid instance of the Domain Type.",
                    new HashSet<VsirMutationKind>()));
            }

            if (!hasRepresentation)
            {
                result.Add(new(
                    "representation",
                    "mapping",
                    VsirFrontierStatus.Required,
                    "Declares the observable form through which a valid Domain Type can be represented without changing its semantic validity.",
                    new HashSet<VsirMutationKind>()));
            }
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
            }));

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
            "classification" when !string.Equals(currentKind, DomainTypeKind, StringComparison.Ordinal) =>
                "UPDATE007: 'classification' is not writable until kind 'domain-type' is established.",
            "classification" when !DomainTypeClassifications.Contains(value, StringComparer.Ordinal) =>
                $"UPDATE008: Classification '{value}' is not valid for kind 'domain-type'. Supported values: {string.Join(", ", DomainTypeClassifications)}.",
            _ => null
        };
    }
}
