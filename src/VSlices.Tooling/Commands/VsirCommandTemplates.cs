namespace VSlices.Tooling;

internal sealed record VsirGrammarSlot(string Name, string ValueKind);

internal sealed record VsirGrammarForm(
    string Name,
    string Template,
    IReadOnlyList<VsirGrammarSlot> Slots);

internal sealed record VsirValueGrammar(
    string RootKind,
    IReadOnlyList<VsirGrammarForm> Forms);

internal static class VsirGrammarDiscovery
{
    private static readonly VsirValueGrammar RepresentationExpression = new(
        "expression",
        [
            new(
                "stringify",
                "{stringify: <semantic-reference>}",
                [new("value", "semantic-reference")]),
            new(
                "represent",
                "{represent: <semantic-reference>}",
                [new("value", "semantic-reference")]),
            new(
                "select",
                "{select: {source: <expression>, field: <field>}}",
                [
                    new("source", "expression"),
                    new("field", "field")
                ]),
            new(
                "map",
                "{map: {source: <expression>, bind: <name>, value: <expression>}}",
                [
                    new("source", "expression"),
                    new("bind", "name"),
                    new("value", "expression")
                ]),
            new(
                "intrinsic",
                "{intrinsic: <ruleset-intrinsic>, ...}",
                [new("intrinsic", "ruleset-intrinsic")])
        ]);

    public static string ValueKind(VsirPathContract contract) =>
        IsRepresentationMapping(contract.Path)
            ? RepresentationExpression.RootKind
            : contract.ValueKind;

    public static VsirValueGrammar? For(VsirPathContract contract) =>
        IsRepresentationMapping(contract.Path)
            ? RepresentationExpression
            : null;

    private static bool IsRepresentationMapping(string path) =>
        path.StartsWith("representation.", StringComparison.Ordinal) &&
        path.EndsWith(".mapping", StringComparison.Ordinal);
}

internal static class VsirCommandTemplates
{
    public static IReadOnlyList<string> For(
        string artifact,
        VsirPathContract contract)
    {
        var result = new List<string>();
        var commandPath = CommandPath(contract.Path);
        var placeholder = ValuePlaceholder(contract);

        foreach (var operation in contract.Operations.OrderBy(OperationOrder))
        {
            switch (operation)
            {
                case VsirMutationKind.Set:
                    result.Add($"vslices update vsir {artifact} --set \"{commandPath}={placeholder}\"");
                    if (contract.Path == "input")
                        result.Add($"vslices update vsir {artifact} --set \"input=<scalar-semantic-type>\"");
                    break;

                case VsirMutationKind.Add:
                    result.Add($"vslices update vsir {artifact} --add \"{commandPath}=<value>\"");
                    break;

                case VsirMutationKind.Remove:
                    result.Add(contract.Path is "tags" or "traits"
                        ? $"vslices update vsir {artifact} --remove \"{commandPath}=<value>\""
                        : $"vslices update vsir {artifact} --remove \"{commandPath}\"");
                    break;
            }
        }

        return result;
    }

    private static string CommandPath(string path) => path switch
    {
        "state" => "state.<property>",
        "representation" => "representation.<property>",
        "input" => "input.<property>",
        "variants" => "variants.<variant>",
        "values" => "values.<member>",
        _ => path
    };

    private static string ValuePlaceholder(VsirPathContract contract)
    {
        if (contract.AllowedValues is { Count: > 0 })
            return $"<one-of:{string.Join("|", contract.AllowedValues)}>";

        return contract.Path switch
        {
            "state" or "representation" or "input" => "<semantic-field-declaration>",
            "variants" => "<variant-declaration>",
            "values" => "<maintained-member-declaration>",
            _ => $"<{VsirGrammarDiscovery.ValueKind(contract).Replace(' ', '-').ToLowerInvariant()}>"
        };
    }

    private static int OperationOrder(VsirMutationKind kind) => kind switch
    {
        VsirMutationKind.Set => 0,
        VsirMutationKind.Add => 1,
        VsirMutationKind.Remove => 2,
        _ => 3
    };
}
