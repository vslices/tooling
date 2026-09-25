namespace VSlices.Tooling;

internal static class DocumentPolicyBootstrap
{
    public static bool CanPrompt =>
        !Console.IsInputRedirected &&
        !Console.IsOutputRedirected;

    public static ProjectConfiguration ConfigureFirstInstall(
        ProjectConfiguration configuration,
        string projectRoot)
    {
        if (CanPrompt)
        {
            return PromptMissing(
                configuration,
                projectRoot,
                Console.In,
                Console.Out);
        }

        if (string.IsNullOrWhiteSpace(configuration.DocumentsRoute) ||
            string.IsNullOrWhiteSpace(configuration.DocumentsTemplate))
        {
            TerminalOutput.Warning("! Document authoring policy is not fully configured");
            if (string.IsNullOrWhiteSpace(configuration.DocumentsRoute))
                TerminalOutput.Muted("  Configure documents.route manually in .vslices/config.yaml.");
            if (string.IsNullOrWhiteSpace(configuration.DocumentsTemplate))
                TerminalOutput.Muted("  Configure documents.template manually in .vslices/config.yaml.");
        }

        return configuration;
    }

    internal static ProjectConfiguration PromptMissing(
        ProjectConfiguration configuration,
        string projectRoot,
        TextReader input,
        TextWriter output)
    {
        var route = configuration.DocumentsRoute;
        if (string.IsNullOrWhiteSpace(route))
            route = PromptRoute(projectRoot, input, output);

        var template = configuration.DocumentsTemplate;
        if (string.IsNullOrWhiteSpace(template))
            template = PromptTemplate(input, output);

        return configuration with
        {
            DocumentsRoute = route,
            DocumentsTemplate = template
        };
    }

    private static string? PromptRoute(
        string projectRoot,
        TextReader input,
        TextWriter output)
    {
        while (true)
        {
            output.Write(
                "Default Document route relative to the project root " +
                "(leave empty to configure manually): ");

            var value = input.ReadLine();
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = DocumentPathResolver.NormalizeRoute(
                projectRoot,
                value);
            if (normalized.IsSuccess)
                return normalized.Route;

            output.WriteLine(normalized.Error);
        }
    }

    private static string? PromptTemplate(
        TextReader input,
        TextWriter output)
    {
        output.Write(
            "Default Document template " +
            "(for example markdown.question-tree; leave empty to configure manually): ");

        var value = input.ReadLine();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
