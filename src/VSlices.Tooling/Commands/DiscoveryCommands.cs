using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class DiscoveryCommands
{
    /// <summary>Shows the immediate semantic mutation frontier for a VSIR artifact.</summary>
    /// <param name="artifact">VSIR symbol or path.</param>
    /// <param name="add">Projected collection additions as semicolon-separated path=value clauses. Add is reserved for collection-valued surfaces such as tags and traits.</param>
    /// <param name="remove">Projected removals as semicolon-separated clauses. Set-valued surfaces use path=value; removable assertions use path alone.</param>
    /// <param name="set">Projected semantic assertions as semicolon-separated path=value clauses. Set establishes a missing assertion or replaces an existing one when the advertised path permits it.</param>
    public static async Task<int> Vsir(
        [Argument] string artifact,
        string? add = null,
        string? remove = null,
        string? set = null,
        CancellationToken cancellationToken = default)
    {
        var resolution = CommandInfrastructure.ResolveVsir(artifact, Environment.CurrentDirectory);
        if (resolution.Diagnostic is not null)
        {
            CommandInfrastructure.WriteDiagnostics([resolution.Diagnostic]);
            return 1;
        }

        var source = await File.ReadAllTextAsync(resolution.Path!, cancellationToken);
        var projections = new List<VsirMutation>();
        var parseError = UpdateCommands.AddGenericMutations(projections, VsirMutationKind.Add, add)
            ?? UpdateCommands.AddGenericMutations(projections, VsirMutationKind.Remove, remove)
            ?? UpdateCommands.AddGenericMutations(projections, VsirMutationKind.Set, set);
        if (parseError is not null)
        {
            TerminalOutput.Error(parseError.Replace("UPDATE020", "DISC020", StringComparison.Ordinal));
            return 2;
        }

        var inspectedSource = source;
        if (projections.Count > 0)
        {
            var projected = VsirMutationPipeline.Apply(source, projections);
            if (!projected.IsSuccess)
            {
                TerminalOutput.Error(projected.Error!.Replace("UPDATE", "DISC", StringComparison.Ordinal));
                return 2;
            }

            inspectedSource = projected.Source!;
        }

        var frontier = VsirMutationPipeline.Discover(inspectedSource, out var error);
        if (error is not null)
        {
            TerminalOutput.Error(error);
            return 2;
        }

        if (projections.Count > 0)
            Console.WriteLine("Projected immediate frontier:");
        else
            Console.WriteLine("Immediate frontier:");

        foreach (var path in frontier)
        {
            Console.WriteLine();
            Console.WriteLine(path.Path);
            Console.WriteLine($"  status: {DisplayStatus(path.Status)}");
            Console.WriteLine($"  meaning: {path.Meaning}");
            Console.WriteLine($"  value kind: {VsirGrammarDiscovery.ValueKind(path)}");
            Console.WriteLine(path.Operations.Count == 0
                ? "  operations: not implemented"
                : $"  operations: {string.Join(", ", path.Operations.Select(DisplayOperation))}");
            if (path.AllowedValues is { Count: > 0 })
                Console.WriteLine($"  values: {string.Join(", ", path.AllowedValues)}");

            foreach (var template in VsirCommandTemplates.For(artifact, path))
                Console.WriteLine($"  command: {template}");

            var grammar = VsirGrammarDiscovery.For(path);
            if (grammar is not null)
            {
                Console.WriteLine($"  grammar root: {grammar.RootKind}");
                foreach (var form in grammar.Forms)
                {
                    Console.WriteLine($"  grammar {form.Name}: {form.Template}");
                    foreach (var slot in form.Slots)
                        Console.WriteLine($"    {slot.Name}: {slot.ValueKind}");
                }
            }
        }

        return 0;
    }

    private static string DisplayOperation(VsirMutationKind kind) =>
        kind.ToString().ToLowerInvariant();

    private static string DisplayStatus(VsirFrontierStatus status) =>
        status.ToString().ToLowerInvariant();
}
