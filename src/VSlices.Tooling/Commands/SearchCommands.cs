namespace VSlices.Tooling;

internal static class SearchCommands
{
    /// <summary>Searches VSIR and knowledge artifacts using a property filter.</summary>
    /// <param name="filter">Filter in &lt;property&gt;:&lt;operator&gt;:&lt;value&gt; form. Supported operators: contains, equals.</param>
    public static async Task<int> Search(
        string filter,
        CancellationToken cancellationToken = default)
    {
        var parsed = SearchFilter.Parse(filter);
        if (parsed.Error is not null)
        {
            TerminalOutput.Error(parsed.Error);
            return 2;
        }

        var root = Path.GetFullPath(Environment.CurrentDirectory);
        var policy = ArtifactDiscoveryPolicy.Load(root);
        var matches = new List<string>();

        foreach (var path in EnumerateArtifacts(root, policy))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = await File.ReadAllTextAsync(path, cancellationToken);

            var matched = Path.GetExtension(path).Equals(".vsir", StringComparison.OrdinalIgnoreCase)
                ? parsed.Filter!.Matches(source)
                : MatchesKnowledgeArtifact(parsed.Filter!, source);

            if (matched)
                matches.Add(Path.GetRelativePath(root, path));
        }

        foreach (var match in matches.OrderBy(x => x, StringComparer.Ordinal))
            Console.WriteLine(match);

        return 0;
    }

    private static bool MatchesKnowledgeArtifact(
        SearchFilter filter,
        string source)
    {
        if (!source.TrimStart().StartsWith("---", StringComparison.Ordinal))
            return false;

        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        return metadata.IsSuccess &&
            KnowledgeArtifactFrontMatter.IsKnownKind(metadata.Metadata!.Kind) &&
            filter.Matches(metadata.Metadata);
    }

    private static IEnumerable<string> EnumerateArtifacts(
        string root,
        ArtifactDiscoveryPolicy policy)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
            {
                var extension = Path.GetExtension(file);
                if ((extension.Equals(".vsir", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".md", StringComparison.OrdinalIgnoreCase)) &&
                    !policy.IgnoreFile(file))
                {
                    yield return file;
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (!policy.IgnoreDirectory(directory))
                    pending.Push(directory);
            }
        }
    }
}
