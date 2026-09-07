namespace VSlices.Tooling;

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
            _ => $"<{contract.ValueKind.Replace(' ', '-').ToLowerInvariant()}>"
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
