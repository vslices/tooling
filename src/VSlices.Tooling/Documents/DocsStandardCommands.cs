using ConsoleAppFramework;
using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal static class DocsStandardCommands
{
    /// <summary>Creates a brand-new local Docs Standard or installs an existing one from --origin.</summary>
    /// <param name="name">Optional installation name; for imported standards overrides manifest.name.</param>
    /// <param name="origin">Existing GitHub origin, archive URL, or physical source directory. Omit to create a new local standard.</param>
    public static Task<int> New(
        [Argument] string? name = null, string? origin = null, CancellationToken cancellationToken = default) =>
        InProject((project) => DocsStandardLifecycle.New(project, name, origin, cancellationToken));

    /// <summary>Lists all installed Docs Standards, including locally authored standards.</summary>
    public static Task<int> List() =>
        InProject(project => Task.FromResult(DocsStandardLifecycle.List(project)));

    /// <summary>Deletes one installed Docs Standard without deleting an external origin.</summary>
    /// <param name="name">Required installed Docs Standard name.</param>
    /// <param name="skipConfirm">Skip the confirmation prompt for automation.</param>
    public static Task<int> Delete(
        [Argument] string? name = null, bool skipConfirm = false,
        CancellationToken cancellationToken = default) =>
        InProject(project => DocsStandardLifecycle.Delete(project, name, skipConfirm, cancellationToken));

    internal static async Task<int> InProject(Func<VSlicesProjectContext, Task<int>> operation)
    {
        try
        {
            var project = VSlicesProjectContext.FindFrom(Environment.CurrentDirectory);
            if (project is null)
            {
                TerminalOutput.Error("STDCLI001: No VSlices project found. Run 'vslices init' first.");
                return 1;
            }
            return await operation(project);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                   or ArgumentException or YamlDotNet.Core.YamlException)
        {
            TerminalOutput.Error($"STDCLI002: {ex.Message}");
            return 1;
        }
    }
}

internal static class DocsStandardDiscoveryCommands
{
    private sealed record Structure(DocsStandardInstallation Standard, string Family, string Type, YamlMappingNode Definition)
    {
        public string Qualified => Standard.Name + "." + Type;
    }

    /// <summary>Explores admitted Docs Standard structures across installed standards.</summary>
    /// <param name="docsStandard">Installed standard name (optional).</param>
    /// <param name="structure">Definition type or qualified name, e.g. serviu.integration-report.</param>
    public static Task<int> Discover(
        string? docsStandard = null, string? structure = null) =>
        DocsStandardCommands.InProject(project => Task.FromResult(Explore(project, docsStandard, structure)));

    private static int Explore(VSlicesProjectContext project, string? standardName, string? structureName)
    {
        var registry = DocsStandardRegistry.Load(project);
        if (registry.Installations.Count == 0)
        {
            Console.WriteLine("No Docs Standards installed. Use 'vslices new docs-standard'.");
            return 0;
        }

        if (structureName is not null && structureName.Contains('.', StringComparison.Ordinal))
        {
            var separator = structureName.IndexOf('.');
            var qualifier = structureName[..separator];
            if (standardName is not null && !standardName.Equals(qualifier, StringComparison.OrdinalIgnoreCase))
            {
                TerminalOutput.Error("STDDISC001: --docs-standard conflicts with the qualified --structure.");
                return 2;
            }
            standardName = qualifier;
            structureName = structureName[(separator + 1)..];
        }

        if (standardName is not null && registry.Find(standardName) is null)
        {
            TerminalOutput.Error($"STDDISC002: Docs Standard '{standardName}' is not installed.");
            return 2;
        }

        var matching = registry.Installations
            .Where(standard => standardName is null ||
                standard.Name.Equals(standardName, StringComparison.OrdinalIgnoreCase)).ToArray();
        var all = new List<Structure>();
        foreach (var standard in matching)
        {
            var root = registry.PathFor(standard);
            // Validate using the family-specific loaders before exposing admitted vocabulary.
            var documents = DocsStandardCatalog.Load(root);
            var notes = SupportNoteStandardCatalog.Load(root);
            var relational = RelationalStandardCatalog.Load(root);
            if (!documents.IsSuccess || !notes.IsSuccess || !relational.IsSuccess)
            {
                TerminalOutput.Error(documents.Error ?? notes.Error ?? relational.Error!);
                return 2;
            }
            foreach (var file in DocsStandardDiff.DeclaredFiles(root))
            {
                using var reader = File.OpenText(file.Value);
                var yaml = new YamlStream();
                yaml.Load(reader);
                if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode mapping)
                {
                    TerminalOutput.Error($"STDDISC003: Invalid structure at '{file.Value}'.");
                    return 2;
                }
                var family = file.Key.Split('/')[0];
                var key = family switch
                {
                    "documents" => "document",
                    "support-notes" => "support-note",
                    "nexus" => "nexus",
                    "continuity-paths" => "continuity-path",
                    _ => throw new InvalidDataException($"Unknown family '{family}'.")
                };
                var inner = Mapping(mapping, key);
                var type = Scalar(inner, "type");
                if (string.IsNullOrWhiteSpace(type))
                {
                    TerminalOutput.Error($"STDDISC004: Missing {key}.type in '{file.Value}'.");
                    return 2;
                }
                all.Add(new Structure(standard, family, type, inner));
            }
        }

