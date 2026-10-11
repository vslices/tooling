using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class RelationalUpdateCommands
{
    public static Task<int> Nexus(
        [Argument] string artifact,
        string? questionId = null,
        string? answer = null,
        string? tags = null,
        string? addTags = null,
        string? removeTags = null,
        CancellationToken cancellationToken = default) =>
        Update(artifact, "nexus", questionId, answer, tags, addTags, removeTags, cancellationToken);

    public static Task<int> ContinuityPath(
        [Argument] string artifact,
        string? questionId = null,
        string? answer = null,
        string? tags = null,
        string? addTags = null,
        string? removeTags = null,
        CancellationToken cancellationToken = default) =>
        Update(artifact, "continuity-path", questionId, answer, tags, addTags, removeTags, cancellationToken);

    private static async Task<int> Update(
        string artifact,
        string expectedKind,
        string? questionId,
        string? answer,
        string? tags,
        string? addTags,
        string? removeTags,
        CancellationToken cancellationToken)
    {
        var hasQuestionMutation = questionId is not null || answer is not null;
        var hasTagMutation = KnowledgeArtifactTags.HasMutation(tags, addTags, removeTags);

        if (!hasQuestionMutation && !hasTagMutation)
        {
            TerminalOutput.Error(
                $"RELUPD001: Update a {DisplayKind(expectedKind)} with --question-id/--answer or searchable tag metadata.");
            return 2;
        }

        if (hasQuestionMutation && string.IsNullOrWhiteSpace(questionId))
        {
            TerminalOutput.Error("RELUPD001: --question-id <path> is required when --answer is supplied.");
            return 2;
        }

        if (hasQuestionMutation && string.IsNullOrWhiteSpace(answer))
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

        var updated = source;
        if (hasQuestionMutation)
        {
            updated = RelationalArtifact.UpdateQuestion(
                state.State!,
                questionId!,
                answer!,
                out var updateError);
            if (updateError is not null)
            {
                TerminalOutput.Error(updateError);
                return 2;
            }
        }

        if (hasTagMutation)
        {
            var tagged = KnowledgeArtifactTags.Apply(updated, tags, addTags, removeTags);
            if (!tagged.IsSuccess)
            {
                TerminalOutput.Error(tagged.Error!);
                return 2;
            }

            updated = tagged.Source!;
        }

        await CommandInfrastructure.AtomicWrite(path, updated, cancellationToken);
        Console.WriteLine($"Updated {DisplayKind(expectedKind)} '{path}'.");
        return 0;
    }

    private static string DisplayKind(string kind) =>
        kind.Equals("continuity-path", StringComparison.Ordinal)
            ? "Continuity Path"
            : "Nexus";
}
