using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class DocumentDiscoveryCommands
{
    /// <summary>Shows the current writable question surface for a progressive Docs Standard Document.</summary>
    /// <param name="document">Document name or path. The .md extension is added when omitted.</param>
    public static async Task<int> Discover(
        [Argument] string document,
        CancellationToken cancellationToken = default)
    {
        var resolved = DocumentPathResolver.Resolve(
            document,
            Environment.CurrentDirectory);
        if (!resolved.IsSuccess)
        {
            TerminalOutput.Error(resolved.Error!);
            return 2;
        }

        var path = resolved.Path!;
        if (!File.Exists(path))
        {
            TerminalOutput.Error($"DISC100: Document '{path}' does not exist.");
            return 1;
        }

        var standardRoot = DocsStandardCatalog.FindInstalledRoot(path);
        if (standardRoot is null)
        {
            TerminalOutput.Error(
                "DISC101: Could not locate an installed Docs Standard snapshot at .vslices/docs-standard. Run 'vslices update docs-standard' to install it.");
            return 1;
        }

        var catalog = DocsStandardCatalog.Load(standardRoot);
        if (!catalog.IsSuccess)
        {
            TerminalOutput.Error(catalog.Error!);
            return 1;
        }

        var materialization = DocumentMaterializationEnvironment.Resolve(path);
        if (!materialization.IsSuccess)
        {
            TerminalOutput.Error(materialization.Error!);
            return 1;
        }

        var source = await File.ReadAllTextAsync(path, cancellationToken);
        KnowledgeArtifactMetadata? metadata = null;
        if (source.TrimStart().StartsWith("---", StringComparison.Ordinal))
        {
            var metadataRead = KnowledgeArtifactFrontMatter.Read(source);
            if (!metadataRead.IsSuccess)
            {
                TerminalOutput.Error(metadataRead.Error!);
                return 2;
            }

            metadata = metadataRead.Metadata;
        }

        var state = DocumentArtifact.Read(
            source,
            catalog.Catalog!,
            materialization.Template!);
        if (!state.IsSuccess)
        {
            TerminalOutput.Error(state.Error!);
            return 2;
        }

        var artifact = state.Artifact!;
        Console.WriteLine("Document:");
        Console.WriteLine($"  path: {Path.GetRelativePath(Environment.CurrentDirectory, path)}");
        Console.WriteLine($"  type: {artifact.DocumentType}");
        Console.WriteLine($"  scope: {metadata?.Scope ?? "<unset>"}");
        Console.WriteLine($"  target: {metadata?.Target ?? "<unset>"}");
        Console.WriteLine($"  status: {metadata?.Status ?? "<legacy>"}");
        Console.WriteLine($"  tooling: {metadata?.ToolingVersion ?? "<legacy>"}");
        if (metadata is { Relations.Count: > 0 })
        {
            Console.WriteLine("  relations:");
            foreach (var relation in metadata.Relations)
                Console.WriteLine($"    - {relation.Relation}: {relation.Path} ({relation.Role})");
        }
        Console.WriteLine();
        Console.WriteLine("Current document surface:");

        var answerLabels = artifact.Surface
            .Where(question => question.AnswerInstanceId is not null)
            .ToDictionary(
                question => question.AnswerInstanceId!,
                question => question.AnswerPreview,
                StringComparer.Ordinal);

        var questionsBySelection = artifact.Surface
            .ToDictionary(question => question.Selection);

        var directChildren = artifact.Surface
            .Where(question => question.ParentSelection is not null)
            .GroupBy(question => question.ParentSelection!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(question => question.SelectionPath).ToArray());

        foreach (var question in artifact.Surface)
        {
            Console.WriteLine();
            Console.WriteLine($"[{question.SelectionPath}] {question.Text}");
            Console.WriteLine($"  status: {DisplayStatus(question)}");
            Console.WriteLine($"  cardinality: {DisplayCardinality(question.Cardinality)}");

            if (question.Cardinality == DocumentQuestionCardinality.Many &&
                question.AnswerInstanceId is not null)
            {
                Console.WriteLine("  answer:");
                Console.WriteLine($"    instance: {question.AnswerInstanceId}");
                Console.WriteLine($"    text: {question.AnswerPreview}");
            }

            if (question.ParentSelection is int parentSelection &&
                questionsBySelection.TryGetValue(parentSelection, out var parent))
            {
                Console.WriteLine("  parent:");
                Console.WriteLine($"    question: [{parent.SelectionPath}] {parent.Text}");
            }

            if (question.ScopeAnswerInstanceId is not null)
            {
                Console.WriteLine("  scope:");
                Console.WriteLine($"    instance: {question.ScopeAnswerInstanceId}");
                if (answerLabels.TryGetValue(question.ScopeAnswerInstanceId, out var scopeAnswer) &&
                    !string.IsNullOrWhiteSpace(scopeAnswer))
                {
                    Console.WriteLine($"    text: {scopeAnswer}");
                }
            }

            if (directChildren.TryGetValue(question.Selection, out var children) &&
                children.Length > 0)
            {
                Console.WriteLine($"  sub-questions: [{string.Join(", ", children)}]");
            }

            if (question.Cardinality == DocumentQuestionCardinality.Many &&
                question.AnswerInstanceId is not null)
            {
                Console.WriteLine("  action: repeated AnswerInstance editing is not supported in the current preview");
            }
            else
            {
                Console.WriteLine(
                    $"  command: vslices update document {QuoteArgument(document)} --question-id {question.SelectionPath} --answer \"<answer>\"");
            }
        }

        return 0;
    }

    private static string DisplayCardinality(DocumentQuestionCardinality cardinality) =>
        cardinality == DocumentQuestionCardinality.Many
            ? "many"
            : "one";

    private static string DisplayStatus(DocumentQuestionAffordance question)
    {
        if (question.IsAnswered)
            return "answered";

        return question.IsMaterialized
            ? "unanswered"
            : "available";
    }

    private static string QuoteArgument(string value)
    {
        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
            return value;

        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
