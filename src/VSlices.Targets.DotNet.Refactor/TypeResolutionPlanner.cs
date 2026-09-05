using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace VSlices.Targets.DotNet.Refactor;

internal static class TypeResolutionPlanner
{
    public static async Task<int> Execute(
        TypeResolutionArguments options,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(options.ProjectPath))
        {
            await TypeResolutionManifestWriter.WriteFailure(
                options.ManifestPath,
                "DOTNET040",
                $"Project '{options.ProjectPath}' does not exist.",
                cancellationToken);
            return 1;
        }

        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(
            options.ProjectPath,
            cancellationToken: cancellationToken);
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            await TypeResolutionManifestWriter.WriteFailure(
                options.ManifestPath,
                "DOTNET041",
                $"Roslyn could not create a compilation for '{options.ProjectPath}'.",
                cancellationToken);
            return 1;
        }

        var assemblies = new[] { compilation.Assembly }
            .Concat(compilation.SourceModule.ReferencedAssemblySymbols)
            .Distinct<IAssemblySymbol>(SymbolEqualityComparer.Default)
            .ToArray();
        var resolved = new List<TypeResolutionEntry>();

        foreach (var typeName in options.TypeNames)
        {
            var candidates = assemblies
                .SelectMany(x => FindTopLevelTypes(x.GlobalNamespace, typeName))
                .Where(x =>
                    SymbolEqualityComparer.Default.Equals(x.ContainingAssembly, compilation.Assembly) ||
                    x.DeclaredAccessibility == Accessibility.Public)
                .GroupBy(
                    x => x.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    StringComparer.Ordinal)
                .Select(x => x.First())
                .ToArray();

            if (candidates.Length == 0)
            {
                await TypeResolutionManifestWriter.WriteFailure(
                    options.ManifestPath,
                    "DOTNET042",
                    $"Could not resolve nominal type '{typeName}' from project '{options.ProjectPath}' or its referenced assemblies.",
                    cancellationToken);
                return 1;
            }

            if (candidates.Length > 1)
            {
                var names = string.Join(
                    ", ",
                    candidates.Select(x => x.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                await TypeResolutionManifestWriter.WriteFailure(
                    options.ManifestPath,
                    "DOTNET043",
                    $"Nominal type '{typeName}' is ambiguous in the target project context: {names}.",
                    cancellationToken);
                return 1;
            }

            var symbol = candidates[0];
            resolved.Add(new(
                typeName,
                symbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : symbol.ContainingNamespace.ToDisplayString(),
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        await TypeResolutionManifestWriter.WriteSuccess(
            options.ManifestPath,
            resolved,
            cancellationToken);
        return 0;
    }

    private static IEnumerable<INamedTypeSymbol> FindTopLevelTypes(
        INamespaceSymbol namespaceSymbol,
        string typeName)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers(typeName))
        {
            if (type.Arity == 0 && type.ContainingType is null)
                yield return type;
        }

        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in FindTopLevelTypes(child, typeName))
                yield return type;
        }
    }
}
