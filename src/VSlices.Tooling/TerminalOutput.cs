using Kokuban;
using Kurukuru;

namespace VSlices.Tooling;

internal static class TerminalOutput
{
    public static void Heading(string text) =>
        Console.WriteLine(Chalk.Bold[text]);

    public static void Info(string text) =>
        Console.WriteLine(Chalk.Cyan[text]);

    public static void Success(string text) =>
        Console.WriteLine(Chalk.Green[text]);

    public static void Warning(string text) =>
        Console.WriteLine(Chalk.Yellow[text]);

    public static void Muted(string text) =>
        Console.WriteLine(Chalk.Gray[text]);

    public static void Error(string text) =>
        Console.Error.WriteLine(Chalk.Red[text]);

    public static void Progress(string text, Action action) =>
        Spinner.Start(text, action);

    public static Task ProgressAsync(string text, Func<Task> action) =>
        Spinner.StartAsync(text, action);
}
