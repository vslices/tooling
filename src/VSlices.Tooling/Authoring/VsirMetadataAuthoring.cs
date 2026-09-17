using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal static class VsirMetadataAuthoring
{
    public const string TagsPath = "tags";

    public static VsirPathContract TagsContract { get; } = new(
        TagsPath,
        "set<string>",
        VsirFrontierStatus.Optional,
        "Declares organizational metadata used to index, group and search artifacts. Tags carry no domain-semantic authority and remain available independently of the semantic frontier.",
        new HashSet<VsirMutationKind>
        {
            VsirMutationKind.Add,
            VsirMutationKind.Remove,
            VsirMutationKind.Set
        });

    public static VsirMutationResult Apply(
        string source,
        IReadOnlyList<VsirMutation> mutations)
    {
        if (mutations.Count == 0)
            return VsirMutationResult.Success(source);

        if (mutations.Any(mutation => mutation.Path != TagsPath))
            return VsirMutationResult.Failure("UPDATE045: Metadata authoring received a non-metadata mutation.");

        var contradiction = FindContradiction(mutations);
        if (contradiction is not null)
            return VsirMutationResult.Failure(contradiction);

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

        var existing = ReadTags(root, out var existingError);
        if (existingError is not null)
            return VsirMutationResult.Failure(existingError);

        var current = existing.ToList();
        foreach (var mutation in mutations)
        {
            var requested = ParseValues(mutation.Value);
            if (requested.Error is not null)
                return VsirMutationResult.Failure(requested.Error);

            switch (mutation.Kind)
            {
                case VsirMutationKind.Add:
                    foreach (var value in requested.Values!)
                    {
                        if (!current.Contains(value, StringComparer.Ordinal))
                            current.Add(value);
                    }
                    break;

                case VsirMutationKind.Remove:
                    current.RemoveAll(value => requested.Values!.Contains(value, StringComparer.Ordinal));
                    break;

                case VsirMutationKind.Set:
                    current = requested.Values!.ToList();
                    break;
            }
        }

        var tagsKey = new YamlScalarNode(TagsPath);
        if (current.Count == 0)
        {
            root.Children.Remove(tagsKey);
        }
        else
        {
            root.Children[tagsKey] = new YamlSequenceNode(
                current.Select(value => new YamlScalarNode(value)))
            {
                Style = YamlDotNet.Core.Events.SequenceStyle.Flow
            };
        }

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return VsirMutationResult.Success(writer.ToString());
    }

    public static IReadOnlyList<string> ReadTags(
        YamlMappingNode root,
        out string? error)
    {
        error = null;
        if (!root.Children.TryGetValue(new YamlScalarNode(TagsPath), out var node))
            return [];

        if (node is not YamlSequenceNode sequence)
        {
            error = "UPDATE046: 'tags' metadata must be a sequence of non-empty unique strings.";
            return [];
        }

        var values = new List<string>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            if (child is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
            {
                error = "UPDATE046: 'tags' metadata must be a sequence of non-empty unique strings.";
                return [];
            }

            values.Add(scalar.Value!);
        }

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            error = "UPDATE047: 'tags' metadata values must be unique.";
            return [];
        }

        return values;
    }

    public static string? ValidateValues(IReadOnlyList<string>? values)
    {
        if (values is null)
            return null;

        if (values.Any(string.IsNullOrWhiteSpace))
            return "NEW011: Tags must be non-empty strings.";

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            return "NEW012: Tags must be unique.";

        if (values.Any(value => value.Contains('\r') || value.Contains('\n')))
            return "NEW011: Tags must be single-line strings.";

        return null;
    }

    private static (IReadOnlyList<string>? Values, string? Error) ParseValues(string? value)
    {
        if (value is null)
            return (null, "UPDATE013: tags mutation requires a value.");

        var values = value.Split(',', StringSplitOptions.TrimEntries);
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
            return (null, "UPDATE013: tags values must be non-empty.");

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            return (null, "UPDATE014: tags values must be unique.");

        if (values.Any(item => item.Contains('\r') || item.Contains('\n')))
            return (null, "UPDATE013: tags values must be single-line.");

        return (values, null);
    }

    private static string? FindContradiction(IReadOnlyList<VsirMutation> mutations)
    {
        if (mutations.Any(mutation => mutation.Kind == VsirMutationKind.Set) && mutations.Count > 1)
            return "UPDATE018: Metadata path 'tags' cannot combine 'set' with another mutation in the same transaction.";

        var adds = mutations
            .Where(mutation => mutation.Kind == VsirMutationKind.Add)
            .SelectMany(mutation => ParseValues(mutation.Value).Values ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var removes = mutations
            .Where(mutation => mutation.Kind == VsirMutationKind.Remove)
            .SelectMany(mutation => ParseValues(mutation.Value).Values ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var overlap = adds.FirstOrDefault(removes.Contains);
        return overlap is null
            ? null
            : $"UPDATE019: Value '{overlap}' is both added to and removed from metadata path 'tags' in the same transaction.";
    }
}
