namespace VSlices.Tooling;

internal sealed record KnowledgeStandards(
    string Root,
    DocsStandardCatalog Documents,
    RelationalStandardCatalog Relational);

internal sealed record KnowledgeStandardsResult(
    KnowledgeStandards? Standards,
    string? Error)
{
    public bool IsSuccess => Standards is not null && Error is null;
    public static KnowledgeStandardsResult Success(KnowledgeStandards standards) => new(standards, null);
    public static KnowledgeStandardsResult Failure(string error) => new(null, error);
}

internal sealed record RecommendationResolution(
    string SourcePath,
    string SelectionPath,
    KnowledgeArtifactMetadata SourceMetadata,
    RelationalRecommendation Recommendation,
    RelationalArtifactState SourceState);

internal sealed record RecommendationResolutionResult(
    RecommendationResolution? Resolution,
    string? Error)
{
    public bool IsSuccess => Resolution is not null && Error is null;
    public static RecommendationResolutionResult Success(RecommendationResolution resolution) => new(resolution, null);
    public static RecommendationResolutionResult Failure(string error) => new(null, error);
}

internal sealed record ExistingArtifactResolution(
    string Path,
    KnowledgeArtifactMetadata Metadata);

internal sealed record ExistingArtifactResolutionResult(
    ExistingArtifactResolution? Resolution,
    string? Error)
{
    public bool IsSuccess => Resolution is not null && Error is null;
    public static ExistingArtifactResolutionResult Success(ExistingArtifactResolution resolution) => new(resolution, null);
    public static ExistingArtifactResolutionResult Failure(string error) => new(null, error);
}

internal static class KnowledgeArtifactCommandSupport
{
    public static KnowledgeStandardsResult LoadStandards(string start)
    {
        var root = DocsStandardCatalog.FindInstalledRoot(start);
        if (root is null)
        {
            return KnowledgeStandardsResult.Failure(
                "RELCLI001: Could not locate an installed Docs Standard snapshot at .vslices/docs-standard. Run 'vslices update docs-standard'.");
        }

        var documents = DocsStandardCatalog.Load(root);
        if (!documents.IsSuccess)
            return KnowledgeStandardsResult.Failure(documents.Error!);

        var relational = RelationalStandardCatalog.Load(root);
        if (!relational.IsSuccess)
            return KnowledgeStandardsResult.Failure(relational.Error!);

        return KnowledgeStandardsResult.Success(
            new KnowledgeStandards(root, documents.Catalog!, relational.Catalog!));
    }

    public static ExistingArtifactResolutionResult ResolveExistingArtifact(
        string artifact,
        string start)
    {
        var resolved = DocumentPathResolver.Resolve(artifact, start);
        if (!resolved.IsSuccess)
            return ExistingArtifactResolutionResult.Failure(resolved.Error!);

        var path = resolved.Path!;
        if (!File.Exists(path))
        {
            return ExistingArtifactResolutionResult.Failure(
                $"RELCLI002: Related artifact '{path}' does not exist.");
        }

        var source = File.ReadAllText(path);
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!metadata.IsSuccess)
            return ExistingArtifactResolutionResult.Failure(metadata.Error!);

