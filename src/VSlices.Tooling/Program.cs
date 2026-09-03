using VSlices.Tooling;

ConsoleApp.Version = CliVersion.Display;

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
}

var app = ConsoleApp.Create();

app.Add("init", RulesetCommands.Init);
app.Add("lower", VsirCommands.Lower);
app.Add("transpile", VsirCommands.Transpile);
app.Add("rebase", VsirCommands.Rebase);
app.Add("update", UpdateCommands.Update);

app.Run(args);