        if (structureName is not null)
        {
            all = all.Where(s => s.Type.Equals(structureName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (all.Count == 0)
            {
                TerminalOutput.Error($"STDDISC005: Structure '{structureName}' is not an admitted definition.");
                return 2;
            }
            var chosen = Choose(all, "Choose a structure:");
            if (chosen is null) return 2;
            Display(chosen);
            return 0;
        }

        if (standardName is null && matching.Length > 1)
        {
            var chosen = Choose(matching, "Choose a Docs Standard:", s => s.Name);
            if (chosen is null) return 2;
            all = all.Where(s => s.Standard.Name == chosen.Name).ToList();
        }

        if (all.Count == 0)
        {
            Console.WriteLine("No manifest-promoted structures are available in this Docs Standard.");
            return 0;
        }
        var selected = Choose(all, "Choose a structure:");
        if (selected is null) return 2;
        Display(selected);
        return 0;
    }

    private static T? Choose<T>(IReadOnlyList<T> options, string prompt, Func<T, string>? label = null) where T : class
    {
        if (options.Count == 1) return options[0];
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            TerminalOutput.Error("STDDISC006: Ambiguous selection. Supply --docs-standard and --structure. Candidates: " +
                string.Join(", ", options.Select(value => label?.Invoke(value) ??
                    (value is Structure s ? s.Qualified + " (" + s.Family + ")" : value.ToString()))));
            return null;
        }
        Console.WriteLine(prompt);
        for (var i = 0; i < options.Count; i++)
            Console.WriteLine($"  {i + 1}. {label?.Invoke(options[i]) ??
                (options[i] is Structure s ? s.Qualified + " (" + s.Family + ")" : options[i]?.ToString())}");
        Console.Write("Selection: ");
        if (!int.TryParse(Console.ReadLine(), out var selected) || selected < 1 || selected > options.Count)
        {
            TerminalOutput.Error("STDDISC007: No valid selection made.");
            return null;
        }
        return options[selected - 1];
    }

    private static void Display(Structure structure)
    {
        Console.WriteLine($"Structure: {structure.Qualified}");
        Console.WriteLine($"  family: {structure.Family}");
        Console.WriteLine($"  source: {structure.Standard.DeclaredName}");
        var definition = structure.Definition;
        var purpose = Scalar(definition, "purpose");
        var traversal = Scalar(definition, "recommended-traversal");
        if (purpose is not null) Console.WriteLine($"  responsibility/purpose: {purpose}");
        else if (structure.Family == "documents")
            Console.WriteLine($"  responsibility/root question: {RootQuestionText(definition)}");
        else if (structure.Family == "nexus")
            Console.WriteLine("  responsibility: compose independently owned documentary perspectives");
        else if (structure.Family == "support-notes")
            Console.WriteLine("  responsibility: preserve auxiliary knowledge through a small question surface");
        if (traversal is not null) Console.WriteLine($"  recommended traversal: {traversal}");

        if (definition.Children.TryGetValue(new YamlScalarNode("scopes"), out var scopes) &&
            scopes is YamlSequenceNode values)
            Console.WriteLine("  scopes: " + string.Join(", ", values.Children.OfType<YamlScalarNode>().Select(x => x.Value)));

        Console.WriteLine("Available questions:");
        if (definition.Children.TryGetValue(new YamlScalarNode("question"), out var question))
            DisplayQuestion(question, "  ");
        if (definition.Children.TryGetValue(new YamlScalarNode("questions"), out var questions) &&
            questions is YamlSequenceNode sequence)
            foreach (var node in sequence.Children) DisplayQuestion(node, "  ");

        if (definition.Children.TryGetValue(new YamlScalarNode("recommendations"), out var recommendations))
            DisplayRecommendations(recommendations, "  ");
    }

    private static void DisplayQuestion(YamlNode node, string indent)
    {
        if (node is not YamlMappingNode mapping) return;
        Console.WriteLine($"{indent}- [{Scalar(mapping, "id")}] {Scalar(mapping, "text")}");
        foreach (var field in new[] { "cardinality", "default" })
            if (Scalar(mapping, field) is { } value) Console.WriteLine($"{indent}  {field}: {value}");
        if (mapping.Children.TryGetValue(new YamlScalarNode("connection"), out var connection) &&
            connection is YamlMappingNode link)
            Console.WriteLine($"{indent}  connection: {Scalar(link, "text")}");
        if (mapping.Children.TryGetValue(new YamlScalarNode("recommendations"), out var recommendations))
            DisplayRecommendations(recommendations, indent + "  ");
        if (mapping.Children.TryGetValue(new YamlScalarNode("children"), out var children) &&
            children is YamlSequenceNode sequence)
            foreach (var child in sequence.Children) DisplayQuestion(child, indent + "  ");
    }

    private static void DisplayRecommendations(YamlNode node, string indent)
    {
        if (node is not YamlSequenceNode sequence) return;
        Console.WriteLine(indent + "recommendations:");
        foreach (var item in sequence.Children)
        {
            if (item is not YamlMappingNode recommendation) continue;
            var target = Scalar(recommendation, "document") ?? Scalar(recommendation, "nexus") ?? "<unknown>";
            Console.WriteLine($"{indent}  - {target}: {Scalar(recommendation, "role")}");
        }
    }

    private static string? RootQuestionText(YamlMappingNode definition) =>
        definition.Children.TryGetValue(new YamlScalarNode("question"), out var node) &&
        node is YamlMappingNode question ? Scalar(question, "text") : null;

    private static YamlMappingNode Mapping(YamlMappingNode parent, string key) =>
        parent.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlMappingNode mapping
            ? mapping : throw new InvalidDataException($"STDDISC008: Missing {key} definition mapping.");

    private static string? Scalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode value
            ? value.Value : null;
}
