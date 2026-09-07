using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record VsirMutation(
    VsirMutationKind Kind,
    string Path,
    string? Value);

internal sealed record VsirMutationResult(
    string? Source,
    string? Error)
{
    public bool IsSuccess => Error is null;

    public static VsirMutationResult Success(string source) => new(source, null);
    public static VsirMutationResult Failure(string error) => new(null, error);
}

internal static class VsirMutationEngine
{
    public static VsirMutationResult Apply(
        string source,
        IReadOnlyList<VsirMutation> mutations)
    {
        if (mutations.Count == 0)
            return VsirMutationResult.Failure("UPDATE001: At least one semantic mutation is required.");

        YamlStream yaml;
        YamlMappingNode root;
        try
        {
            yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
                return VsirMutationResult.Failure("UPDATE002: Expected one YAML mapping VSIR document.");
            root = mapping;
        }
        catch (Exception ex)
        {
            return VsirMutationResult.Failure($"UPDATE003: Could not parse VSIR artifact: {ex.Message}");
        }

        var identityError = ValidateIdentity(root);
        if (identityError is not null)
            return VsirMutationResult.Failure(identityError);

        var contradiction = FindContradiction(mutations);
        if (contradiction is not null)
            return VsirMutationResult.Failure(contradiction);

        foreach (var mutation in mutations)
        {
            var error = ApplyOne(root, mutation);
            if (error is not null)
                return VsirMutationResult.Failure(error);
        }

        var candidateError = ValidateCandidate(root);
        if (candidateError is not null)
            return VsirMutationResult.Failure(candidateError);

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return VsirMutationResult.Success(writer.ToString());
    }

    public static IReadOnlyList<VsirPathContract> Discover(string source, out string? error)
    {
        error = null;
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                error = "DISC001: Expected one YAML mapping VSIR document.";
                return [];
            }

            var identityError = ValidateIdentity(root);
            if (identityError is not null)
            {
                error = identityError.Replace("UPDATE", "DISC", StringComparison.Ordinal);
                return [];
            }

            var kind = Scalar(root, "kind");
            var classification = Scalar(root, "classification");
            return VsirAuthoringContract.Discover(kind, classification);
        }
        catch (Exception ex)
        {
            error = $"DISC002: Could not parse VSIR artifact: {ex.Message}";
            return [];
        }
    }

    private static string? ApplyOne(YamlMappingNode root, VsirMutation mutation)
    {
        if (mutation.Kind != VsirMutationKind.Set)
            return "UPDATE012: The current VSIR authoring frontier supports only 'set'.";

        return mutation.Path switch
        {
            "kind" => ApplyScalarMutation(root, "kind", mutation),
            "classification" => ApplyScalarMutation(root, "classification", mutation),
            _ => $"UPDATE004: Semantic path '{mutation.Path}' is not writable by the current authoring contract."
        };
    }

    private static string? ApplyScalarMutation(
        YamlMappingNode root,
        string path,
        VsirMutation mutation)
    {
        var value = mutation.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return $"UPDATE005: Value for semantic path '{path}' must not be empty.";

        root.Children[new YamlScalarNode(path)] = new YamlScalarNode(value);
        return null;
    }

    private static string? ValidateCandidate(YamlMappingNode root)
    {
        var kind = Scalar(root, "kind");
        var classification = Scalar(root, "classification");

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var error = VsirAuthoringContract.ValidateScalar("kind", kind, kind);
            if (error is not null)
                return error;
        }

        if (!string.IsNullOrWhiteSpace(classification))
        {
            var error = VsirAuthoringContract.ValidateScalar("classification", classification, kind);
            if (error is not null)
                return error;
        }

        return null;
    }

    private static string? ValidateIdentity(YamlMappingNode root)
    {
        var version = Scalar(root, "vsir");
        var name = Scalar(root, "name");
        if (string.IsNullOrWhiteSpace(version))
            return "UPDATE016: Progressive VSIR artifact requires 'vsir'.";
        if (string.IsNullOrWhiteSpace(name))
            return "UPDATE017: Progressive VSIR artifact requires 'name'.";
        return null;
    }

    private static string? FindContradiction(IReadOnlyList<VsirMutation> mutations)
    {
        foreach (var group in mutations.GroupBy(mutation => mutation.Path, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
                return $"UPDATE018: Semantic path '{group.Key}' cannot be set more than once in the same transaction.";
        }

        return null;
    }

    private static string? Scalar(YamlMappingNode root, string key) =>
        root.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar
            ? scalar.Value
            : null;
}
