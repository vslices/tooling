using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class RelationalDiscoveryCommands
{
    public static Task<int> Nexus([Argument] string artifact, CancellationToken cancellationToken = default) =>
        Task.FromResult(Discover(artifact, "nexus", cancellationToken));
    public static Task<int> ContinuityPath([Argument] string artifact, CancellationToken cancellationToken = default) =>
        Task.FromResult(Discover(artifact, "continuity-path", cancellationToken));

    private static int Discover(string artifact, string expectedKind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(artifact, Environment.CurrentDirectory);
        if (!existing.IsSuccess) return Fail(existing.Error!, 1);
        var resolved = existing.Resolution!;
        var path = resolved.Path;
        var metadata = resolved.Metadata;
        if (metadata.Kind != expectedKind)
            return Fail($"RELDISC001: '{path}' is artifact.kind '{metadata.Kind}', not '{expectedKind}'.");
        var loaded = KnowledgeArtifactCommandSupport.LoadStandards(path);
        if (!loaded.IsSuccess) return Fail(loaded.Error!, 1);
        var standards = loaded.Standards!;

        RelationalArtifactStateResult state;
        IReadOnlyList<(string Id, RelationalRecommendation Recommendation)> recommendations;
        string context;
        string? graph = null;
        if (expectedKind == "nexus")
        {
            if (!standards.Relational.TryGetNexus(metadata.Type, out var definition) || definition is null)
                return Fail($"RELDISC002: Nexus type '{metadata.Type}' is not defined by the installed candidate surface.");
            state = RelationalArtifact.ReadNexus(resolved.Source, definition);
            if (!state.IsSuccess) return Fail(state.Error!);
            recommendations = RelationalArtifact.EnumerateRecommendations(definition, state.State!);
            context = RelationalArtifact.RecommendationContext(definition);
        }
        else
        {
            if (!standards.Relational.TryGetContinuityPath(metadata.Type, out var definition) || definition is null)
                return Fail($"RELDISC003: Continuity Path type '{metadata.Type}' is not defined by the installed candidate surface.");
            state = RelationalArtifact.ReadContinuityPath(resolved.Source, definition);
            if (!state.IsSuccess) return Fail(state.Error!);
            recommendations = RelationalArtifact.EnumerateRecommendations(definition, state.State!);
            context = RelationalArtifact.RecommendationContext(definition);
            graph = RelationalArtifact.RenderContinuityGraph(definition);
        }
        var contextError = RelationalArtifact.ValidateRecommendationContext(metadata, context);
        if (contextError is not null) return Fail(contextError);
        foreach (var (id, recommendation) in recommendations)
        {
            var relation = metadata.Relations.FirstOrDefault(candidate => candidate.RecommendationId == id);
            if (relation is not null && (relation.Kind != recommendation.Family || relation.Type != recommendation.Type))
                return Fail($"RELDISC004: Association at selection '{id}' conflicts with its recommendation family/type.");
        }

        Console.WriteLine(expectedKind == "nexus" ? "Nexus:" : "Continuity Path:");
        Console.WriteLine($"  path: {Path.GetRelativePath(Environment.CurrentDirectory, path)}");
        Console.WriteLine($"  type: {metadata.Type}");
        Console.WriteLine($"  scope: {metadata.Scope ?? "<unset>"}");
        Console.WriteLine($"  target: {metadata.Target ?? "<unset>"}");
        Console.WriteLine($"  status: {metadata.Status}");
        Console.WriteLine($"  tooling: {metadata.ToolingVersion}");
        if (graph is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Continuity graph (current definition):");
            Console.Write(graph);
        }
        Console.WriteLine();
        Console.WriteLine("Discovery surface:");
        var entries = new List<DiscoveryEntry>();
        entries.AddRange(state.State!.Questions.Select(question => new DiscoveryEntry(question.SelectionPath, question, null, null)));
        entries.AddRange(recommendations.Select(item => new DiscoveryEntry(item.Id, null, item.Recommendation,
            metadata.Relations.FirstOrDefault(relation => relation.RecommendationId == item.Id))));
        foreach (var entry in entries.OrderBy(entry => entry.SelectionPath, SelectionPathComparer.Instance))
        {
            Console.WriteLine();
            if (entry.Question is { } question)
            {
                Console.WriteLine($"[{entry.SelectionPath}] {question.Text}");
                Console.WriteLine("  kind: question");
                Console.WriteLine($"  status: {(question.IsAnswered ? "answered" : "available")}");
                if (question.ParentSelectionPath is not null) Console.WriteLine($"  parent: [{question.ParentSelectionPath}]");
                if (question.Connection is not null) Console.WriteLine($"  connection: {question.Connection}");
                if (question.IsAnswered) Console.WriteLine($"  answer: {question.Answer}");
                Console.WriteLine($"  command: vslices update {expectedKind} {QuoteArgument(artifact)} --question-id {question.SelectionPath} --answer \"<answer>\"");
                continue;
            }
            var recommendation = entry.Recommendation!;
            Console.WriteLine($"[{entry.SelectionPath}] {recommendation.Family}: {recommendation.Type}");
            Console.WriteLine("  kind: recommendation");
            Console.WriteLine($"  recommends: {recommendation.Family} {recommendation.Type}");
            Console.WriteLine($"  role: {entry.Relation?.Role ?? recommendation.Role}");
            if (recommendation.QuestionId is not null)
            {
                var parent = state.State.Questions.First(question => question.Id == recommendation.QuestionId);
                Console.WriteLine($"  parent: [{parent.SelectionPath}] {parent.Text}");
            }
            if (entry.Relation is { } relation)
            {
                Console.WriteLine("  status: created");
                Console.WriteLine($"  artifact: {relation.Path}");
                Console.WriteLine($"  command: vslices discovery {relation.Kind} {QuoteArgument(KnowledgeArtifactCommandSupport.ResolveRelationTargetPath(path, relation.Path))}");
            }
            else
            {
                Console.WriteLine("  status: available");
                var flag = expectedKind == "nexus" ? "--from-nexus" : "--from-path";
                Console.WriteLine($"  command: vslices new {recommendation.Family} {flag} {QuoteArgument(artifact + ":" + entry.SelectionPath)}");
            }
        }
        if (entries.Count == 0) Console.WriteLine("  no open questions or recommendations");

        // Associations are open-world and visible independently of recommendation slots.
        Console.WriteLine();
        Console.WriteLine("Artifacts asociados:");
        if (metadata.Relations.Count == 0) Console.WriteLine("  none");
        foreach (var relation in metadata.Relations)
        {
            Console.WriteLine($"  artifact: {relation.Path}");
            Console.WriteLine($"    kind: {relation.Kind}");
            Console.WriteLine($"    role: {relation.Role}");
            Console.WriteLine($"    command: vslices discovery {relation.Kind} {QuoteArgument(KnowledgeArtifactCommandSupport.ResolveRelationTargetPath(path, relation.Path))}");
        }
        return 0;
    }

    private static int Fail(string error, int exitCode = 2) { TerminalOutput.Error(error); return exitCode; }
    private static string QuoteArgument(string value) => !value.Any(char.IsWhiteSpace) && !value.Contains('"')
        ? value
        : "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    private sealed record DiscoveryEntry(string SelectionPath, RelationalQuestionSurface? Question, RelationalRecommendation? Recommendation, ArtifactRelation? Relation);
    private sealed class SelectionPathComparer : IComparer<string>
    {
        public static SelectionPathComparer Instance { get; } = new();
        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            var a = left.Split('.').Select(int.Parse).ToArray();
            var b = right.Split('.').Select(int.Parse).ToArray();
            for (var index = 0; index < Math.Min(a.Length, b.Length); index++)
            {
                var compared = a[index].CompareTo(b[index]);
                if (compared != 0) return compared;
            }
            return a.Length.CompareTo(b.Length);
        }
    }
}
