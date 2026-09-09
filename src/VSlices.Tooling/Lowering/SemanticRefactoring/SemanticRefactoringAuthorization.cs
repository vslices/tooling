namespace VSlices.Tooling;

internal static class SemanticRefactoringAuthorization
{
    public static bool ConfirmAnalysis(TextReader input, TextWriter output)
    {
        output.Write("Analyze semantic blast radius with Roslyn? [y/N] ");
        return IsExplicitYes(input.ReadLine());
    }

    public static bool Confirm(TextReader input, TextWriter output)
    {
        output.Write("Apply semantic refactoring? [y/N] ");
        return IsExplicitYes(input.ReadLine());
    }

    private static bool IsExplicitYes(string? answer)
    {
        var normalized = answer?.Trim();
        return normalized?.Equals("y", StringComparison.OrdinalIgnoreCase) == true ||
               normalized?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true;
    }
}
