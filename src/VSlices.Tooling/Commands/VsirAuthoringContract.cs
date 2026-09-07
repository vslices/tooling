namespace VSlices.Tooling;

internal enum VsirMutationKind
{
    Add,
    Remove,
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
        "identifier",
        "maintained",
        "aggregate-root"
    ];

    public static IReadOnlyList<string> DomainTypeShapes { get; } =
    [
        "product",
        "sum"
    ];

    public static HashSet<string> InferredTraits(string? classification) =>
        classification switch
        {
            "identifier" => new(StringComparer.Ordinal) { "identifier" },
            "maintained" => new(StringComparer.Ordinal) { "maintained" },
            "aggregate-root" => new(StringComparer.Ordinal) { "aggregate-root", "entity" },
            _ => new(StringComparer.Ordinal)
        };

    public static IReadOnlyList<VsirPathContract> Discover(
        string? kind,
        string? classification)
    {
        var result = new List<VsirPathContract>
        {
            new(
                "tags",
                "set<string>",
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
                new HashSet<VsirMutationKind> { VsirMutationKind.Set },
                DomainTypeClassifications));
            return result;
        }

        result.Add(new(
            "shape",
            "enum",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set },
            DomainTypeShapes));

        result.Add(new(
            "traits",
            "set<string>",
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
            "shape" when !string.Equals(currentKind, DomainTypeKind, StringComparison.Ordinal) =>
                "UPDATE009: 'shape' is not writable until kind 'domain-type' is established.",
            "shape" when !DomainTypeShapes.Contains(value, StringComparer.Ordinal) =>
                $"UPDATE010: Shape '{value}' is not valid for kind 'domain-type'. Supported values: {string.Join(", ", DomainTypeShapes)}.",
            _ => null
        };
    }
}
