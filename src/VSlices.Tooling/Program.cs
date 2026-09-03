using VSlices.Tooling;

var app = ConsoleApp.Create();

app.Add("generate-document", (
    string type,
    string level = "L0",
    string language = "en") =>
{
    Console.WriteLine($"Generating {language} {level} {type} document.");
});

app.Add("transpile", VsirCommands.Transpile);
app.Add("rebase", VsirCommands.Rebase);

app.Run(args);