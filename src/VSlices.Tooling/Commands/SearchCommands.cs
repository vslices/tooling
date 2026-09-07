namespace VSlices.Tooling;

internal static class SearchCommands
{
    /// <summary>Searches VSIR artifacts using a property filter.</summary>
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

        foreach (var path in EnumerateVsir(root, policy))
        {
            var source = await File.ReadAllTextAsync(path, cancellationToken);
            if (parsed.Filter!.Matches(source))
                matches.Add(Path.GetRelativePath(root, path));
        }

        foreach (var match in matches.OrderBy(x => x, StringComparer.Ordinal))
            Console.WriteLine(match);

        return 0;
    }

    private static IEnumerable<string> EnumerateVsir(
        string root,
        ArtifactDiscoveryPolicy policy)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var file in Directory.EnumerateFiles(current, "*.vsir", SearchOption.TopDirectoryOnly))
            {
                if (!policy.IgnoreFile(file))
                    yield return file;
            }

            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (!policy.IgnoreDirectory(directory))
                    pending.Push(directory);
            }
        }
    }
}
