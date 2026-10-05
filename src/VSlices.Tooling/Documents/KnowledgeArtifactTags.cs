namespace VSlices.Tooling;

internal sealed record KnowledgeArtifactTagMutationResult(
    string? Source,
    string? Error)
{
    public bool IsSuccess => Source is not null && Error is null;

    public static KnowledgeArtifactTagMutationResult Success(string source) =>
        new(source, null);

    public static KnowledgeArtifactTagMutationResult Failure(string error) =>
        new(null, error);
}

internal static class KnowledgeArtifactTags
{
    public static bool HasMutation(
        string? tags,
        string? addTags,
        string? removeTags) =>
        tags is not null || addTags is not null || removeTags is not null;

    public static KnowledgeArtifactTagMutationResult Apply(
        string source,
        string? tags,
        string? addTags,
        string? removeTags)
    {
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!metadata.IsSuccess)
            return KnowledgeArtifactTagMutationResult.Failure(metadata.Error!);

        var set = Parse(tags, "--tags");
        if (set.Error is not null)
            return KnowledgeArtifactTagMutationResult.Failure(set.Error);

        var add = Parse(addTags, "--add-tags");
        if (add.Error is not null)
            return KnowledgeArtifactTagMutationResult.Failure(add.Error);

        var remove = Parse(removeTags, "--remove-tags");
        if (remove.Error is not null)
            return KnowledgeArtifactTagMutationResult.Failure(remove.Error);

        var current = tags is null
            ? metadata.Metadata!.Tags.ToList()
            : set.Tags!.ToList();

        foreach (var tag in add.Tags ?? [])
        {
            if (!current.Contains(tag, StringComparer.Ordinal))
                current.Add(tag);
        }

        if (remove.Tags is { Count: > 0 })
        {
            var removals = new HashSet<string>(remove.Tags, StringComparer.Ordinal);
            current.RemoveAll(removals.Contains);
        }

        var updatedMetadata = metadata.Metadata! with { Tags = current };
        return KnowledgeArtifactTagMutationResult.Success(
            KnowledgeArtifactFrontMatter.WithMetadata(
                source,
                updatedMetadata,
                updatedMetadata.Relations));
    }

    public static string Display(IReadOnlyList<string> tags) =>
        tags.Count == 0
            ? "[]"
            : "[" + string.Join(", ", tags) + "]";

    public static void WriteDiscovery(
        string family,
        string artifact,
        KnowledgeArtifactMetadata metadata)
    {
        Console.WriteLine($"  tags: {Display(metadata.Tags)}");
        Console.WriteLine("  tag commands:");
        Console.WriteLine(
            $"    set: vslices update {family} {QuoteArgument(artifact)} --tags \"<tag,...>\"");
        Console.WriteLine(
            $"    add: vslices update {family} {QuoteArgument(artifact)} --add-tags \"<tag,...>\"");
        Console.WriteLine(
            $"    remove: vslices update {family} {QuoteArgument(artifact)} --remove-tags \"<tag,...>\"");
    }

    private static (IReadOnlyList<string>? Tags, string? Error) Parse(
        string? value,
        string option)
    {
        if (value is null)
            return (null, null);

        if (string.IsNullOrWhiteSpace(value))
            return (null, $"TAG001: {option} requires one or more comma-separated non-empty tags.");

        var parts = value.Split(',', StringSplitOptions.None)
            .Select(part => part.Trim())
            .ToArray();

        if (parts.Any(part => part.Length == 0))
            return (null, $"TAG002: {option} contains an empty tag.");

        if (parts.Any(part => part.Any(char.IsControl)))
            return (null, $"TAG003: {option} contains a control character.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in parts)
        {
            if (!seen.Add(tag))
                return (null, $"TAG004: {option} contains duplicate tag '{tag}'.");
        }

        return (parts, null);
    }

    private static string QuoteArgument(string value) =>
        !value.Any(char.IsWhiteSpace) && !value.Contains('"')
            ? value
            : "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
