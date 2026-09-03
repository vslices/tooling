using VSlices.Tooling;

ConsoleApp.Version = CliVersion.Display;

if (args is ["-v"])
    args = ["--version"];

var app = ConsoleApp.Create();

app.Add("init", RulesetCommands.Init);
app.Add("lower", VsirCommands.Lower);
app.Add("transpile", VsirCommands.Transpile);
app.Add("rebase", VsirCommands.Rebase);
app.Add("update", UpdateCommands.Update);

app.Run(args);