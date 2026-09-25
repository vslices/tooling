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

    /// <summary>Creates the minimal progressive Markdown artifact for a Docs Standard document type.</summary>
    /// <param name="name">Document name or path. The .md extension is added when omitted.</param>
    /// <param name="kind">Document type defined by the installed VSlices Docs Standard.</param>
    public static async Task<int> Document(
        [Argument] string name,
        string? kind = null,
        CancellationToken cancellationToken = default)
    {
        var standardRoot = DocsStandardCatalog.FindInstalledRoot(Environment.CurrentDirectory);
        if (standardRoot is null)
        {
            Console.Error.WriteLine(
                "NEW104: Could not locate an installed Docs Standard snapshot at .vslices/docs-standard. Run 'vslices update docs-standard' to install it.");
            return 1;
        }

        var catalog = DocsStandardCatalog.Load(standardRoot);
        if (!catalog.IsSuccess)
        {
            Console.Error.WriteLine(catalog.Error);
            return 1;
        }

        var materialization = DocumentMaterializationEnvironment.Resolve(
            Environment.CurrentDirectory);
        if (!materialization.IsSuccess)
        {
            Console.Error.WriteLine(materialization.Error);
            return 1;
        }

        var result = DocumentTemplate.Create(
            name,
            kind,
            catalog.Catalog!,
            materialization.Template!);
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Error);
            return 2;
        }

        var path = DocumentPathResolver.Resolve(
            name,
            Environment.CurrentDirectory);
        if (!path.IsSuccess)
        {
            TerminalOutput.Error(path.Error!);
            return 2;
        }

        return await CommandInfrastructure.WriteResult(
            result.Source!,
            path.Path!,
            output: null,
            stdout: false,
            overwrite: false,
            cancellationToken);
    }
}
