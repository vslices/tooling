using VSlices.Tooling;

var app = ConsoleApp.Create();

app.Add("transpile", VsirCommands.Transpile);
app.Add("rebase", VsirCommands.Rebase);

app.Run(args);