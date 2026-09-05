using System.Runtime.CompilerServices;

namespace VSlices.Tooling;

internal static class SemanticRefactoringCompanionHealth
{
    private const string CompanionAssembly = "VSlices.Targets.DotNet.Refactor.dll";
    private const string BuildHostAssembly = "Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll";

    public static bool IsInstalled(string? baseDirectory = null) =>
        IsCompleteCompanionDirectory(Path.Combine(
            baseDirectory ?? AppContext.BaseDirectory,
            "refactor"));

    public static bool IsCompleteCompanionDirectory(string companionDirectory) =>
        File.Exists(Path.Combine(companionDirectory, CompanionAssembly)) &&
        File.Exists(Path.Combine(
            companionDirectory,
            "BuildHost-netcore",
            BuildHostAssembly));

    public static bool ShouldNotify(
        IReadOnlyList<string> args,
        string? executablePath = null,
        string? baseDirectory = null,
        bool? isNativeAot = null)
    {
        executablePath ??= Environment.ProcessPath;
        isNativeAot ??= !RuntimeFeature.IsDynamicCodeSupported;

        if (!isNativeAot.Value || !IsStandaloneVslices(executablePath))
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
