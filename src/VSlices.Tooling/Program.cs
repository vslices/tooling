using VSlices.Tooling;

ConsoleApp.Version = CliVersion.Display;

if (SemanticRefactoringCompanionHealth.ShouldNotify(args))
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
}

var app = ConsoleApp.Create();

app.Add("init", RulesetCommands.Init);
app.Add("new vsir", NewCommands.Vsir);
app.Add("discovery vsir", DiscoveryCommands.Vsir);
app.Add("update vsir", UpdateCommands.Vsir);
app.Add("update self", UpdateCommands.Self);
app.Add("update ruleset", UpdateCommands.Ruleset);
app.Add("lower", VsirCommands.Lower);
app.Add("transpile", VsirCommands.Transpile);
app.Add("rebase", VsirCommands.Rebase);

app.Run(args);