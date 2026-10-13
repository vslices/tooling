using System.Text;
using VSlices.Vsir.CSharp;

namespace VSlices.Tooling;

/// <summary>Rule-level comparison; unknown or unreferenced files are still reported as changed paths.</summary>
internal static class RulesetStandardDiff
{
    public static string Create(string name, string oldRoot, string nextRoot, string source)
    {
        var before = CSharpLoweringRuleSet.Load(oldRoot);
        var after = CSharpLoweringRuleSet.Load(nextRoot);
        if (!before.IsSuccess || !after.IsSuccess)
            throw new InvalidDataException("RSDIFF001: Cannot differentiate a ruleset with invalid C# lowering rules.");
        var previous = before.RuleSet!.Rules.ToDictionary(r => r.Node, StringComparer.Ordinal);
        var candidate = after.RuleSet!.Rules.ToDictionary(r => r.Node, StringComparer.Ordinal);
        var output = new StringBuilder();
        output.AppendLine($"# Ruleset Standard update — {name}");
        output.AppendLine();
        output.AppendLine($"Source: `{source.Replace("`", "'", StringComparison.Ordinal)}`");
        output.AppendLine();
        AddList(output, "Added nodes", candidate.Keys.Except(previous.Keys).OrderBy(x => x, StringComparer.Ordinal));
        AddList(output, "Removed nodes", previous.Keys.Except(candidate.Keys).OrderBy(x => x, StringComparer.Ordinal));
        output.AppendLine("## Modified nodes");
        var any = false;
        foreach (var node in candidate.Keys.Intersect(previous.Keys).OrderBy(x => x, StringComparer.Ordinal))
        {
            var old = previous[node];
            var now = candidate[node];
            if (old == now || (old.Mode == now.Mode && old.Renderer == now.Renderer &&
                old.Template == now.Template && old.Bindings.SequenceEqual(now.Bindings))) continue;
            any = true;
            output.AppendLine($"### {node}");
            Changed(output, "mode", old.Mode, now.Mode);
            Changed(output, "renderer", old.Renderer, now.Renderer);
            Changed(output, "bindings", string.Join(", ", old.Bindings), string.Join(", ", now.Bindings));
            Changed(output, "template", old.Template, now.Template);
        }
        if (!any) output.AppendLine("- None");
        output.AppendLine();

        output.AppendLine("## Source files");
        var oldFiles = Files(oldRoot);
        var newFiles = Files(nextRoot);
        AddList(output, "Added files", newFiles.Keys.Except(oldFiles.Keys).OrderBy(x => x, StringComparer.Ordinal));
        AddList(output, "Removed files", oldFiles.Keys.Except(newFiles.Keys).OrderBy(x => x, StringComparer.Ordinal));
        AddList(output, "Changed files", newFiles.Keys.Intersect(oldFiles.Keys)
            .Where(key => !File.ReadAllBytes(newFiles[key]).SequenceEqual(File.ReadAllBytes(oldFiles[key])))
            .OrderBy(x => x, StringComparer.Ordinal));
        output.AppendLine("## Compatibility");
        output.AppendLine("Rule/node and raw-source changes are recorded. Compatibility with authored VSIR and project extensions is not implied; validate lowering separately.");
        return output.ToString();
    }

    private static IReadOnlyDictionary<string, string> Files(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), path => path, StringComparer.Ordinal);

    private static void Changed(StringBuilder output, string property, string old, string next)
    {
        if (old == next) return;
        output.AppendLine($"- {property}: `{old.Replace("`", "'", StringComparison.Ordinal)}` → " +
            $"`{next.Replace("`", "'", StringComparison.Ordinal)}`");
    }

    private static void AddList(StringBuilder output, string heading, IEnumerable<string> items)
    {
        output.AppendLine($"## {heading}");
        var values = items.ToArray();
        if (values.Length == 0) output.AppendLine("- None");
        else foreach (var value in values) output.AppendLine($"- {value}");
        output.AppendLine();
    }
}
