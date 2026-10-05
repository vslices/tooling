using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class SupportNoteUpdateCommands
{
    public static async Task<int> Update(
        [Argument] string artifact,
        string? questionId = null,
        string? answer = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(questionId))
        {
            TerminalOutput.Error(
                "SUPUPD001: --question-id <path> is required when updating a Support Note.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            TerminalOutput.Error("SUPUPD002: --answer must contain non-whitespace text.");
            return 2;
        }

        var existing = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(
            artifact,
            Environment.CurrentDirectory);
        if (!existing.IsSuccess)
        {
            TerminalOutput.Error(existing.Error!);
            return 1;
        }

        var resolved = existing.Resolution!;
        if (!resolved.Metadata.Kind.Equals("support-note", StringComparison.Ordinal))
        {
            TerminalOutput.Error(
                $"SUPUPD003: '{resolved.Path}' is artifact.kind '{resolved.Metadata.Kind}', not 'support-note'.");
            return 2;
        }

        var standardsResult = KnowledgeArtifactCommandSupport.LoadStandards(resolved.Path);
        if (!standardsResult.IsSuccess)
        {
            TerminalOutput.Error(standardsResult.Error!);
            return 1;
        }

        if (!standardsResult.Standards!.SupportNotes.TryGetSupportNote(
                resolved.Metadata.Type,
                out var definition) ||
            definition is null)
        {
            TerminalOutput.Error(
                $"SUPUPD004: Support Note type '{resolved.Metadata.Type}' is not defined by the installed Docs Standard.");
            return 2;
        }

        var source = await File.ReadAllTextAsync(resolved.Path, cancellationToken);
        var state = SupportNoteArtifact.Read(source, definition);
        if (!state.IsSuccess)
        {
            TerminalOutput.Error(state.Error!);
            return 2;
        }

        var updated = SupportNoteArtifact.UpdateQuestion(
            state.State!,
            questionId,
            answer,
            out var updateError);
        if (updateError is not null)
        {
            TerminalOutput.Error(updateError);
            return 2;
        }

        await CommandInfrastructure.AtomicWrite(resolved.Path, updated, cancellationToken);
        Console.WriteLine($"Updated question [{questionId}] in '{resolved.Path}'.");
        return 0;
    }
}
