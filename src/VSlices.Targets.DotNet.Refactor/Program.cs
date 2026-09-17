using Microsoft.Build.Locator;
using VSlices.Targets.DotNet.Refactor;

if (args.Length > 0 && args[0] == "resolve-types")
{
    var typeOptions = TypeResolutionArguments.Parse(args[1..]);
    if (typeOptions is null)
    {
        Console.Error.WriteLine("Usage: VSlices.Targets.DotNet.Refactor resolve-types --project <csproj> --type <name> [--type <name> ...] --manifest <json>");
        return 2;
    }

    try
    {
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        return await TypeResolutionPlanner.Execute(typeOptions, CancellationToken.None);
    }
    catch (Exception ex)
    {
        await TypeResolutionManifestWriter.WriteFailure(
            typeOptions.ManifestPath,
            "DOTNET044",
            $"Roslyn nominal type resolution failed: {ex.Message}",
            CancellationToken.None);
        return 1;
    }
}

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
