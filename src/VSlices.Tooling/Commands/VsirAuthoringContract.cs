namespace VSlices.Tooling;

internal enum VsirMutationKind
{
    Set
}

internal sealed record VsirPathContract(
    string Path,
    string ValueKind,
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
        string? classification)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return
            [
                new(
                    "kind",
                    "enum",
                    new HashSet<VsirMutationKind> { VsirMutationKind.Set },
                    Kinds)
            ];
        }

        if (!kind.Equals(DomainTypeKind, StringComparison.Ordinal))
            return [];

        if (string.IsNullOrWhiteSpace(classification))
        {
            return
            [
                new(
                    "classification",
                    "enum",
                    new HashSet<VsirMutationKind> { VsirMutationKind.Set },
                    DomainTypeClassifications)
            ];
        }

        return [];
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
