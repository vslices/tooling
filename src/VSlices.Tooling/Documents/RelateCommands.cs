using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class RelateCommands
{
    /// <summary>Relates two existing Support Notes without recreating either artifact.</summary>
    /// <param name="artifact">Existing source Support Note name or path.</param>
    /// <param name="to">Existing destination Support Note name or path.</param>
    /// <param name="role">Human-readable purpose of this relationship.</param>
    public static async Task<int> SupportNote(
        [Argument] string artifact,
        string? to = null,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(role))
        {
            TerminalOutput.Error("RELATE001: --to <support-note> and --role <text> are required.");
            return 2;
        }

        var start = Environment.CurrentDirectory;
        var sourceResult = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(artifact, start);
        if (!sourceResult.IsSuccess) return Fail(sourceResult.Error!, 1);
        var targetResult = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(to, start);
        if (!targetResult.IsSuccess) return Fail(targetResult.Error!, 1);
        var source = sourceResult.Resolution!;
        var target = targetResult.Resolution!;
        if (source.Metadata.Kind != "support-note" || target.Metadata.Kind != "support-note")
            return Fail("RELATE002: Both artifacts must be Support Notes.");
        if (KnowledgeArtifactFrontMatter.PathComparer.Equals(
                Path.GetFullPath(source.Path), Path.GetFullPath(target.Path)))
            return Fail("RELATE003: A Support Note cannot be related to itself.");

        var forward = KnowledgeArtifactCommandSupport.RelationFrom(
            source.Path, target.Path, target.Metadata.Kind, target.Metadata.Type, role.Trim(), null);
        var backward = KnowledgeArtifactCommandSupport.RelationFrom(
            target.Path, source.Path, source.Metadata.Kind, source.Metadata.Type, role.Trim(), null);
        if (source.Metadata.Relations.Any(r =>
                KnowledgeArtifactFrontMatter.PathComparer.Equals(r.Path, forward.Path)) ||
            target.Metadata.Relations.Any(r =>
                KnowledgeArtifactFrontMatter.PathComparer.Equals(r.Path, backward.Path)))
            return Fail("RELATE004: An existing relationship already connects these artifacts. No files were modified.");

        var updatedSource = KnowledgeArtifactFrontMatter.WithMetadata(
            source.Source, source.Metadata, [.. source.Metadata.Relations, forward]);
        var updatedTarget = KnowledgeArtifactFrontMatter.WithMetadata(
            target.Source, target.Metadata, [.. target.Metadata.Relations, backward]);
        var sourceValidation = KnowledgeArtifactFrontMatter.Read(updatedSource);
        var targetValidation = KnowledgeArtifactFrontMatter.Read(updatedTarget);
        if (!sourceValidation.IsSuccess) return Fail(sourceValidation.Error!);
        if (!targetValidation.IsSuccess) return Fail(targetValidation.Error!);

        var staging = Path.Combine(Path.GetTempPath(), "vslices-relate-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            var stagedSource = Path.Combine(staging, "source.md");
            var stagedTarget = Path.Combine(staging, "target.md");
            await File.WriteAllTextAsync(stagedSource, updatedSource, cancellationToken);
            await File.WriteAllTextAsync(stagedTarget, updatedTarget, cancellationToken);
            var result = await TransactionalFileWriter.Apply(
                [
                    new TransactionalFileChange(source.Path, stagedSource, true, source.Sha256),
                    new TransactionalFileChange(target.Path, stagedTarget, true, target.Sha256)
                ], cancellationToken);
            if (!result.Success) return Fail("RELATE005: " + result.Error, 1);
            Console.WriteLine($"Related Support Notes '{source.Path}' and '{target.Path}'.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return Fail($"RELATE006: Could not relate Support Notes: {ex.Message}", 1);
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static int Fail(string message, int exitCode = 2)
    {
        TerminalOutput.Error(message);
        return exitCode;
    }
}
