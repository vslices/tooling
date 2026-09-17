namespace VSlices.Targets.DotNet.Refactor;

internal sealed record TypeResolutionArguments(
    string ProjectPath,
    string ManifestPath,
    IReadOnlyList<string> TypeNames)
{
    public static TypeResolutionArguments? Parse(string[] args)
    {
        string? project = null;
        string? manifest = null;
        var types = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--type" && i + 1 < args.Length)
            {
                types.Add(args[++i]);
                continue;
            }

            if (i + 1 >= args.Length)
                return null;

            var value = args[++i];
            switch (args[i - 1])
            {
                case "--project":
                    project = value;
                    break;
                case "--manifest":
                    manifest = value;
                    break;
                default:
                    return null;
            }
        }

        return string.IsNullOrWhiteSpace(project) ||
               string.IsNullOrWhiteSpace(manifest) ||
               types.Count == 0
            ? null
            : new(
                Path.GetFullPath(project),
                Path.GetFullPath(manifest),
                types.Distinct(StringComparer.Ordinal).ToArray());
    }
}
