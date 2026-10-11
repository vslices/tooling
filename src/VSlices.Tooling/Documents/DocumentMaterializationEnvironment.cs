namespace VSlices.Tooling;

internal sealed record DocumentMaterializationEnvironmentResult(
    MaterializationTemplateDefinition? Template,
    string? Error)
{
    public bool IsSuccess => Template is not null && Error is null;

    public static DocumentMaterializationEnvironmentResult Success(
        MaterializationTemplateDefinition template) =>
        new(template, null);

    public static DocumentMaterializationEnvironmentResult Failure(string error) =>
        new(null, error);
}

internal static class DocumentMaterializationEnvironment
{
    public static DocumentMaterializationEnvironmentResult Resolve(string start)
    {
        var project = VSlicesProjectContext.FindFrom(start);
        if (project is null)
        {
            return DocumentMaterializationEnvironmentResult.Failure(
                "DOCMAT001: Could not locate .vslices/config.yaml. Run 'vslices init' before authoring Documents.");
        }

        var templateRoot = Path.Combine(project.VslicesRoot, "template-standard");
        if (!File.Exists(Path.Combine(templateRoot, "manifest.yaml")))
        {
            return DocumentMaterializationEnvironmentResult.Failure(
                "DOCMAT002: Could not locate an installed Template Standard snapshot at .vslices/template-standard. Run 'vslices update template-standard' to install it.");
        }

        var catalog = TemplateStandardCatalog.Load(templateRoot);
        if (!catalog.IsSuccess)
            return DocumentMaterializationEnvironmentResult.Failure(catalog.Error!);

        var configuredTemplate = project.Configuration.DocumentsTemplate;
        if (string.IsNullOrWhiteSpace(configuredTemplate))
        {
            return DocumentMaterializationEnvironmentResult.Failure(
                "DOCMAT003: Project configuration must declare documents.template.");
        }

        if (!catalog.Catalog!.TryGetTemplate(
                configuredTemplate,
                out var template) ||
            template is null)
        {
            var available = string.Join(
                ", ",
                catalog.Catalog.Templates
                    .Select(candidate => candidate.Id)
                    .OrderBy(id => id, StringComparer.Ordinal));

            return DocumentMaterializationEnvironmentResult.Failure(
                $"DOCMAT004: Configured Document template '{configuredTemplate}' is not installed. Available templates: {available}.");
        }

        var validationError = DocumentMaterialization.ValidateTemplate(template);
        if (validationError is not null)
            return DocumentMaterializationEnvironmentResult.Failure(validationError);

        return DocumentMaterializationEnvironmentResult.Success(template);
    }
}
