namespace VSlices.Tooling;

internal sealed record ProjectOrigin(
    string Source,
    string? Reference)
{
    public static ProjectOrigin OfficialRuleset =>
        new(
            ProjectConfiguration.OfficialRulesetSource,
            ProjectConfiguration.OfficialRulesetRef);

    public static ProjectOrigin OfficialDocsStandard =>
        new(
            ProjectConfiguration.OfficialDocsStandardSource,
            ProjectConfiguration.OfficialDocsStandardRef);
}

internal sealed record ProjectOriginResolution(
    ProjectOrigin? Origin,
    string? Error)
{
    public bool IsSuccess => Origin is not null && Error is null;

    public static ProjectOriginResolution Success(ProjectOrigin origin) =>
        new(origin, null);

    public static ProjectOriginResolution Failure(string error) =>
        new(null, error);
}

internal static class ProjectOriginResolver
{
    public static ProjectOriginResolution Resolve(
        string? compactOrigin,
        string? legacySource,
        string? legacyReference,
        string? configuredSource,
        string? configuredReference,
        ProjectOrigin official,
        string diagnosticCode)
    {
        if (!string.IsNullOrWhiteSpace(compactOrigin) &&
            (!string.IsNullOrWhiteSpace(legacySource) ||
             !string.IsNullOrWhiteSpace(legacyReference)))
        {
            return ProjectOriginResolution.Failure(
                $"{diagnosticCode}: --origin cannot be combined with --from or --ref.");
        }

        if (!string.IsNullOrWhiteSpace(compactOrigin))
            return Parse(compactOrigin!, official, diagnosticCode);

        var explicitLegacySource = !string.IsNullOrWhiteSpace(legacySource);
        var explicitLegacyReference = !string.IsNullOrWhiteSpace(legacyReference);
        if (explicitLegacySource || explicitLegacyReference)
        {
            var source = explicitLegacySource
                ? legacySource!.Trim()
                : configuredSource ?? official.Source;

            var reference = explicitLegacyReference
                ? legacyReference!.Trim()
                : explicitLegacySource
                    ? DefaultReferenceFor(source, official)
                    : configuredReference ?? DefaultReferenceFor(source, official);

            return ProjectOriginResolution.Success(
                new ProjectOrigin(source, reference));
        }

        if (!string.IsNullOrWhiteSpace(configuredSource))
        {
            return ProjectOriginResolution.Success(
                new ProjectOrigin(
                    configuredSource!,
                    configuredReference ?? DefaultReferenceFor(configuredSource!, official)));
        }

        return ProjectOriginResolution.Success(official);
    }

    public static ProjectOriginResolution Parse(
        string value,
        ProjectOrigin official,
        string diagnosticCode)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return ProjectOriginResolution.Failure(
                $"{diagnosticCode}: --origin must not be empty.");
        }

        if (TryParseGitHubShorthand(trimmed, out var shorthandSource, out var shorthandReference))
        {
            return ProjectOriginResolution.Success(
                new ProjectOrigin(
                    shorthandSource!,
                    shorthandReference ?? DefaultReferenceFor(shorthandSource!, official)));
        }

        if (TryParseGitHubUrlWithFragment(trimmed, out var githubSource, out var githubReference))
        {
            return ProjectOriginResolution.Success(
                new ProjectOrigin(
                    githubSource!,
                    githubReference ?? DefaultReferenceFor(githubSource!, official)));
        }

        return ProjectOriginResolution.Success(
            new ProjectOrigin(trimmed, DefaultReferenceFor(trimmed, official)));
    }

    private static string? DefaultReferenceFor(
        string source,
        ProjectOrigin official) =>
        source.Equals(official.Source, StringComparison.OrdinalIgnoreCase)
            ? official.Reference
            : null;

    private static bool TryParseGitHubShorthand(
        string value,
        out string? source,
        out string? reference)
    {
        source = null;
        reference = null;

        if (value.Contains("://", StringComparison.Ordinal) ||
            value.Contains('\\') ||
            Path.IsPathRooted(value))
        {
            return false;
        }

        var separator = value.IndexOf(':');
        if (separator <= 0)
            return false;

        var repository = value[..separator];
        var candidateReference = value[(separator + 1)..];

        if (string.IsNullOrWhiteSpace(candidateReference))
            return false;

        var segments = repository
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2 ||
            repository.StartsWith(".", StringComparison.Ordinal) ||
            repository.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!segments.All(IsGitHubSlug))
            return false;

        source = $"https://github.com/{segments[0]}/{segments[1]}";
        reference = candidateReference?.Trim();
        return true;
    }

    private static bool TryParseGitHubUrlWithFragment(
        string value,
        out string? source,
        out string? reference)
    {
        source = null;
        reference = null;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
            return false;

        source = uri.GetLeftPart(UriPartial.Authority) + "/" +
                 string.Join("/", segments);
        reference = string.IsNullOrWhiteSpace(uri.Fragment)
            ? null
            : Uri.UnescapeDataString(uri.Fragment[1..]);
        return true;
    }

    private static bool IsGitHubSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character) &&
                character is not '-' and not '_' and not '.')
            {
                return false;
            }
        }

        return true;
    }
}
