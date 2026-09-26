namespace VSlices.Tooling;

internal sealed record KnowledgeArtifactCreationRequest(
    string Family, string? Name, string? Kind, string? Target, string? Scope,
    string? FromNexus, string? FromPath, string? RelatedTo, string? Role);
internal sealed record KnowledgeArtifactCreationResult(string? Path, string? Error, int ExitCode)
{
    public static KnowledgeArtifactCreationResult Success(string path) => new(path, null, 0);
    public static KnowledgeArtifactCreationResult Failure(string error, int exitCode = 2) => new(null, error, exitCode);
}

internal static class KnowledgeArtifactCreation
{
    public static async Task<KnowledgeArtifactCreationResult> Execute(
        KnowledgeArtifactCreationRequest request, string start, CancellationToken cancellationToken)
    {
        var standardsResult = KnowledgeArtifactCommandSupport.LoadStandards(start);
        if (!standardsResult.IsSuccess)
        {
            var error = standardsResult.Error!;
            if (request.Family == "document" && error.StartsWith("RELCLI001:", StringComparison.Ordinal))
                error = "NEW104:" + error["RELCLI001:".Length..];
            return KnowledgeArtifactCreationResult.Failure(error, 1);
        }
        var standards = standardsResult.Standards!;
        var modes = new[] { request.FromNexus, request.FromPath, request.RelatedTo };
        if (modes.Count(value => value is not null) > 1)
            return KnowledgeArtifactCreationResult.Failure("NEW106: Use only one of --from-nexus, --from-path, or --related-to.");
        if (modes.Any(value => value is not null && string.IsNullOrWhiteSpace(value)))
            return KnowledgeArtifactCreationResult.Failure("NEW110: Artifact references must not be empty.");

        RecommendationResolution? recommendation = null;
        ExistingArtifactResolution? related = null;
        if (request.FromNexus is not null || request.FromPath is not null)
        {
            var resolved = KnowledgeArtifactCommandSupport.ResolveRecommendation(
                request.FromNexus ?? request.FromPath!, request.FromNexus is not null ? "nexus" : "continuity-path", start, standards);
            if (!resolved.IsSuccess) return KnowledgeArtifactCreationResult.Failure(resolved.Error!);
            recommendation = resolved.Resolution!;
            related = recommendation.Source;
            if (recommendation.Recommendation.Family != request.Family)
                return KnowledgeArtifactCreationResult.Failure($"NEW108: Recommendation '{recommendation.SelectionPath}' creates a {recommendation.Recommendation.Family}, not a {request.Family}.");
            if (request.Kind is not null && !request.Kind.Equals(recommendation.Recommendation.Type, StringComparison.OrdinalIgnoreCase))
                return KnowledgeArtifactCreationResult.Failure("NEW109: --kind cannot override the type selected by the recommendation.");
        }
        else if (request.RelatedTo is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Role))
                return KnowledgeArtifactCreationResult.Failure("NEW107: --related-to requires --role <text>.");
            var resolved = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(request.RelatedTo, start);
            if (!resolved.IsSuccess) return KnowledgeArtifactCreationResult.Failure(resolved.Error!);
            related = resolved.Resolution;
        }

        var kind = (recommendation?.Recommendation.Type ?? request.Kind)?.Trim();
        var target = (request.Target ?? recommendation?.Source.Metadata.Target)?.Trim();
        if (string.IsNullOrWhiteSpace(kind))
            return KnowledgeArtifactCreationResult.Failure("NEW102: Artifact kind is required. Use --kind <type> or a recommendation.");
        if (string.IsNullOrWhiteSpace(target))
            return KnowledgeArtifactCreationResult.Failure("NEW105: Artifact target is required. Use --target <target> or a recommendation whose source has artifact.target.");
        var name = string.IsNullOrWhiteSpace(request.Name) ? KnowledgeArtifactCommandSupport.SuggestedName(target, kind) : request.Name;
        var pathResult = DocumentPathResolver.Resolve(name!, start);
        if (!pathResult.IsSuccess) return KnowledgeArtifactCreationResult.Failure(pathResult.Error!);
        var path = pathResult.Path!;
        if (File.Exists(path) || Directory.Exists(path))
            return KnowledgeArtifactCreationResult.Failure($"RELWRITE001: '{path}' already exists; artifact creation never overwrites it.");

        var role = related is null ? null : !string.IsNullOrWhiteSpace(request.Role) ? request.Role.Trim() : recommendation!.Recommendation.Role;
        var relations = new List<ArtifactRelation>();
        if (related is not null)
            relations.Add(KnowledgeArtifactCommandSupport.RelationFrom(path, related.Path, related.Metadata.Kind, related.Metadata.Type, role!, null));

        string source;
        if (request.Family == "document")
        {
            if (!standards.Documents.TryGetDocument(kind, out var definition) || definition is null)
                return KnowledgeArtifactCreationResult.Failure($"NEW103: Document kind '{kind}' is not defined by the installed Docs Standard.");
            kind = definition.Type;
            var scope = KnowledgeArtifactCommandSupport.ResolveScope(request.Scope, definition.Scopes, recommendation?.Source.Metadata.Scope);
            var materialization = DocumentMaterializationEnvironment.Resolve(start);
            if (!materialization.IsSuccess) return KnowledgeArtifactCreationResult.Failure(materialization.Error!, 1);
            var rendered = DocumentTemplate.Create(name!, kind, target, scope, relations, standards.Documents, materialization.Template!);
            if (!rendered.IsSuccess) return KnowledgeArtifactCreationResult.Failure(rendered.Error!);
            source = rendered.Source!;
        }
        else if (request.Family == "nexus")
        {
            if (!standards.Relational.TryGetNexus(kind, out var definition) || definition is null)
                return KnowledgeArtifactCreationResult.Failure($"NEWN006: Nexus kind '{kind}' is not defined by the installed candidate surface.");
            kind = definition.Type;
            var scope = KnowledgeArtifactCommandSupport.ResolveScope(request.Scope, definition.Scopes, recommendation?.Source.Metadata.Scope);
            source = RelationalArtifact.CreateNexus(definition, target, scope);
            var reconstructed = RelationalArtifact.ReadNexus(source, definition);
            if (!reconstructed.IsSuccess) return KnowledgeArtifactCreationResult.Failure(reconstructed.Error!);
        }
        else if (request.Family == "continuity-path")
        {
            if (!standards.Relational.TryGetContinuityPath(kind, out var definition) || definition is null)
                return KnowledgeArtifactCreationResult.Failure($"NEWP003: Continuity Path kind '{kind}' is not defined by the installed candidate surface.");
            kind = definition.Type;
            // Path vocabulary does not currently declare target scopes. Its type is not a target classification.
            source = RelationalArtifact.CreateContinuityPath(definition, target, request.Scope?.Trim());
            var reconstructed = RelationalArtifact.ReadContinuityPath(source, definition);
            if (!reconstructed.IsSuccess) return KnowledgeArtifactCreationResult.Failure(reconstructed.Error!);
        }
        else return KnowledgeArtifactCreationResult.Failure($"NEW111: Unsupported artifact family '{request.Family}'.");

        if (request.Family != "document" && relations.Count > 0)
        {
            source = RelationalArtifact.AddRelation(source, relations[0], out var error);
            if (error is not null) return KnowledgeArtifactCreationResult.Failure(error);
        }
        ArtifactRelation? reciprocal = related is null ? null : KnowledgeArtifactCommandSupport.RelationFrom(
            related.Path, path, request.Family, kind, role!, recommendation?.SelectionPath,
            recommendationContext: recommendation?.Context);
        var writeError = await KnowledgeArtifactWriter.Create(path, source, related, reciprocal, cancellationToken);
        return writeError is null ? KnowledgeArtifactCreationResult.Success(path) : KnowledgeArtifactCreationResult.Failure(writeError);
    }
}
