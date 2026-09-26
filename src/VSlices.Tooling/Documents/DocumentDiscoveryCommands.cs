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
        Console.WriteLine();
        Console.WriteLine("Current document surface:");

        var answerLabels = artifact.Surface
            .Where(question => question.AnswerInstanceId is not null)
            .ToDictionary(
                question => question.AnswerInstanceId!,
                question => question.AnswerPreview,
                StringComparer.Ordinal);

        var scopedChildren = artifact.Surface
            .Where(question => question.ScopeAnswerInstanceId is not null)
            .GroupBy(question => question.ScopeAnswerInstanceId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(question => question.Selection).ToArray(),
                StringComparer.Ordinal);

        foreach (var question in artifact.Surface)
        {
            Console.WriteLine();
            Console.WriteLine($"[{question.Selection}] {question.Text}");
            Console.WriteLine($"  status: {DisplayStatus(question)}");
            Console.WriteLine($"  cardinality: {DisplayCardinality(question.Cardinality)}");
            if (question.Cardinality == DocumentQuestionCardinality.Many &&
                question.AnswerInstanceId is not null)
            {
                Console.WriteLine("  answer:");
                Console.WriteLine($"    instance: {question.AnswerInstanceId}");
                Console.WriteLine($"    text: {question.AnswerPreview}");
                if (scopedChildren.TryGetValue(question.AnswerInstanceId, out var children) &&
                    children.Length > 0)
                {
                    Console.WriteLine($"    sub-questions: [{string.Join(", ", children)}]");
                }
                Console.WriteLine("  action: repeated AnswerInstance editing is not supported in the current preview");
            }
            else
            {
                if (question.ScopeAnswerInstanceId is not null)
                {
                    Console.WriteLine("  from:");
                    Console.WriteLine($"    instance: {question.ScopeAnswerInstanceId}");
                    if (answerLabels.TryGetValue(question.ScopeAnswerInstanceId, out var scopeAnswer) &&
                        !string.IsNullOrWhiteSpace(scopeAnswer))
                    {
                        Console.WriteLine($"    text: {scopeAnswer}");
                    }
                }

                Console.WriteLine(
                    $"  command: vslices update document {QuoteArgument(document)} --question-id {question.Selection} --answer \"<answer>\"");

                if (question.ScopeAnswerInstanceId is not null &&
                    question.IsAnswered &&
                    question.HasChildren)
                {
                    Console.WriteLine(
                        "  children: deeper scoped authoring is not supported in the current preview");
                }
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
