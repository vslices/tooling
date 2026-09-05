using Microsoft.Build.Locator;
using VSlices.Targets.DotNet.Refactor;

var options = RefactorArguments.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: VSlices.Targets.DotNet.Refactor --project <csproj> --document <cs> --candidate <cs> --symbol <name> --staging <dir> --manifest <json>");
    return 2;
}

try
{
    if (!MSBuildLocator.IsRegistered)
        MSBuildLocator.RegisterDefaults();

    return await NamespaceMovePlanner.Execute(options, CancellationToken.None);
}
catch (Exception ex)
{
    await RefactorManifest.WriteFailure(
        options.ManifestPath,
        "DOTNET020",
        $"Roslyn semantic refactoring failed: {ex.Message}");
    return 1;
}
