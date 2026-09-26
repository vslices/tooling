namespace VSlices.Tooling;

internal static class RelationalUpdateCommands
{
    public static Task<int> Nexus(
        string artifact,
        string? questionId = null,
        string? answer = null,
        CancellationToken cancellationToken = default) =>
        Update(artifact, "nexus", questionId, answer, cancellationToken);

    public static Task<int> ContinuityPath(
        string artifact,
        string? questionId = null,
        string? answer = null,
        CancellationToken cancellationToken = default) =>
        Update(artifact, "continuity-path", questionId, answer, cancellationToken);

    private static async Task<int> Update(
        string artifact,
        string expectedKind,
        string? questionId,
        string? answer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(questionId))
        {
            TerminalOutput.Error(
                $"RELUPD001: --question-id <path> is required when updating a {DisplayKind(expectedKind)}.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            TerminalOutput.Error("RELUPD002: --answer must contain non-whitespace text.");
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

        var path = existing.Resolution!.Path;
        var metadata = existing.Resolution.Metadata;
        if (!metadata.Kind.Equals(expectedKind, StringComparison.Ordinal))
        {
            TerminalOutput.Error(
                $"RELUPD003: '{path}' is artifact.kind '{metadata.Kind}', not '{expectedKind}'.");
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
        if (expectedKind.Equals("nexus", StringComparison.Ordinal))
        {
            if (!standards.Relational.TryGetNexus(metadata.Type, out var definition) || definition is null)
            {
                TerminalOutput.Error(
                    $"RELUPD004: Nexus type '{metadata.Type}' is not defined by the installed Docs Standard candidate surface.");
                return 2;
            }

            state = RelationalArtifact.ReadNexus(source, definition);
        }
        else
        {
            if (!standards.Relational.TryGetContinuityPath(metadata.Type, out var definition) || definition is null)
            {
                TerminalOutput.Error(
                    $"RELUPD005: Continuity Path type '{metadata.Type}' is not defined by the installed Docs Standard candidate surface.");
                return 2;
            }

            state = RelationalArtifact.ReadContinuityPath(source, definition);
        }

        if (!state.IsSuccess)
        {
            TerminalOutput.Error(state.Error!);
            return 2;
        }

        var updated = RelationalArtifact.UpdateQuestion(
            state.State!,
            questionId,
            answer,
            out var updateError);
        if (updateError is not null)
        {
            TerminalOutput.Error(updateError);
            return 2;
        }

        await CommandInfrastructure.AtomicWrite(path, updated, cancellationToken);
        Console.WriteLine(
            $"Updated question [{questionId}] in '{path}'.");
        return 0;
    }

    private static string DisplayKind(string kind) =>
        kind.Equals("continuity-path", StringComparison.Ordinal)
            ? "Continuity Path"
            : "Nexus";
}
