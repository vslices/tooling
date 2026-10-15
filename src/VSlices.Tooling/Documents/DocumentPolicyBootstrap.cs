namespace VSlices.Tooling;

internal static class DocumentPolicyBootstrap
{
    private const string DefaultRoute = "docs";
    private const string OfficialTemplate = "markdown.question-tree";

    public static bool CanPrompt =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public static ProjectConfiguration ConfigureFirstInstall(
        ProjectConfiguration configuration,
        string projectRoot)
    {
        return Configure(configuration, projectRoot, Console.In, Console.Out, CanPrompt);
    }

    internal static ProjectConfiguration PromptMissing(
        ProjectConfiguration configuration,
        string projectRoot,
        TextReader input,
        TextWriter output) =>
        Configure(configuration, projectRoot, input, output, interactive: true);

    internal static ProjectConfiguration Configure(
        ProjectConfiguration configuration,
        string projectRoot,
        TextReader input,
        TextWriter output,
        bool interactive)
    {
        var installed = Path.Combine(projectRoot, ".vslices", "template-standard");
        var templates = Array.Empty<MaterializationTemplateDefinition>();
        var manifest = Path.Combine(installed, "manifest.yaml");
        if (File.Exists(manifest))
        {
            var loaded = TemplateStandardCatalog.Load(installed);
            if (loaded.IsSuccess)
                templates = loaded.Catalog!.Templates
                    .Where(template => template.ArtifactKind.Equals("document", StringComparison.OrdinalIgnoreCase) &&
                        DocumentMaterialization.ValidateTemplate(template) is null)
                    .OrderBy(template => template.Id, StringComparer.Ordinal)
                    .ToArray();
            else
                output.WriteLine($"Warning: installed Template Standard could not be read: {loaded.Error}");
        }

        var route = configuration.DocumentsRoute;
        if (string.IsNullOrWhiteSpace(route))
        {
            if (templates.Length == 0)
                output.WriteLine("No compatible Document template is installed. Document route can still be configured.");
            route = interactive ? PromptRoute(projectRoot, input, output) : DefaultRoute;
            output.WriteLine($"Document route: {route}");
        }

        var templateId = configuration.DocumentsTemplate;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            if (templates.Length == 1)
            {
                templateId = templates[0].Id;
                output.WriteLine($"Document template automatically selected: {templateId}");
            }
            else if (templates.Length > 1)
            {
                if (interactive)
                    templateId = PromptTemplate(templates, input, output);
                else
                    output.WriteLine("Warning: multiple compatible Document templates are installed. Set documents.template in .vslices/config.yaml.");
            }
        }

        if (templates.Length == 0 && string.IsNullOrWhiteSpace(templateId))
        {
            output.WriteLine(
                "Warning: no compatible Template Standard is installed. " +
                "Install the official one with 'vslices update template-standard' " +
                "or choose another with 'vslices update template-standard --origin <source>'. " +
                "Document authoring will require documents.template after installation.");
        }

        return configuration with { DocumentsRoute = route, DocumentsTemplate = templateId };
    }

    private static string PromptRoute(string projectRoot, TextReader input, TextWriter output)
    {
        while (true)
        {
            output.Write("Default Document route relative to project root [docs] (Enter to accept): ");
            var value = input.ReadLine();
            var requested = string.IsNullOrWhiteSpace(value) ? DefaultRoute : value.Trim();
            var normalized = DocumentPathResolver.NormalizeRoute(projectRoot, requested);
            if (normalized.IsSuccess)
                return normalized.Route!;
            output.WriteLine(normalized.Error);
        }
    }

    private static string? PromptTemplate(
        MaterializationTemplateDefinition[] templates,
        TextReader input, TextWriter output)
    {
        output.WriteLine("Multiple compatible Document templates are installed:");
        for (var i = 0; i < templates.Length; i++)
            output.WriteLine($"  {i + 1}. {templates[i].Id}");
        output.Write("Choose a number or template id (Enter to configure later): ");
        var value = input.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out var number) && number >= 1 && number <= templates.Length)
            return templates[number - 1].Id;
        var match = templates.FirstOrDefault(template => template.Id.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match.Id;
        output.WriteLine("Warning: invalid template choice. Set documents.template manually in .vslices/config.yaml.");
        return null;
    }
}
