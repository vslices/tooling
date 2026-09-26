using System.Security.Cryptography;

namespace VSlices.Tooling;

internal sealed record KnowledgeStandards(string Root, DocsStandardCatalog Documents, RelationalStandardCatalog Relational);
internal sealed record KnowledgeStandardsResult(KnowledgeStandards? Standards, string? Error)
{
    public bool IsSuccess => Standards is not null && Error is null;
    public static KnowledgeStandardsResult Success(KnowledgeStandards standards) => new(standards, null);
    public static KnowledgeStandardsResult Failure(string error) => new(null, error);
}
internal sealed record ExistingArtifactResolution(string Path, KnowledgeArtifactMetadata Metadata, string Source, string Sha256);
internal sealed record ExistingArtifactResolutionResult(ExistingArtifactResolution? Resolution, string? Error)
{
    public bool IsSuccess => Resolution is not null && Error is null;
    public static ExistingArtifactResolutionResult Success(ExistingArtifactResolution resolution) => new(resolution, null);
    public static ExistingArtifactResolutionResult Failure(string error) => new(null, error);
}
internal sealed record RecommendationResolution(
    ExistingArtifactResolution Source, string SelectionPath,
    RelationalRecommendation Recommendation, RelationalArtifactState SourceState,
    string Context);
internal sealed record RecommendationResolutionResult(RecommendationResolution? Resolution, string? Error)
{
    public bool IsSuccess => Resolution is not null && Error is null;
    public static RecommendationResolutionResult Success(RecommendationResolution resolution) => new(resolution, null);
    public static RecommendationResolutionResult Failure(string error) => new(null, error);
}

internal static class KnowledgeArtifactCommandSupport
{
    public static KnowledgeStandardsResult LoadStandards(string start)
    {
        var root = DocsStandardCatalog.FindInstalledRoot(start);
        if (root is null)
            return KnowledgeStandardsResult.Failure("RELCLI001: Could not locate an installed Docs Standard snapshot at .vslices/docs-standard. Run 'vslices update docs-standard'.");
        var documents = DocsStandardCatalog.Load(root);
        if (!documents.IsSuccess) return KnowledgeStandardsResult.Failure(documents.Error!);
        var relational = RelationalStandardCatalog.Load(root);
        if (!relational.IsSuccess) return KnowledgeStandardsResult.Failure(relational.Error!);
        return KnowledgeStandardsResult.Success(new KnowledgeStandards(root, documents.Catalog!, relational.Catalog!));
    }

