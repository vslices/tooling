namespace VSlices.Tooling;

// Rendering is independent of the selection syntax: ancestry comes from parent references.
internal sealed record QuestionTreeNode(string Selection, string Text, string? Parent);

internal static class QuestionTreePresentation
{
    public static void Write(IEnumerable<QuestionTreeNode> source)
    {
        var nodes = source.ToArray();
        var children = nodes.GroupBy(x => x.Parent, StringComparer.Ordinal)
            .ToDictionary(g => g.Key ?? "", g => g.ToArray(), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(QuestionTreeNode node, string prefix, string branch)
        {
            if (!visited.Add(node.Selection)) return;
            Console.WriteLine(prefix + branch + "[" + node.Selection + "] " + node.Text);
            if (!children.TryGetValue(node.Selection, out var descendants)) return;
            for (var i = 0; i < descendants.Length; i++)
                Visit(descendants[i], prefix + (branch == "" ? "" :
                    branch == "└─ " ? "   " : "│  "),
                    i == descendants.Length - 1 ? "└─ " : "├─ ");
        }

        if (children.TryGetValue("", out var roots))
            for (var i = 0; i < roots.Length; i++)
                Visit(roots[i], "", "");
        foreach (var orphan in nodes.Where(x => !visited.Contains(x.Selection)))
            Visit(orphan, "", "");
    }
}
