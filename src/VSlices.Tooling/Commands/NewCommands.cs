using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class NewCommands
{
    /// <summary>Creates the minimal progressive VSIR artifact for a semantic name.</summary>
    /// <param name="name">Semantic name of the concept being introduced.</param>
    public static async Task<int> Vsir(
        [Argument] string name,
        CancellationToken cancellationToken = default)
    {
        var result = VsirTemplate.Create(name);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Error);
            return 2;
        }

        var defaultPath = Path.GetFullPath(
            name.EndsWith(".vsir", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".vsir",
            Environment.CurrentDirectory);

        return await CommandInfrastructure.WriteResult(
            result.Source!,
            defaultPath,
            output: null,
            stdout: false,
            overwrite: false,
            cancellationToken);
    }
}