        return ExistingArtifactResolutionResult.Success(
            new ExistingArtifactResolution(path, metadata.Metadata!));
    }

    public static RecommendationResolutionResult ResolveRecommendation(
        string reference,
        string expectedSourceKind,
        string start,
        KnowledgeStandards standards)
    {
        var parsed = ParseRecommendationReference(reference);
        if (parsed is null)
        {
            return RecommendationResolutionResult.Failure(
                $"RELCLI003: Recommendation reference '{reference}' must use <artifact-path>:<selection-path>.");
        }

        var existing = ResolveExistingArtifact(parsed.Value.Artifact, start);
        if (!existing.IsSuccess)
            return RecommendationResolutionResult.Failure(existing.Error!);

        var sourcePath = existing.Resolution!.Path;
        var metadata = existing.Resolution.Metadata;
        if (!metadata.Kind.Equals(expectedSourceKind, StringComparison.Ordinal))
        {
            return RecommendationResolutionResult.Failure(
                $"RELCLI004: '{sourcePath}' is artifact.kind '{metadata.Kind}', not '{expectedSourceKind}'.");
        }

        var source = File.ReadAllText(sourcePath);

        if (expectedSourceKind.Equals("nexus", StringComparison.Ordinal))
        {
            if (!standards.Relational.TryGetNexus(metadata.Type, out var definition) || definition is null)
            {
                return RecommendationResolutionResult.Failure(
                    $"RELCLI005: Nexus type '{metadata.Type}' is not defined by the installed Docs Standard snapshot.");
            }

            var state = RelationalArtifact.ReadNexus(source, definition);
            if (!state.IsSuccess)
                return RecommendationResolutionResult.Failure(state.Error!);

            var match = RelationalArtifact
                .EnumerateRecommendations(definition, state.State!)
                .FirstOrDefault(candidate => candidate.Id.Equals(parsed.Value.Selection, StringComparison.Ordinal));

            if (match.Recommendation is null)
            {
                return RecommendationResolutionResult.Failure(
                    $"RELCLI006: Nexus recommendation '{parsed.Value.Selection}' is not available on the current discovery surface.");
            }

            if (state.State!.Metadata.Relations.Any(relation =>
                    string.Equals(relation.RecommendationId, parsed.Value.Selection, StringComparison.Ordinal)))
            {
                return RecommendationResolutionResult.Failure(
                    $"RELCLI007: Nexus recommendation '{parsed.Value.Selection}' already has an associated artifact.");
            }

            return RecommendationResolutionResult.Success(
                new RecommendationResolution(
                    sourcePath,
                    parsed.Value.Selection,
                    metadata,
                    match.Recommendation,
                    state.State));
        }

        if (expectedSourceKind.Equals("continuity-path", StringComparison.Ordinal))
        {
            if (!standards.Relational.TryGetContinuityPath(metadata.Type, out var definition) || definition is null)
            {
                return RecommendationResolutionResult.Failure(
                    $"RELCLI008: Continuity Path type '{metadata.Type}' is not defined by the installed Docs Standard snapshot.");
            }

            var state = RelationalArtifact.ReadContinuityPath(source, definition);
            if (!state.IsSuccess)
                return RecommendationResolutionResult.Failure(state.Error!);

            var match = RelationalArtifact
                .EnumerateRecommendations(definition, state.State!)
                .FirstOrDefault(candidate => candidate.Id.Equals(parsed.Value.Selection, StringComparison.Ordinal));

            if (match.Recommendation is null)
            {
                return RecommendationResolutionResult.Failure(
                    $"RELCLI009: Continuity Path recommendation '{parsed.Value.Selection}' is not available on the current discovery surface.");
            }

            if (state.State!.Metadata.Relations.Any(relation =>
                    string.Equals(relation.RecommendationId, parsed.Value.Selection, StringComparison.Ordinal)))
            {
                return RecommendationResolutionResult.Failure(
                    $"RELCLI010: Continuity Path recommendation '{parsed.Value.Selection}' already has an associated artifact.");
            }

            return RecommendationResolutionResult.Success(
                new RecommendationResolution(
                    sourcePath,
                    parsed.Value.Selection,
                    metadata,
                    match.Recommendation,
                    state.State));
        }

        return RecommendationResolutionResult.Failure(
            $"RELCLI011: Unsupported recommendation source kind '{expectedSourceKind}'.");
    }

    public static string ResolveScope(
        string? explicitScope,
        IReadOnlyList<string> admittedScopes,
        string? sourceScope)
    {
        if (!string.IsNullOrWhiteSpace(explicitScope))
            return explicitScope.Trim();

        if (!string.IsNullOrWhiteSpace(sourceScope) &&
            admittedScopes.Contains(sourceScope, StringComparer.Ordinal))
        {
            return sourceScope;
        }

        return admittedScopes.FirstOrDefault()
               ?? sourceScope
               ?? string.Empty;
    }

    public static string SuggestedName(
        string target,
        string type)
    {
        var combined = target + "-" + type;
        var chars = combined
            .Trim()
            .Select(character =>
                char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? type : slug;
    }

    public static ArtifactRelation RelationFrom(
        string ownerPath,
        string relatedPath,
        string relatedKind,
        string relatedType,
        string role,
        string? recommendationId,
        string relation = "related")
    {
        var ownerDirectory = Path.GetDirectoryName(Path.GetFullPath(ownerPath))!;
        var relative = Path.GetRelativePath(ownerDirectory, Path.GetFullPath(relatedPath))
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return new ArtifactRelation(
            relation,
            relative,
            relatedKind,
            relatedType,
            role,
            recommendationId);
    }

    public static async Task<string?> AddRelationToExistingArtifact(
        string artifactPath,
        ArtifactRelation relation,
        CancellationToken cancellationToken)
    {
        var source = await File.ReadAllTextAsync(artifactPath, cancellationToken);
        var metadata = KnowledgeArtifactFrontMatter.Read(source);
        if (!metadata.IsSuccess)
            return metadata.Error;

        string updated;
        if (metadata.Metadata!.Kind is "nexus" or "continuity-path")
        {
            updated = RelationalArtifact.AddRelation(source, relation, out var error);
            if (error is not null)
                return error;
        }
        else
        {
            var relations = metadata.Metadata.Relations.ToList();
            var existing = relations.FindIndex(candidate =>
                candidate.Path.Equals(relation.Path, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                relations[existing] = relation;
            else
                relations.Add(relation);

            updated = KnowledgeArtifactFrontMatter.WithMetadata(
                source,
                metadata.Metadata,
                relations);
        }

        await CommandInfrastructure.AtomicWrite(artifactPath, updated, cancellationToken);
        return null;
    }

    public static (string Artifact, string Selection)? ParseRecommendationReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        var separator = reference.LastIndexOf(':');
        if (separator <= 0 || separator == reference.Length - 1)
            return null;

        var artifact = reference[..separator].Trim();
        var selection = reference[(separator + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(artifact) || string.IsNullOrWhiteSpace(selection))
            return null;

        return (artifact, selection);
    }

    public static string DisplayPathFrom(
        string ownerPath,
        string relatedPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(ownerPath))!;
        return Path.GetRelativePath(directory, Path.GetFullPath(relatedPath))
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public static string ResolveRelationTargetPath(
        string ownerPath,
        string relationPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(ownerPath))!;
        return Path.GetFullPath(
            relationPath.Replace('/', Path.DirectorySeparatorChar),
            directory);
    }
}
