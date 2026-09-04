namespace VSlices.Tooling;

internal static class SemanticRefactoringCompanionHealth
{
    private const string CompanionAssembly = "VSlices.Targets.DotNet.Refactor.dll";

    public static bool IsInstalled(string? baseDirectory = null) =>
        File.Exists(Path.Combine(
            baseDirectory ?? AppContext.BaseDirectory,
            "refactor",
            CompanionAssembly));

    public static bool ShouldNotify(
        IReadOnlyList<string> args,
        string? executablePath = null,
        string? baseDirectory = null)
    {
        executablePath ??= Environment.ProcessPath;
        if (!IsStandaloneVslices(executablePath))
            return false;

        if (IsSelfUpdate(args))
            return false;

        return !IsInstalled(baseDirectory);
    }

    private static bool IsStandaloneVslices(string? executablePath) =>
        !string.IsNullOrWhiteSpace(executablePath) &&
        Path.GetFileNameWithoutExtension(executablePath)
            .Equals("vslices", StringComparison.OrdinalIgnoreCase);

    private static bool IsSelfUpdate(IReadOnlyList<string> args) =>
        args.Count >= 2 &&
        args[0].Equals("update", StringComparison.OrdinalIgnoreCase) &&
        args.Skip(1).Any(x => x.Equals("--self", StringComparison.OrdinalIgnoreCase));
}
