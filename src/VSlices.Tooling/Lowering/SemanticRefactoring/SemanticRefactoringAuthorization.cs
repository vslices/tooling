namespace VSlices.Tooling;

internal static class SemanticRefactoringAuthorization
{
    public static bool Confirm(TextReader input, TextWriter output)
    {
        output.Write("Apply semantic refactoring? [y/N] ");
        var answer = input.ReadLine()?.Trim();
        return answer?.Equals("y", StringComparison.OrdinalIgnoreCase) == true ||
               answer?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true;
    }
}
