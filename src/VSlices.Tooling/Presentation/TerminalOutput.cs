using Kokuban;
using Kokuban.AnsiEscape;
using Kurukuru;

namespace VSlices.Tooling;

internal static class TerminalOutput
{
    private const int DetailLabelWidth = 14;
    private const string DetailGutter = "  ";

    private static readonly AnsiStyle BrandStyle = Chalk.Rgb(156, 47, 160);

    public static void Brand(string? context = null, bool trailingBlankLine = true)
    {
        if (Console.IsOutputRedirected)
            return;

        var logo = new[]
        {
            @"\ \       / /",
            @" \ \     / / ",
            @"  \ \   / /  ",
            @"   \ \ / /   ",
            @"    \ V /    ",
            @"     \ /     ",
            @"      V      "
        };

        for (var index = 0; index < logo.Length; index++)
        {
            var suffix = index switch
            {
                1 => "  " + Chalk.Bold["VSlices"],
                2 when !string.IsNullOrWhiteSpace(context) => "  " + Chalk.Gray[context],
                _ => string.Empty
            };

            Console.WriteLine(BrandStyle[logo[index]] + suffix);
        }

        if (trailingBlankLine)
            BlankLine();
    }

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

    public static void Detail(string label, string value) =>
        Console.WriteLine(
            $"  {Chalk.Gray[label.PadRight(DetailLabelWidth)]}{DetailGutter}{Chalk.Bold[value]}");

    public static void BlankLine() =>
        Console.WriteLine();

    public static void Progress(string text, Action action) =>
        Spinner.Start(text, action);

    public static Task ProgressAsync(string text, Func<Task> action) =>
        Spinner.StartAsync(text, action);
}
