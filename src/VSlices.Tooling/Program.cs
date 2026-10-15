using System.Text.Json;
using VSlices.Tooling;

ConsoleApp.Version = CliVersion.Display;

if (args is not ["--cli-schema"] && SemanticRefactoringCompanionHealth.ShouldNotify(args))
{
    Console.Error.WriteLine(
        "UPD016: The semantic-refactoring companion is not installed for this VSlices build.");
    Console.Error.WriteLine(
        "Run 'vslices update self' once more to finish the update.");
    Console.Error.WriteLine();
}

if (args is ["-v"] or ["--version"])
{
    if (!Console.IsOutputRedirected)
    {
        TerminalOutput.Brand("tooling");
        TerminalOutput.Detail("Version", CliVersion.Display);
    }
    else
    {
        Console.WriteLine(CliVersion.Display);
    }

    return;
}

if (!Console.IsOutputRedirected && args.Length > 0)
{
    if (args[0].Equals("init", StringComparison.OrdinalIgnoreCase))
        TerminalOutput.Brand("init");
    else if (args[0].Equals("update", StringComparison.OrdinalIgnoreCase))
        TerminalOutput.Brand("update", trailingBlankLine: false);
    else if (args[0].Equals("discovery", StringComparison.OrdinalIgnoreCase))
        TerminalOutput.Brand("discovery", trailingBlankLine: false);
    else if (args[0].Equals("read", StringComparison.OrdinalIgnoreCase))
        TerminalOutput.Brand("read", trailingBlankLine: false);
}

var app = ConsoleApp.Create();

app.Add("init", InitCommands.Init);
app.Add("new vsir", NewCommands.Vsir);
app.Add("new docs-standard", DocsStandardCommands.New);
app.Add("new ruleset-standard", RulesetStandardCommands.New);
app.Add("list ruleset-standards", RulesetStandardCommands.List);
app.Add("discovery ruleset-standard", RulesetStandardDiscoveryCommands.Discover);
app.Add("delete ruleset-standard", RulesetStandardCommands.Delete);
app.Add("list docs-standards", DocsStandardCommands.List);
app.Add("delete docs-standard", DocsStandardCommands.Delete);
app.Add("discovery docs-standard", DocsStandardDiscoveryCommands.Discover);
app.Add("new document", NewCommands.Document);
app.Add("new support-note", NewCommands.SupportNote);
app.Add("new nexus", NewCommands.Nexus);
app.Add("new continuity-path", NewCommands.ContinuityPath);
app.Add("discovery vsir", DiscoveryCommands.Vsir);
app.Add("discovery document", DocumentDiscoveryCommands.Discover);
app.Add("read document", QuestionReadCommands.Document);
app.Add("read nexus", QuestionReadCommands.Nexus);
app.Add("read continuity-path", QuestionReadCommands.ContinuityPath);
app.Add("read support-note", QuestionReadCommands.SupportNote);
app.Add("discovery support-note", SupportNoteDiscoveryCommands.Discover);
app.Add("discovery nexus", RelationalDiscoveryCommands.Nexus);
app.Add("discovery continuity-path", RelationalDiscoveryCommands.ContinuityPath);
app.Add("search", SearchCommands.Search);
app.Add("update vsir", UpdateCommands.Vsir);
app.Add("update document", UpdateCommands.Document);
app.Add("update support-note", SupportNoteUpdateCommands.Update);
app.Add("update nexus", RelationalUpdateCommands.Nexus);
app.Add("update continuity-path", RelationalUpdateCommands.ContinuityPath);
app.Add("update self", UpdateCommands.Self);
app.Add("update ruleset", UpdateCommands.Ruleset); // Deprecated legacy singleton installer.
app.Add("update ruleset-standard", UpdateCommands.RulesetStandard);
app.Add("update docs-standard", UpdateCommands.DocsStandard);
app.Add("update template-standard", UpdateCommands.TemplateStandard);
app.Add("lower", VsirCommands.Lower);
app.Add("transpile", VsirCommands.Transpile);
app.Add("rebase", VsirCommands.Rebase);

// Machine-readable schema is generated from these registered commands, not a second catalogue.
// Handle it before executing any command, and suppress all human terminal decoration.
if (args is ["--cli-schema"])
{
    var schema = app.GetCliSchema();
    Console.WriteLine(JsonSerializer.Serialize(
        schema,
        CliSchemaJsonSerializerContext.Default.CommandHelpDefinitionArray));
    return;
}

app.Run(args);
