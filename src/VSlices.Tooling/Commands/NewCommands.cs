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

    /// <summary>Creates the minimal progressive Markdown artifact for a Docs Standard document type.</summary>
    /// <param name="name">Document name or path. The .md extension is added when omitted.</param>
    /// <param name="kind">Document type defined by the installed VSlices Docs Standard.</param>
    public static async Task<int> Document(
        [Argument] string name,
        string? kind = null,
        CancellationToken cancellationToken = default)
    {
        var standardRoot = DocsStandardCatalog.FindInstalledRoot(Environment.CurrentDirectory);
        if (standardRoot is null)
        {
            Console.Error.WriteLine(
                "NEW104: Could not locate an installed Docs Standard snapshot at .vslices/docs-standard. Run 'vslices update docs-standard' to install it.");
            return 1;
        }

        var catalog = DocsStandardCatalog.Load(standardRoot);
        if (!catalog.IsSuccess)
        {
            Console.Error.WriteLine(catalog.Error);
            return 1;
        }

        var project = VSlicesProjectContext.FindFrom(Environment.CurrentDirectory);
        if (project is null)
        {
            Console.Error.WriteLine(
                "NEW105: Could not locate .vslices/config.yaml. Run 'vslices init' before creating Documents.");
            return 1;
        }

        var templateRoot = Path.Combine(project.VslicesRoot, "template-standard");
        if (!File.Exists(Path.Combine(templateRoot, "manifest.yaml")))
        {
            Console.Error.WriteLine(
                "NEW106: Could not locate an installed Template Standard snapshot at .vslices/template-standard. Run 'vslices update template-standard' to install it.");
            return 1;
        }

        var templateCatalog = TemplateStandardCatalog.Load(templateRoot);
        if (!templateCatalog.IsSuccess)
        {
            Console.Error.WriteLine(templateCatalog.Error);
            return 1;
        }

        var configuredTemplate = project.Configuration.DocumentsTemplate;
        if (string.IsNullOrWhiteSpace(configuredTemplate))
        {
            Console.Error.WriteLine(
                "NEW107: Project configuration must declare documents.template.");
            return 1;
        }

        if (!templateCatalog.Catalog!.TryGetTemplate(
                configuredTemplate,
                out var materializationTemplate) ||
            materializationTemplate is null)
        {
            var available = string.Join(
                ", ",
                templateCatalog.Catalog.Templates
                    .Select(template => template.Id)
                    .OrderBy(id => id, StringComparer.Ordinal));

            Console.Error.WriteLine(
                $"NEW108: Configured Document template '{configuredTemplate}' is not installed. Available templates: {available}.");
            return 1;
        }

        var result = DocumentTemplate.Create(
            name,
            kind,
            catalog.Catalog!,
            materializationTemplate);
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Error);
            return 2;
        }

        var defaultPath = Path.GetFullPath(
            name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".md",
            Environment.CurrentDirectory);

        return await CommandInfrastructure.WriteResult(
            result.Source!,
            defaultPath,
            output: null,
            stdout: false,
            overwrite: false,
            cancellationToken);
    }
}
