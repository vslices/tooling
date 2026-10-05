using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class SupportNoteDiscoveryCommands
{
    public static Task<int> Discover(
        [Argument] string artifact,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(
            artifact,
            Environment.CurrentDirectory);
        if (!existing.IsSuccess)
            return Task.FromResult(Fail(existing.Error!, 1));

        var resolved = existing.Resolution!;
        if (!resolved.Metadata.Kind.Equals("support-note", StringComparison.Ordinal))
            return Task.FromResult(Fail(
                $"SUPDISC001: '{resolved.Path}' is artifact.kind '{resolved.Metadata.Kind}', not 'support-note'."));

        var standardsResult = KnowledgeArtifactCommandSupport.LoadStandards(resolved.Path);
        if (!standardsResult.IsSuccess)
            return Task.FromResult(Fail(standardsResult.Error!, 1));

        if (!standardsResult.Standards!.SupportNotes.TryGetSupportNote(
                resolved.Metadata.Type,
                out var definition) ||
            definition is null)
        {
            return Task.FromResult(Fail(
                $"SUPDISC002: Support Note type '{resolved.Metadata.Type}' is not defined by the installed Docs Standard."));
        }

        var state = SupportNoteArtifact.Read(resolved.Source, definition);
        if (!state.IsSuccess)
            return Task.FromResult(Fail(state.Error!));

        Console.WriteLine("Support Note:");
        Console.WriteLine($"  path: {Path.GetRelativePath(Environment.CurrentDirectory, resolved.Path)}");
        Console.WriteLine($"  type: {resolved.Metadata.Type}");
        Console.WriteLine($"  scope: {resolved.Metadata.Scope ?? "<unset>"}");
        Console.WriteLine($"  target: {resolved.Metadata.Target ?? "<unset>"}");
        Console.WriteLine($"  status: {resolved.Metadata.Status}");
        Console.WriteLine($"  tooling: {resolved.Metadata.ToolingVersion}");
        Console.WriteLine();
        Console.WriteLine("Discovery surface:");

        foreach (var question in state.State!.Questions)
        {
            Console.WriteLine();
            Console.WriteLine($"[{question.SelectionPath}] {question.Text}");
            Console.WriteLine("  kind: question");
            Console.WriteLine($"  status: {(question.IsAnswered ? "answered" : "available")}");
            if (question.ParentSelectionPath is not null)
                Console.WriteLine($"  parent: [{question.ParentSelectionPath}]");
            if (question.IsAnswered)
                Console.WriteLine($"  answer: {question.Answer}");
            Console.WriteLine(
                $"  command: vslices update support-note {QuoteArgument(artifact)} --question-id {question.SelectionPath} --answer \"<answer>\"");
        }

        Console.WriteLine();
        Console.WriteLine("Artifacts asociados:");
        if (resolved.Metadata.Relations.Count == 0)
            Console.WriteLine("  none");

        foreach (var relation in resolved.Metadata.Relations)
        {
            Console.WriteLine($"  artifact: {relation.Path}");
            Console.WriteLine($"    kind: {relation.Kind}");
            Console.WriteLine($"    role: {relation.Role}");
            Console.WriteLine(
                $"    command: vslices discovery {relation.Kind} {QuoteArgument(KnowledgeArtifactCommandSupport.ResolveRelationTargetPath(resolved.Path, relation.Path))}");
        }

        return Task.FromResult(0);
    }

    private static int Fail(string error, int exitCode = 2)
    {
        TerminalOutput.Error(error);
        return exitCode;
    }

    private static string QuoteArgument(string value) =>
        !value.Any(char.IsWhiteSpace) && !value.Contains('"')
            ? value
            : "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
