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
        var path = Path.GetFullPath(
            document.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? document
                : document + ".md",
            Environment.CurrentDirectory);
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

        foreach (var question in artifact.Surface)
        {
            Console.WriteLine();
            Console.WriteLine($"[{question.Selection}] {question.Text}");
            Console.WriteLine($"  status: {DisplayStatus(question)}");
            Console.WriteLine($"  cardinality: {DisplayCardinality(question.Cardinality)}");
            if (question.Cardinality == DocumentQuestionCardinality.Many)
            {
                Console.WriteLine("  action: multiple-answer authoring is not supported in the current preview");
            }
            else
            {
                Console.WriteLine(
                    $"  command: vslices update document {QuoteArgument(document)} --question-id {question.Selection} --answer \"<answer>\"");
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
