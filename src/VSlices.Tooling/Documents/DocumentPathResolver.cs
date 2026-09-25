namespace VSlices.Tooling;

internal sealed record DocumentPathResolution(
    string? Path,
    string? Error)
{
    public bool IsSuccess => Path is not null && Error is null;

    public static DocumentPathResolution Success(string path) =>
        new(path, null);

    public static DocumentPathResolution Failure(string error) =>
        new(null, error);
}

internal sealed record DocumentRouteNormalizationResult(
    string? Route,
    string? Error)
{
    public bool IsSuccess => Route is not null && Error is null;

    public static DocumentRouteNormalizationResult Success(string route) =>
        new(route, null);

    public static DocumentRouteNormalizationResult Failure(string error) =>
        new(null, error);
}

internal static class DocumentPathResolver
{
    public static DocumentPathResolution Resolve(
        string document,
        string start)
    {
        var value = document.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? document
            : document + ".md";

        if (IsExplicitPath(document))
        {
            return DocumentPathResolution.Success(
                Path.GetFullPath(value, start));
        }

        var project = VSlicesProjectContext.FindFrom(start);
        if (project is null ||
            string.IsNullOrWhiteSpace(project.Configuration.DocumentsRoute))
        {
            return DocumentPathResolution.Success(
                Path.GetFullPath(value, start));
        }

        var route = NormalizeRoute(
            project.ProjectRoot,
            project.Configuration.DocumentsRoute);
        if (!route.IsSuccess)
            return DocumentPathResolution.Failure(route.Error!);

        var baseDirectory = route.Route!.Equals(".", StringComparison.Ordinal)
            ? project.ProjectRoot
            : Path.Combine(
                project.ProjectRoot,
                route.Route.Replace('/', Path.DirectorySeparatorChar));

        return DocumentPathResolution.Success(
            Path.GetFullPath(value, baseDirectory));
    }

    public static DocumentRouteNormalizationResult NormalizeRoute(
        string projectRoot,
        string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return DocumentRouteNormalizationResult.Failure(
                "DOCPATH001: documents.route must be a non-empty relative path.");
        }

        var trimmed = route.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return DocumentRouteNormalizationResult.Failure(
                "DOCPATH002: documents.route must be relative to the VSlices project root.");
        }

        var full = Path.GetFullPath(trimmed, projectRoot);
        var relative = Path.GetRelativePath(projectRoot, full);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return DocumentRouteNormalizationResult.Failure(
                "DOCPATH003: documents.route cannot escape the VSlices project root.");
        }

        var normalized = relative
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return DocumentRouteNormalizationResult.Success(normalized);
    }

    private static bool IsExplicitPath(string value) =>
        Path.IsPathRooted(value) ||
        value.Contains('/') ||
        value.Contains('\\');
}
