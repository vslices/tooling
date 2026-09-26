namespace VSlices.Tooling;

internal static class RelationalDiscoveryCommands
{
    public static Task<int> Nexus(
        string artifact,
        CancellationToken cancellationToken = default) =>
        Discover(artifact, "nexus", cancellationToken);

    public static Task<int> ContinuityPath(
        string artifact,
        CancellationToken cancellationToken = default) =>
        Discover(artifact, "continuity-path", cancellationToken);

    private static async Task<int> Discover(
        string artifact,
        string expectedKind,
        CancellationToken cancellationToken)
    {
        var existing = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(
            artifact,
            Environment.CurrentDirectory);
        if (!existing.IsSuccess)
        {
            TerminalOutput.Error(existing.Error!);
            return 1;
        }

        var path = existing.Resolution!.Path;
        var metadata = existing.Resolution.Metadata;
        if (!metadata.Kind.Equals(expectedKind, StringComparison.Ordinal))
        {
            TerminalOutput.Error(
                $"RELDISC001: '{path}' is artifact.kind '{metadata.Kind}', not '{expectedKind}'.");
            return 2;
        }

        var standardsResult = KnowledgeArtifactCommandSupport.LoadStandards(path);
        if (!standardsResult.IsSuccess)
        {
            TerminalOutput.Error(standardsResult.Error!);
            return 1;
        }

        var standards = standardsResult.Standards!;
        var source = await File.ReadAllTextAsync(path, cancellationToken);

        RelationalArtifactStateResult state;
        IReadOnlyList<(string Id, RelationalRecommendation Recommendation)> recommendations;

        if (expectedKind.Equals("nexus", StringComparison.Ordinal))
        {
            if (!standards.Relational.TryGetNexus(metadata.Type, out var definition) || definition is null)
            {
                TerminalOutput.Error(
                    $"RELDISC002: Nexus type '{metadata.Type}' is not defined by the installed Docs Standard candidate surface.");
                return 2;
            }

            state = RelationalArtifact.ReadNexus(source, definition);
            if (!state.IsSuccess)
            {
                TerminalOutput.Error(state.Error!);
                return 2;
            }

            recommendations = RelationalArtifact.EnumerateRecommendations(definition, state.State!);
        }
        else
        {
            if (!standards.Relational.TryGetContinuityPath(metadata.Type, out var definition) || definition is null)
            {
                TerminalOutput.Error(
                    $"RELDISC003: Continuity Path type '{metadata.Type}' is not defined by the installed Docs Standard candidate surface.");
                return 2;
            }

            state = RelationalArtifact.ReadContinuityPath(source, definition);
            if (!state.IsSuccess)
            {
                TerminalOutput.Error(state.Error!);
                return 2;
            }

            recommendations = RelationalArtifact.EnumerateRecommendations(definition, state.State!);
        }

        Console.WriteLine(expectedKind == "nexus" ? "Nexus:" : "Continuity Path:");
        Console.WriteLine($"  path: {Path.GetRelativePath(Environment.CurrentDirectory, path)}");
        Console.WriteLine($"  type: {metadata.Type}");
        Console.WriteLine($"  scope: {metadata.Scope ?? "<unset>"}");
        Console.WriteLine($"  target: {metadata.Target ?? "<unset>"}");
        Console.WriteLine($"  status: {metadata.Status}");
        Console.WriteLine($"  tooling: {metadata.ToolingVersion}");
        Console.WriteLine();
        Console.WriteLine("Discovery surface:");

        var entries = new List<DiscoveryEntry>();
        entries.AddRange(state.State!.Questions.Select(question =>
            DiscoveryEntry.Question(question)));

        foreach (var (id, recommendation) in recommendations)
        {
            var relation = metadata.Relations.FirstOrDefault(candidate =>
                string.Equals(candidate.RecommendationId, id, StringComparison.Ordinal));
            entries.Add(DiscoveryEntry.Recommendation(id, recommendation, relation));
        }

        foreach (var entry in entries.OrderBy(entry => entry.SelectionPath, SelectionPathComparer.Instance))
        {
            Console.WriteLine();
            Console.WriteLine($"[{entry.SelectionPath}] {entry.Title}");
            Console.WriteLine($"  kind: {entry.Kind}");

            if (entry.Question is not null)
            {
                var question = entry.Question;
                Console.WriteLine($"  status: {(question.IsAnswered ? "answered" : "available")}");
                if (!string.IsNullOrWhiteSpace(question.ParentSelectionPath))
                    Console.WriteLine($"  parent: [{question.ParentSelectionPath}]");
                if (question.IsAnswered)
                    Console.WriteLine($"  answer: {question.Answer}");

                Console.WriteLine(
                    $"  command: vslices update {DisplayCommandKind(expectedKind)} {QuoteArgument(artifact)} --question-id {question.SelectionPath} --answer \"<answer>\"");
                continue;
            }

            var recommendation = entry.Recommendation!;
            Console.WriteLine($"  recommends: {recommendation.Family} {recommendation.Type}");
            Console.WriteLine($"  role: {recommendation.Role}");

            if (entry.Relation is not null)
            {
                var relatedPath = KnowledgeArtifactCommandSupport.ResolveRelationTargetPath(
                    path,
                    entry.Relation.Path);
                Console.WriteLine("  status: created");
                Console.WriteLine($"  artifact: {entry.Relation.Path}");
                Console.WriteLine(
                    $"  command: vslices discovery {DisplayCommandKind(entry.Relation.Kind)} {QuoteArgument(relatedPath)}");
            }
            else
            {
                Console.WriteLine("  status: available");
                var fromFlag = expectedKind.Equals("nexus", StringComparison.Ordinal)
                    ? "--from-nexus"
                    : "--from-path";
                Console.WriteLine(
                    $"  command: vslices new {DisplayCommandKind(recommendation.Family)} {fromFlag} {QuoteArgument(artifact + ":" + entry.SelectionPath)}");
            }
        }

        if (entries.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("  no open questions or recommendations");
        }

        return 0;
    }

    private static string DisplayCommandKind(string kind) =>
        kind switch
        {
            "continuity-path" => "continuity-path",
            "document" => "document",
            "nexus" => "nexus",
            _ => kind
        };

    private static string QuoteArgument(string value)
    {
        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
            return value;

        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private sealed record DiscoveryEntry(
        string SelectionPath,
        string Title,
        string Kind,
        RelationalQuestionSurface? Question,
        RelationalRecommendation? Recommendation,
        ArtifactRelation? Relation)
    {
        public static DiscoveryEntry Question(RelationalQuestionSurface question) =>
            new(
                question.SelectionPath,
                question.Text,
                "question",
                question,
                null,
                null);

        public static DiscoveryEntry Recommendation(
            string selectionPath,
            RelationalRecommendation recommendation,
            ArtifactRelation? relation) =>
            new(
                selectionPath,
                $"{recommendation.Family}: {recommendation.Type}",
                "recommendation",
                null,
                recommendation,
                relation);
    }

    private sealed class SelectionPathComparer : IComparer<string>
    {
        public static SelectionPathComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;

            var a = left.Split('.').Select(ParseSegment).ToArray();
            var b = right.Split('.').Select(ParseSegment).ToArray();
            var length = Math.Min(a.Length, b.Length);
            for (var index = 0; index < length; index++)
            {
                var comparison = a[index].CompareTo(b[index]);
                if (comparison != 0)
                    return comparison;
            }

            return a.Length.CompareTo(b.Length);
        }

        private static int ParseSegment(string value) =>
            int.TryParse(value, out var parsed) ? parsed : int.MaxValue;
    }
}
