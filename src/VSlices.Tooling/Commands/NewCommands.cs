using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class NewCommands
{
    /// <summary>Creates the minimal progressive VSIR artifact for a semantic name.</summary>
    /// <param name="name">Semantic name of the concept being introduced.</param>
    public static async Task<int> Vsir(
        [Argument] string name,
        CancellationToken cancellationToken = default)
    {
        var result = VsirTemplate.Create(name);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Error);
            return 2;
        }

        var defaultPath = Path.GetFullPath(
            name.EndsWith(".vsir", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".vsir",
            Environment.CurrentDirectory);

        return await CommandInfrastructure.WriteResult(
            result.Source!,
            defaultPath,
            output: null,
            stdout: false,
            overwrite: false,
            cancellationToken);
    }

    /// <summary>Creates a progressive Document, optionally from a Nexus or Continuity Path recommendation.</summary>
    /// <param name="name">Document name or path. When omitted, Tooling derives a name from target and type.</param>
    /// <param name="kind">Document type defined by the installed Docs Standard.</param>
    /// <param name="target">Target documented by this artifact. Required unless inherited from --from-nexus or --from-path.</param>
    /// <param name="scope">Optional scope override.</param>
    /// <param name="fromNexus">Creates the Document from a Nexus recommendation using &lt;nexus-path&gt;:&lt;selection-path&gt;.</param>
    /// <param name="fromPath">Creates the Document from a Continuity Path recommendation using &lt;path&gt;:&lt;selection-path&gt;.</param>
    /// <param name="relatedTo">Associates the new Document with an arbitrary existing artifact.</param>
    /// <param name="role">Concrete relation role. Overrides a recommendation role; ignored for standalone creation.</param>
    public static async Task<int> Document(
        [Argument] string? name = null,
        string? kind = null,
        string? target = null,
        string? scope = null,
        string? fromNexus = null,
        string? fromPath = null,
        string? relatedTo = null,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var standardsResult = KnowledgeArtifactCommandSupport.LoadStandards(Environment.CurrentDirectory);
        if (!standardsResult.IsSuccess)
        {
            TerminalOutput.Error(standardsResult.Error!);
            return 1;
        }

        var standards = standardsResult.Standards!;
        var relationModeCount =
            (fromNexus is null ? 0 : 1) +
            (fromPath is null ? 0 : 1) +
            (relatedTo is null ? 0 : 1);

        if (relationModeCount > 1)
        {
            TerminalOutput.Error(
                "NEW106: Use only one of --from-nexus, --from-path, or --related-to when creating one artifact.");
            return 2;
        }

        RecommendationResolution? recommendation = null;
        ExistingArtifactResolution? arbitraryRelation = null;

        if (!string.IsNullOrWhiteSpace(fromNexus))
        {
            var resolved = KnowledgeArtifactCommandSupport.ResolveRecommendation(
                fromNexus,
                "nexus",
                Environment.CurrentDirectory,
                standards);
            if (!resolved.IsSuccess)
            {
                TerminalOutput.Error(resolved.Error!);
                return 2;
            }

            recommendation = resolved.Resolution;
        }
        else if (!string.IsNullOrWhiteSpace(fromPath))
        {
            var resolved = KnowledgeArtifactCommandSupport.ResolveRecommendation(
                fromPath,
                "continuity-path",
                Environment.CurrentDirectory,
                standards);
            if (!resolved.IsSuccess)
            {
                TerminalOutput.Error(resolved.Error!);
                return 2;
            }

            recommendation = resolved.Resolution;
        }
        else if (!string.IsNullOrWhiteSpace(relatedTo))
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                TerminalOutput.Error("NEW107: --related-to requires --role <text>.");
                return 2;
            }

            var resolved = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(
                relatedTo,
                Environment.CurrentDirectory);
            if (!resolved.IsSuccess)
            {
                TerminalOutput.Error(resolved.Error!);
                return 2;
            }

            arbitraryRelation = resolved.Resolution;
        }

        if (recommendation is not null &&
            !recommendation.Recommendation.Family.Equals("document", StringComparison.Ordinal))
        {
            TerminalOutput.Error(
                $"NEW108: Recommendation '{recommendation.SelectionPath}' creates a {recommendation.Recommendation.Family}, not a document.");
            return 2;
        }

        var resolvedKind = recommendation?.Recommendation.Type ?? kind;
        var resolvedTarget = target ?? recommendation?.SourceMetadata.Target;

        if (string.IsNullOrWhiteSpace(resolvedKind))
        {
            TerminalOutput.Error("NEW102: Document kind is required. Use --kind <type> or a recommendation.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(resolvedTarget))
        {
            TerminalOutput.Error(
                "NEW105: Document target is required. Use --target <target> or create it from a recommendation whose source has artifact.target.");
            return 2;
        }

        if (!standards.Documents.TryGetDocument(resolvedKind, out var definition) || definition is null)
        {
            TerminalOutput.Error(
                $"NEW103: Document kind '{resolvedKind}' is not defined by the installed Docs Standard.");
            return 2;
        }

        var resolvedScope = KnowledgeArtifactCommandSupport.ResolveScope(
            scope,
            definition.Scopes,
            recommendation?.SourceMetadata.Scope);

        var resolvedName = string.IsNullOrWhiteSpace(name)
            ? KnowledgeArtifactCommandSupport.SuggestedName(resolvedTarget, resolvedKind)
            : name;

        var pathResult = DocumentPathResolver.Resolve(resolvedName!, Environment.CurrentDirectory);
        if (!pathResult.IsSuccess)
        {
            TerminalOutput.Error(pathResult.Error!);
            return 2;
        }

        var path = pathResult.Path!;
        var initialRelations = new List<ArtifactRelation>();
        string? relationSourcePath = null;
        KnowledgeArtifactMetadata? relationSourceMetadata = null;
        string? sourceRecommendationId = null;
        string? effectiveRole = null;

        if (recommendation is not null)
        {
            relationSourcePath = recommendation.SourcePath;
            relationSourceMetadata = recommendation.SourceMetadata;
            sourceRecommendationId = recommendation.SelectionPath;
            effectiveRole = string.IsNullOrWhiteSpace(role)
                ? recommendation.Recommendation.Role
                : role.Trim();
        }
        else if (arbitraryRelation is not null)
        {
            relationSourcePath = arbitraryRelation.Path;
            relationSourceMetadata = arbitraryRelation.Metadata;
            effectiveRole = role!.Trim();
        }

        if (relationSourcePath is not null && relationSourceMetadata is not null)
        {
            initialRelations.Add(
                KnowledgeArtifactCommandSupport.RelationFrom(
                    path,
                    relationSourcePath,
                    relationSourceMetadata.Kind,
                    relationSourceMetadata.Type,
                    effectiveRole!,
                    recommendationId: null));
        }

        var materialization = DocumentMaterializationEnvironment.Resolve(Environment.CurrentDirectory);
        if (!materialization.IsSuccess)
        {
            TerminalOutput.Error(materialization.Error!);
            return 1;
        }

        var result = DocumentTemplate.Create(
            resolvedName!,
            resolvedKind,
            resolvedTarget,
            resolvedScope,
            initialRelations,
            standards.Documents,
            materialization.Template!);
        if (!result.IsSuccess)
        {
            TerminalOutput.Error(result.Error!);
            return 2;
        }

        var write = await CommandInfrastructure.WriteResult(
            result.Source!,
            path,
            output: null,
            stdout: false,
            overwrite: false,
            cancellationToken);
        if (write != 0)
            return write;

        if (relationSourcePath is not null && relationSourceMetadata is not null)
        {
            var reciprocal = KnowledgeArtifactCommandSupport.RelationFrom(
                relationSourcePath,
                path,
                "document",
                resolvedKind,
                effectiveRole!,
                sourceRecommendationId);

            var relationError = await KnowledgeArtifactCommandSupport.AddRelationToExistingArtifact(
                relationSourcePath,
                reciprocal,
                cancellationToken);
            if (relationError is not null)
            {
                File.Delete(path);
                TerminalOutput.Error(relationError);
                return 2;
            }
        }

        return 0;
    }

    /// <summary>Creates a Nexus composition artifact.</summary>
    public static async Task<int> Nexus(
        [Argument] string? name = null,
        string? kind = null,
        string? target = null,
        string? scope = null,
        string? fromNexus = null,
        string? fromPath = null,
        string? relatedTo = null,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var standardsResult = KnowledgeArtifactCommandSupport.LoadStandards(Environment.CurrentDirectory);
        if (!standardsResult.IsSuccess)
        {
            TerminalOutput.Error(standardsResult.Error!);
            return 1;
        }

        var standards = standardsResult.Standards!;
        var relationModeCount =
            (fromNexus is null ? 0 : 1) +
            (fromPath is null ? 0 : 1) +
            (relatedTo is null ? 0 : 1);

        if (relationModeCount > 1)
        {
            TerminalOutput.Error(
                "NEWN001: Use only one of --from-nexus, --from-path, or --related-to when creating one Nexus.");
            return 2;
        }

        RecommendationResolution? recommendation = null;
        ExistingArtifactResolution? arbitraryRelation = null;

        if (!string.IsNullOrWhiteSpace(fromNexus))
        {
            var resolved = KnowledgeArtifactCommandSupport.ResolveRecommendation(
                fromNexus,
                "nexus",
                Environment.CurrentDirectory,
                standards);
            if (!resolved.IsSuccess)
            {
                TerminalOutput.Error(resolved.Error!);
                return 2;
            }
            recommendation = resolved.Resolution;
        }
        else if (!string.IsNullOrWhiteSpace(fromPath))
        {
            var resolved = KnowledgeArtifactCommandSupport.ResolveRecommendation(
                fromPath,
                "continuity-path",
                Environment.CurrentDirectory,
                standards);
            if (!resolved.IsSuccess)
            {
                TerminalOutput.Error(resolved.Error!);
                return 2;
            }
            recommendation = resolved.Resolution;
        }
        else if (!string.IsNullOrWhiteSpace(relatedTo))
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                TerminalOutput.Error("NEWN002: --related-to requires --role <text>.");
                return 2;
            }

            var resolved = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(
                relatedTo,
                Environment.CurrentDirectory);
            if (!resolved.IsSuccess)
            {
                TerminalOutput.Error(resolved.Error!);
                return 2;
            }
            arbitraryRelation = resolved.Resolution;
        }

        if (recommendation is not null &&
            !recommendation.Recommendation.Family.Equals("nexus", StringComparison.Ordinal))
        {
            TerminalOutput.Error(
                $"NEWN003: Recommendation '{recommendation.SelectionPath}' creates a {recommendation.Recommendation.Family}, not a Nexus.");
            return 2;
        }

        var resolvedKind = recommendation?.Recommendation.Type ?? kind;
        var resolvedTarget = target ?? recommendation?.SourceMetadata.Target;
        if (string.IsNullOrWhiteSpace(resolvedKind))
        {
            TerminalOutput.Error("NEWN004: Nexus kind is required. Use --kind <type> or a recommendation.");
            return 2;
        }
        if (string.IsNullOrWhiteSpace(resolvedTarget))
        {
            TerminalOutput.Error(
                "NEWN005: Nexus target is required. Use --target <target> or create it from a recommendation whose source has artifact.target.");
            return 2;
        }

        if (!standards.Relational.TryGetNexus(resolvedKind, out var definition) || definition is null)
        {
            TerminalOutput.Error(
                $"NEWN006: Nexus kind '{resolvedKind}' is not defined by the installed Docs Standard candidate surface.");
            return 2;
        }

        var resolvedScope = KnowledgeArtifactCommandSupport.ResolveScope(
            scope,
            definition.Scopes,
            recommendation?.SourceMetadata.Scope);
        var resolvedName = string.IsNullOrWhiteSpace(name)
            ? KnowledgeArtifactCommandSupport.SuggestedName(resolvedTarget, resolvedKind)
            : name;

        var pathResult = DocumentPathResolver.Resolve(resolvedName!, Environment.CurrentDirectory);
        if (!pathResult.IsSuccess)
        {
            TerminalOutput.Error(pathResult.Error!);
            return 2;
        }

        var path = pathResult.Path!;
        var source = RelationalArtifact.CreateNexus(definition, resolvedTarget, resolvedScope);

        string? relationSourcePath = null;
        KnowledgeArtifactMetadata? relationSourceMetadata = null;
        string? sourceRecommendationId = null;
        string? effectiveRole = null;

        if (recommendation is not null)
        {
            relationSourcePath = recommendation.SourcePath;
            relationSourceMetadata = recommendation.SourceMetadata;
            sourceRecommendationId = recommendation.SelectionPath;
            effectiveRole = string.IsNullOrWhiteSpace(role)
                ? recommendation.Recommendation.Role
                : role.Trim();
        }
        else if (arbitraryRelation is not null)
        {
            relationSourcePath = arbitraryRelation.Path;
            relationSourceMetadata = arbitraryRelation.Metadata;
            effectiveRole = role!.Trim();
        }

        if (relationSourcePath is not null && relationSourceMetadata is not null)
        {
            var relation = KnowledgeArtifactCommandSupport.RelationFrom(
                path,
                relationSourcePath,
                relationSourceMetadata.Kind,
                relationSourceMetadata.Type,
                effectiveRole!,
                recommendationId: null);
            source = RelationalArtifact.AddRelation(source, relation, out var error);
            if (error is not null)
            {
                TerminalOutput.Error(error);
                return 2;
            }
        }

        var write = await CommandInfrastructure.WriteResult(
            source,
            path,
            output: null,
            stdout: false,
            overwrite: false,
            cancellationToken);
        if (write != 0)
            return write;

        if (relationSourcePath is not null && relationSourceMetadata is not null)
        {
            var reciprocal = KnowledgeArtifactCommandSupport.RelationFrom(
                relationSourcePath,
                path,
                "nexus",
                resolvedKind,
                effectiveRole!,
                sourceRecommendationId);

            var relationError = await KnowledgeArtifactCommandSupport.AddRelationToExistingArtifact(
                relationSourcePath,
                reciprocal,
                cancellationToken);
            if (relationError is not null)
            {
                File.Delete(path);
                TerminalOutput.Error(relationError);
                return 2;
            }
        }

        return 0;
    }

    /// <summary>Creates a Continuity Path trajectory artifact.</summary>
    public static async Task<int> ContinuityPath(
        [Argument] string? name = null,
        string? kind = null,
        string? target = null,
        string? scope = null,
        string? relatedTo = null,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var standardsResult = KnowledgeArtifactCommandSupport.LoadStandards(Environment.CurrentDirectory);
        if (!standardsResult.IsSuccess)
        {
            TerminalOutput.Error(standardsResult.Error!);
            return 1;
        }

        if (string.IsNullOrWhiteSpace(kind))
        {
            TerminalOutput.Error("NEWP001: Continuity Path kind is required. Use --kind <type>.");
            return 2;
        }
        if (string.IsNullOrWhiteSpace(target))
        {
            TerminalOutput.Error("NEWP002: Continuity Path target is required. Use --target <target>.");
            return 2;
        }

        var standards = standardsResult.Standards!;
        if (!standards.Relational.TryGetContinuityPath(kind, out var definition) || definition is null)
        {
            TerminalOutput.Error(
                $"NEWP003: Continuity Path kind '{kind}' is not defined by the installed Docs Standard candidate surface.");
            return 2;
        }

        ExistingArtifactResolution? arbitraryRelation = null;
        if (!string.IsNullOrWhiteSpace(relatedTo))
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                TerminalOutput.Error("NEWP004: --related-to requires --role <text>.");
                return 2;
            }

            var resolved = KnowledgeArtifactCommandSupport.ResolveExistingArtifact(
                relatedTo,
                Environment.CurrentDirectory);
            if (!resolved.IsSuccess)
            {
                TerminalOutput.Error(resolved.Error!);
                return 2;
            }
            arbitraryRelation = resolved.Resolution;
        }

        var resolvedScope = string.IsNullOrWhiteSpace(scope) ? kind : scope.Trim();
        var resolvedName = string.IsNullOrWhiteSpace(name)
            ? KnowledgeArtifactCommandSupport.SuggestedName(target, kind)
            : name;

        var pathResult = DocumentPathResolver.Resolve(resolvedName!, Environment.CurrentDirectory);
        if (!pathResult.IsSuccess)
        {
            TerminalOutput.Error(pathResult.Error!);
            return 2;
        }

        var path = pathResult.Path!;
        var source = RelationalArtifact.CreateContinuityPath(definition, target, resolvedScope);

        if (arbitraryRelation is not null)
        {
            var relation = KnowledgeArtifactCommandSupport.RelationFrom(
                path,
                arbitraryRelation.Path,
                arbitraryRelation.Metadata.Kind,
                arbitraryRelation.Metadata.Type,
                role!.Trim(),
                recommendationId: null);
            source = RelationalArtifact.AddRelation(source, relation, out var error);
            if (error is not null)
            {
                TerminalOutput.Error(error);
                return 2;
            }
        }

        var write = await CommandInfrastructure.WriteResult(
            source,
            path,
            output: null,
            stdout: false,
            overwrite: false,
            cancellationToken);
        if (write != 0)
            return write;

        if (arbitraryRelation is not null)
        {
            var reciprocal = KnowledgeArtifactCommandSupport.RelationFrom(
                arbitraryRelation.Path,
                path,
                "continuity-path",
                kind,
                role!.Trim(),
                recommendationId: null);

            var relationError = await KnowledgeArtifactCommandSupport.AddRelationToExistingArtifact(
                arbitraryRelation.Path,
                reciprocal,
                cancellationToken);
            if (relationError is not null)
            {
                File.Delete(path);
                TerminalOutput.Error(relationError);
                return 2;
            }
        }

        return 0;
    }

}