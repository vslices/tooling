namespace VSlices.Tooling;

internal static class KnowledgeArtifactWriter
{
    public static async Task<string?> Create(
        string path, string source, ExistingArtifactResolution? related,
        ArtifactRelation? reciprocal, CancellationToken cancellationToken)
    {
        if (File.Exists(path) || Directory.Exists(path))
            return $"RELWRITE001: '{path}' already exists; artifact creation never overwrites it.";
        var createdMetadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!createdMetadata.IsSuccess) return createdMetadata.Error;
        if (createdMetadata.Metadata!.Kind != "document")
        {
            var standards = KnowledgeArtifactCommandSupport.LoadStandards(Environment.CurrentDirectory);
            if (!standards.IsSuccess) return standards.Error;
            RelationalArtifactStateResult reconstructed;
            if (createdMetadata.Metadata.Kind == "nexus" && standards.Standards!.Relational.TryGetNexus(createdMetadata.Metadata.Type, out var nexus) && nexus is not null)
                reconstructed = RelationalArtifact.ReadNexus(source, nexus);
            else if (createdMetadata.Metadata.Kind == "continuity-path" && standards.Standards!.Relational.TryGetContinuityPath(createdMetadata.Metadata.Type, out var pathDefinition) && pathDefinition is not null)
                reconstructed = RelationalArtifact.ReadContinuityPath(source, pathDefinition);
            else return "RELWRITE005: The materialized artifact cannot be reconstructed against its installed definition.";
            if (!reconstructed.IsSuccess) return reconstructed.Error;
        }

        string? relatedSource = null;
        if (related is not null && reciprocal is not null)
        {
            if (related.Metadata.Kind is "nexus" or "continuity-path")
            {
                relatedSource = RelationalArtifact.AddRelation(related.Source, reciprocal, out var error);
                if (error is not null) return error;
            }
            else
            {
                var relations = related.Metadata.Relations.ToList();
                if (relations.Any(relation => KnowledgeArtifactFrontMatter.PathComparer.Equals(relation.Path, reciprocal.Path)))
                    return "RELWRITE002: The reciprocal relation already exists. No files were modified.";
                relations.Add(reciprocal);
                relatedSource = KnowledgeArtifactFrontMatter.WithMetadata(related.Source, related.Metadata, relations);
            }
            var relatedMetadata = KnowledgeArtifactFrontMatter.Read(relatedSource);
            if (!relatedMetadata.IsSuccess) return relatedMetadata.Error;
        }

        var staging = Path.Combine(Path.GetTempPath(), "vslices-artifact-write-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            var changes = new List<TransactionalFileChange>();
            if (related is not null && relatedSource is not null)
            {
                var relatedStage = Path.Combine(staging, "related.md");
                await File.WriteAllTextAsync(relatedStage, relatedSource, cancellationToken);
                changes.Add(new(related.Path, relatedStage, true, related.Sha256));
            }
            var createdStage = Path.Combine(staging, "created.md");
            await File.WriteAllTextAsync(createdStage, source, cancellationToken);
            changes.Add(new(path, createdStage, false, null));
            var result = await TransactionalFileWriter.Apply(changes, cancellationToken);
            return result.Success ? null : "RELWRITE003: " + result.Error;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return $"RELWRITE004: Artifact creation could not complete: {ex.Message}";
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A staging cleanup failure must not reinterpret the transaction result.
            }
        }
    }
}
