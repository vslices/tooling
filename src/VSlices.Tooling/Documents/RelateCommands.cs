using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class RelateCommands
{
    /// <summary>Relates two existing documentary artifacts without recreating them.</summary>
    /// <param name="artifact">Existing source artifact name or path.</param>
    /// <param name="to">Existing destination artifact name or path.</param>
    /// <param name="role">Human-readable purpose of this relationship.</param>
    public static async Task<int> Relate(
        [Argument] string artifact,
        string? to = null,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(role))
        {
            TerminalOutput.Error("RELATE001: --to <artifact> and --role <text> are required.");
            return 2;
        }

        var start = Environment.CurrentDirectory;
        var sourceResult = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(artifact, start);
        if (!sourceResult.IsSuccess) return Fail(sourceResult.Error!, 1);
        var targetResult = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(to, start);
        if (!targetResult.IsSuccess) return Fail(targetResult.Error!, 1);
        var source = sourceResult.Resolution!;
        var target = targetResult.Resolution!;
        if (KnowledgeArtifactFrontMatter.PathComparer.Equals(
                Path.GetFullPath(source.Path), Path.GetFullPath(target.Path)))
            return Fail("RELATE003: An artifact cannot be related to itself.");

        var forward = KnowledgeArtifactCommandSupport.RelationFrom(
            source.Path, target.Path, target.Metadata.Kind, target.Metadata.Type, role.Trim(), null);
        var backward = KnowledgeArtifactCommandSupport.RelationFrom(
            target.Path, source.Path, source.Metadata.Kind, source.Metadata.Type, role.Trim(), null);
        if (source.Metadata.Relations.Any(r =>
                KnowledgeArtifactFrontMatter.PathComparer.Equals(r.Path, forward.Path)) ||
            target.Metadata.Relations.Any(r =>
                KnowledgeArtifactFrontMatter.PathComparer.Equals(r.Path, backward.Path)))
            return Fail("RELATE004: An existing relationship already connects these artifacts. No files were modified.");

        var updatedSource = AddRelation(source, forward, out var sourceError);
        if (sourceError is not null) return Fail(sourceError);
        var updatedTarget = AddRelation(target, backward, out var targetError);
        if (targetError is not null) return Fail(targetError);
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
            Console.WriteLine($"Related artifacts '{source.Path}' and '{target.Path}'.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return Fail($"RELATE006: Could not relate artifacts: {ex.Message}", 1);
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static string AddRelation(ExistingArtifactResolution artifact, ArtifactRelation relation, out string? error)
    {
        if (artifact.Metadata.Kind is "nexus" or "continuity-path")
            return RelationalArtifact.AddRelation(artifact.Source, relation, out error);
        error = null;
        return KnowledgeArtifactFrontMatter.WithMetadata(artifact.Source, artifact.Metadata,
            [.. artifact.Metadata.Relations, relation]);
    }

    private static int Fail(string message, int exitCode = 2)
    {
        TerminalOutput.Error(message);
        return exitCode;
    }
}
