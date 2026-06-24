var app = ConsoleApp.Create();

app.Add("generate-document", (
    string type,
    string level = "L0",
    string language = "en") =>
{
    Console.WriteLine($"Generating {language} {level} {type} document.");
});

app.Run(args);