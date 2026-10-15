using ConsoleAppFramework;
using VSlices.Vsir.CSharp;
using YamlDotNet.Core;

namespace VSlices.Tooling;

internal static class RulesetStandardCommands
{
    /// <summary>Creates a local Ruleset Standard or imports an existing standard from --origin.</summary>
    /// <param name="name">Optional installation name; an explicit name overrides the origin manifest name.</param>
    /// <param name="origin">An existing physical directory, GitHub origin/ref, or supported archive URL.</param>
    public static Task<int> New([Argument] string? name = null, string? origin = null,
        CancellationToken cancellationToken = default) =>
        InProject(project => RulesetStandardLifecycle.New(project, name, origin, cancellationToken));

    /// <summary>Lists all locally authored and imported Ruleset Standards.</summary>
    public static Task<int> List() =>
        InProject(project => Task.FromResult(RulesetStandardLifecycle.List(project)));

    /// <summary>Removes one named Ruleset Standard installation, preserving external sources.</summary>
    /// <param name="name">Installed name to remove.</param>
    /// <param name="skipConfirm">Skip confirmation (for scripted use).</param>
    public static Task<int> Delete([Argument] string? name = null, bool skipConfirm = false,
        CancellationToken cancellationToken = default) =>
        InProject(project => RulesetStandardLifecycle.Delete(project, name, skipConfirm, cancellationToken));

    internal static async Task<int> InProject(Func<VSlicesProjectContext, Task<int>> action)
    {
        try
        {
            var project = VSlicesProjectContext.FindFrom(Environment.CurrentDirectory);
            if (project is null)
            {
                TerminalOutput.Error("RSCLI001: No VSlices project found. Run 'vslices init' first.");
                return 1;
            }
            return await action(project);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                   or ArgumentException or YamlException)
        {
            TerminalOutput.Error($"RSCLI002: {ex.Message}");
            return 1;
        }
    }
}

internal static class RulesetStandardDiscoveryCommands
{
    private sealed record Structure(RulesetStandardInstallation Standard, CSharpLoweringRule Rule)
    {
        public string Qualified => Standard.Name + "." + Rule.Node;
    }

    /// <summary>Explores admitted lowering nodes, bindings and rendering semantics across Ruleset Standards.</summary>
    /// <param name="rulesetStandard">Optional installed Ruleset Standard name.</param>
    /// <param name="structure">Lowering node (e.g. intrinsic.non-empty) or qualified installation.node.</param>
    public static Task<int> Discover(string? rulesetStandard = null, string? structure = null) =>
        RulesetStandardCommands.InProject(project =>
            Task.FromResult(Explore(project, rulesetStandard, structure)));

    private static int Explore(VSlicesProjectContext project, string? standardName, string? structureName)
    {
        var registry = RulesetStandardRegistry.Load(project);
        if (registry.Installations.Count == 0)
        {
            Console.WriteLine("No Ruleset Standards installed. Use 'vslices new ruleset-standard'.");
            return 0;
        }
        if (standardName is not null && registry.Find(standardName) is null)
        {
            TerminalOutput.Error($"RSDISC001: Ruleset Standard '{standardName}' is not installed.");
            return 2;
        }

        var candidates = new List<Structure>();
        var installed = registry.Installations
            .Where(s => standardName is null || s.Name.Equals(standardName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var standard in installed)
        {
            var root = registry.PathFor(standard);
            if (!Directory.Exists(Path.Combine(root, "csharp"))) continue; // An empty local standard is valid.
            var parsed = CSharpLoweringRuleSet.Load(root);
            if (!parsed.IsSuccess)
            {
                CommandInfrastructure.WriteDiagnostics(parsed.Diagnostics);
                return 2;
            }
            candidates.AddRange(parsed.RuleSet!.Rules.Select(rule => new Structure(standard, rule)));
        }

        if (structureName is not null)
        {
            candidates = candidates.Where(c => c.Rule.Node.Equals(structureName, StringComparison.Ordinal) ||
                c.Qualified.Equals(structureName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count == 0)
            {
                TerminalOutput.Error($"RSDISC002: Lowering node '{structureName}' was not admitted by the installed standards.");
                return 2;
            }
            var selected = Choose(candidates, "Choose a lowering node:", c => c.Qualified);
            if (selected is null) return 2;
            Show(selected);
            return 0;
        }

        if (standardName is null && installed.Length > 1)
        {
            var selected = Choose(installed, "Choose a Ruleset Standard:", x => x.Name);
            if (selected is null) return 2;
            candidates = candidates.Where(c => c.Standard.Name == selected.Name).ToList();
        }
        if (candidates.Count == 0)
        {
            Console.WriteLine("No target lowering rules have been admitted in this Ruleset Standard.");
            return 0;
        }
        var chosen = Choose(candidates, "Choose a lowering node:", c => c.Qualified);
        if (chosen is null) return 2;
        Show(chosen);
        return 0;
    }

    private static void Show(Structure structure)
    {
        var rule = structure.Rule;
        Console.WriteLine($"Structure: {structure.Qualified}");
        Console.WriteLine("  family: ruleset-standard");
        Console.WriteLine($"  standard: {structure.Standard.Name}");
        Console.WriteLine("  target: csharp");
        Console.WriteLine($"  node: {rule.Node}");
        Console.WriteLine($"  mode: {rule.Mode}");
        Console.WriteLine($"  renderer: {rule.Renderer}");
        Console.WriteLine($"  bindings: {string.Join(", ", rule.Bindings)}");
        Console.WriteLine($"  template: {rule.Template}");
    }

    private static T? Choose<T>(IReadOnlyList<T> options, string prompt, Func<T, string> label)
        where T : class
    {
        if (options.Count == 1) return options[0];
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            TerminalOutput.Error("RSDISC003: Ambiguous selection. Supply --ruleset-standard and --structure. Candidates: " +
                string.Join(", ", options.Select(label)));
            return null;
        }
        Console.WriteLine(prompt);
        for (var i = 0; i < options.Count; i++) Console.WriteLine($"  {i + 1}. {label(options[i])}");
        Console.Write("Selection: ");
        if (!int.TryParse(Console.ReadLine(), out var index) || index < 1 || index > options.Count)
        {
            TerminalOutput.Error("RSDISC004: No valid selection made.");
            return null;
        }
        return options[index - 1];
    }
}
