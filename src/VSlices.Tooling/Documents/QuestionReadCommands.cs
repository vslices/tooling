using ConsoleAppFramework;
using YamlDotNet.Core;

namespace VSlices.Tooling;

internal static class QuestionReadCommands
{
    /// <summary>Reads one Document question and its complete answer without modifying the artifact.</summary>
    /// <param name="document">Document filename or path; .md is optional.</param>
    /// <param name="questionId">Current selection from 'vslices discovery document' (for example 2.1).</param>
    /// <param name="deep">Include the full route of ancestor questions and their answers.</param>
    public static Task<int> Document(
        [Argument] string document, string? questionId = null, bool deep = false,
        CancellationToken cancellationToken = default) =>
        Execute(() => QuestionReadOperation.Document(document, questionId ?? "", deep, cancellationToken));

    /// <summary>Reads one Nexus question and answer, optionally with its question ancestry.</summary>
    /// <param name="artifact">Nexus name or path, including directory-backed README artifacts.</param>
    /// <param name="questionId">Current question selection from Nexus discovery.</param>
    /// <param name="deep">Include the ancestor question/answer route.</param>
    public static Task<int> Nexus(
        [Argument] string artifact, string? questionId = null, bool deep = false,
        CancellationToken cancellationToken = default) =>
        Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(QuestionReadOperation.Relational(
                artifact, "nexus", questionId ?? "", deep));
        });

    /// <summary>Reads one Continuity Path question and answer, optionally with its trajectory.</summary>
    /// <param name="artifact">Continuity Path name or path, including directory-backed README artifacts.</param>
    /// <param name="questionId">Current question selection from Continuity Path discovery.</param>
    /// <param name="deep">Include the ancestor question/answer route.</param>
    public static Task<int> ContinuityPath(
        [Argument] string artifact, string? questionId = null, bool deep = false,
        CancellationToken cancellationToken = default) =>
        Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(QuestionReadOperation.Relational(
                artifact, "continuity-path", questionId ?? "", deep));
        });

    /// <summary>Reads a Support Note question, using the same question selection contract.</summary>
    /// <param name="artifact">Support Note name or path.</param>
    /// <param name="questionId">Current question selection from Support Note discovery.</param>
    /// <param name="deep">Include the ancestor question/answer route.</param>
    public static Task<int> SupportNote(
        [Argument] string artifact, string? questionId = null, bool deep = false,
        CancellationToken cancellationToken = default) =>
        Execute(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(QuestionReadOperation.SupportNote(
                artifact, questionId ?? "", deep));
        });

    private static async Task<int> Execute(Func<Task<QuestionReadResult>> read)
    {
        try
        {
            var result = await read();
            if (!result.IsSuccess)
            {
                TerminalOutput.Error(result.Error!);
                return 2;
            }

            var questions = result.Questions!;
            var ancestry = questions.Count > 1;
            for (var index = 0; index < questions.Count; index++)
            {
                var question = questions[index];
                if (index > 0)
                    TerminalOutput.BlankLine();

                WriteQuestion(ancestry
                    ? $"[{question.SelectionPath}] {question.Text}"
                    : $"Question: {question.Text}");

                if (string.IsNullOrWhiteSpace(question.Answer))
                    continue;

                var lines = question.Answer.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n').Split('\n');
                var prefix = ancestry ? "  " : "Answer: ";
                Console.WriteLine(prefix + lines[0]);
                foreach (var line in lines.Skip(1))
                    Console.WriteLine(new string(' ', prefix.Length) + line);
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   InvalidDataException or ArgumentException or YamlException)
        {
            TerminalOutput.Error($"READ011: Could not read artifact: {ex.Message}");
            return 1;
        }
    }

    private static void WriteQuestion(string text)
    {
        if (Console.IsOutputRedirected)
            Console.WriteLine(text);
        else
            TerminalOutput.Heading(text);
    }
}