    public static ExistingArtifactResolutionResult ResolveExistingArtifact(string artifact, string start)
    {
        var resolved = DocumentPathResolver.Resolve(artifact, start);
        if (!resolved.IsSuccess) return ExistingArtifactResolutionResult.Failure(resolved.Error!);
        var path = resolved.Path!;
        try
        {
            if (!File.Exists(path)) return ExistingArtifactResolutionResult.Failure($"RELCLI002: Related artifact '{path}' does not exist.");
            // The parsed source and optimistic-write hash come from the same byte snapshot.
            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            var source = reader.ReadToEnd();
            var metadata = KnowledgeArtifactFrontMatter.Read(source);
            return metadata.IsSuccess
                ? ExistingArtifactResolutionResult.Success(new ExistingArtifactResolution(path, metadata.Metadata!, source, Convert.ToHexString(SHA256.HashData(bytes))))
                : ExistingArtifactResolutionResult.Failure(metadata.Error!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ExistingArtifactResolutionResult.Failure($"RELCLI014: Could not read artifact '{path}': {ex.Message}");
        }
    }

    public static RecommendationResolutionResult ResolveRecommendation(
        string reference, string expectedSourceKind, string start, KnowledgeStandards standards)
    {
        var parsed = ParseRecommendationReference(reference);
        if (parsed is null)
            return RecommendationResolutionResult.Failure($"RELCLI003: Recommendation reference '{reference}' must use <artifact-path>:<selection-path>.");
        var existing = ResolveExistingArtifact(parsed.Value.Artifact, start);
        if (!existing.IsSuccess) return RecommendationResolutionResult.Failure(existing.Error!);
        var source = existing.Resolution!;
        if (source.Metadata.Kind != expectedSourceKind)
            return RecommendationResolutionResult.Failure($"RELCLI004: '{source.Path}' is artifact.kind '{source.Metadata.Kind}', not '{expectedSourceKind}'.");

        RelationalArtifactStateResult state;
        IReadOnlyList<(string Id, RelationalRecommendation Recommendation)> recommendations;
        string context;
        if (expectedSourceKind == "nexus")
        {
            if (!standards.Relational.TryGetNexus(source.Metadata.Type, out var definition) || definition is null)
                return RecommendationResolutionResult.Failure($"RELCLI005: Nexus type '{source.Metadata.Type}' is not defined by the installed snapshot.");
            state = RelationalArtifact.ReadNexus(source.Source, definition);
            if (!state.IsSuccess) return RecommendationResolutionResult.Failure(state.Error!);
            recommendations = RelationalArtifact.EnumerateRecommendations(definition, state.State!);
            context = RelationalArtifact.RecommendationContext(definition);
        }
        else if (expectedSourceKind == "continuity-path")
        {
            if (!standards.Relational.TryGetContinuityPath(source.Metadata.Type, out var definition) || definition is null)
                return RecommendationResolutionResult.Failure($"RELCLI008: Continuity Path type '{source.Metadata.Type}' is not defined by the installed snapshot.");
            state = RelationalArtifact.ReadContinuityPath(source.Source, definition);
            if (!state.IsSuccess) return RecommendationResolutionResult.Failure(state.Error!);
            recommendations = RelationalArtifact.EnumerateRecommendations(definition, state.State!);
            context = RelationalArtifact.RecommendationContext(definition);
        }
        else return RecommendationResolutionResult.Failure($"RELCLI011: Unsupported recommendation source kind '{expectedSourceKind}'.");

        var contextError = RelationalArtifact.ValidateRecommendationContext(source.Metadata, context);
        if (contextError is not null) return RecommendationResolutionResult.Failure(contextError);
        var match = recommendations.FirstOrDefault(candidate => candidate.Id == parsed.Value.Selection);
        if (match.Recommendation is null)
            return RecommendationResolutionResult.Failure($"RELCLI006: Recommendation '{parsed.Value.Selection}' is not available on the current discovery surface.");
        if (source.Metadata.Relations.Any(relation => relation.RecommendationId == parsed.Value.Selection))
            return RecommendationResolutionResult.Failure($"RELCLI007: Recommendation '{parsed.Value.Selection}' already has an associated artifact.");
        return RecommendationResolutionResult.Success(new RecommendationResolution(source, parsed.Value.Selection, match.Recommendation, state.State!, context));
    }

    public static string ResolveScope(string? explicitScope, IReadOnlyList<string> admittedScopes, string? sourceScope)
    {
        if (!string.IsNullOrWhiteSpace(explicitScope)) return explicitScope.Trim();
        if (!string.IsNullOrWhiteSpace(sourceScope) && admittedScopes.Contains(sourceScope, StringComparer.Ordinal)) return sourceScope;
        var candidates = admittedScopes.Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 ? candidates[0] : string.Empty;
    }

    public static string SuggestedName(string target, string type)
    {
        var slug = new string((target + "-" + type).Trim().Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? type : slug;
    }

    public static ArtifactRelation RelationFrom(
        string ownerPath, string relatedPath, string relatedKind, string relatedType,
        string role, string? recommendationId, string relation = "related", string? recommendationContext = null) =>
        new(relation, DisplayPathFrom(ownerPath, relatedPath), relatedKind, relatedType, role, recommendationId, recommendationContext);

    public static (string Artifact, string Selection)? ParseRecommendationReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var separator = reference.LastIndexOf(':');
        if (separator <= 0 || separator == reference.Length - 1) return null;
        var artifact = reference[..separator].Trim();
        var selection = reference[(separator + 1)..].Trim();
        return string.IsNullOrWhiteSpace(artifact) || string.IsNullOrWhiteSpace(selection) ? null : (artifact, selection);
    }

    public static string DisplayPathFrom(string ownerPath, string relatedPath) =>
        Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(ownerPath))!, Path.GetFullPath(relatedPath))
            .Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    public static string ResolveRelationTargetPath(string ownerPath, string relationPath) =>
        Path.GetFullPath(relationPath.Replace('/', Path.DirectorySeparatorChar), Path.GetDirectoryName(Path.GetFullPath(ownerPath))!);
}
