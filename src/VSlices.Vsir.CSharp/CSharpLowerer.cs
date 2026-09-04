using System.Text;
using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

public sealed record CSharpLoweringContext(
    string Namespace,
    CSharpLoweringRuleSet Rules);

public sealed record CSharpLoweringResult(
    string? Source,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Source is not null && Diagnostics.Count == 0;
}

public static class CSharpLowerer
{
    public static CSharpLoweringResult Lower(DomainTypeVsir document, CSharpLoweringContext context)
    {
        var diagnostics = DomainTypeValidator.Validate(document).ToList();
        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        if (document.Traits.Contains("identifier", StringComparer.Ordinal))
        {
            diagnostics.Add(new(
                "CSL020",
                "No deterministic C# lowering mechanism is available yet for trait 'identifier'."));
        }

        if (document.Equality is not null)
        {
            diagnostics.Add(new(
                "CSL021",
                $"No deterministic C# lowering mechanism is available yet for equality '{document.Equality.Intrinsic}' by '{document.Equality.By}'."));
        }

        foreach (var ensure in document.Construction.Steps.OfType<EnsureStep>())
        {
            if (ensure.FailureMessage.Contains("{length}", StringComparison.Ordinal) &&
                ensure.Condition is not LengthAtMostCondition)
            {
                diagnostics.Add(new(
                    "CSL001",
                    "Placeholder '{length}' is currently only defined for length-at-most failures."));
            }

            var (node, bindings) = DescribeCondition(ensure.Condition);
            if (!context.Rules.TryRenderDeterministicExpression(node, bindings, out _))
            {
                diagnostics.Add(new(
                    "CSL010",
                    $"No deterministic C# lowering rule is available for '{node}'."));
            }
        }

        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        var typeName = document.Name;
        var inputFields = document.Construction.Input.Fields;
        var reprFields = document.Representation.Fields;
        var stateFields = document.State.Fields;

        var source = new StringBuilder();
        source.AppendLine($"namespace {context.Namespace};");
        source.AppendLine();
        source.AppendLine($"public sealed class {typeName} :");
        source.AppendLine($"    DomainType<{typeName}, {typeName}.Repr>,");
        source.AppendLine($"    Transform<{typeName}, {typeName}.Input>");
        source.AppendLine("{");
        source.AppendLine($"    public readonly record struct Repr({Parameters(reprFields)});");
        source.AppendLine();
        source.AppendLine($"    public readonly record struct Input({Parameters(inputFields)});");
        source.AppendLine();

        foreach (var field in stateFields)
            source.AppendLine($"    private readonly {CSharpType(field.Type)} _{Camel(field.Name)};");

        source.AppendLine();
        source.AppendLine($"    private {typeName}({Parameters(stateFields, camelNames: true)}) =>");
        source.AppendLine($"        {ConstructorAssignment(stateFields)};");
        source.AppendLine();
        source.AppendLine($"    public static VSlices.Arrows.Req<Input, {typeName}>.Full Invariants =>");

        var ensures = document.Construction.Steps.OfType<EnsureStep>().ToArray();
        for (var i = 0; i < ensures.Length; i++)
        {
            var prefix = i == 0 ? "        " : "        >> ";
            source.AppendLine(prefix + RenderEnsure(typeName, ensures[i], context.Rules));
        }

        source.AppendLine("        * Instance;");
        source.AppendLine();
        source.AppendLine($"    private static {typeName} Instance(Input input) =>");
        source.AppendLine($"        new({string.Join(", ", stateFields.Select(x => "input." + x.Name))});");
        source.AppendLine();
        source.AppendLine("    public Repr To() =>");
        source.AppendLine($"        new({string.Join(", ", reprFields.Select(x => "_" + Camel(x.Name)))});");
        source.AppendLine("}");

        return new(source.ToString(), []);
    }

    private static string RenderEnsure(
        string typeName,
        EnsureStep ensure,
        CSharpLoweringRuleSet rules)
    {
        var (node, bindings) = DescribeCondition(ensure.Condition);
        if (!rules.TryRenderDeterministicExpression(node, bindings, out var expression))
            throw new InvalidOperationException($"Validated lowering rule '{node}' became unavailable.");

        var predicate =
            $"VSlices.Arrows.Req<Input, {typeName}>.Ensure((Input input) => {expression}";

        return $"{predicate}, Fail: {RenderFailure(ensure)})";
    }

    private static (string Node, IReadOnlyDictionary<string, string> Bindings) DescribeCondition(Condition condition) =>
        condition switch
        {
            NonEmptyCondition x =>
                ("intrinsic.non-empty", new Dictionary<string, string>
                {
                    ["value"] = Reference(x.Value)
                }),
            LengthAtMostCondition x =>
                ("intrinsic.length-at-most", new Dictionary<string, string>
                {
                    ["value"] = Reference(x.Value),
                    ["max"] = x.Max.ToString()
                }),
            _ => throw new InvalidOperationException("Unsupported condition reached C# lowering.")
        };

    private static string RenderFailure(EnsureStep ensure)
    {
        var literal = Quote(ensure.FailureMessage);
        if (!ensure.FailureMessage.Contains("{length}", StringComparison.Ordinal))
            return literal;

        var condition = (LengthAtMostCondition)ensure.Condition;
        return $"(Input input) => {literal}.Replace(\"{{length}}\", {Reference(condition.Value)}.Length.ToString())";
    }

    private static string ConstructorAssignment(IReadOnlyList<Field> fields)
    {
        if (fields.Count == 1)
        {
            var field = fields[0];
            return $"_{Camel(field.Name)} = {Camel(field.Name)}";
        }

        var left = string.Join(", ", fields.Select(x => "_" + Camel(x.Name)));
        var right = string.Join(", ", fields.Select(x => Camel(x.Name)));
        return $"({left}) = ({right})";
    }

    private static string Reference(string value) => value;

    private static string Parameters(IReadOnlyList<Field> fields, bool camelNames = false) =>
        string.Join(", ", fields.Select(field =>
        {
            var name = camelNames ? Camel(field.Name) : field.Name;
            return $"{CSharpType(field.Type)} {name}";
        }));

    private static string CSharpType(string type) => type switch
    {
        "string" => "string",
        _ => throw new InvalidOperationException($"Unsupported type '{type}'.")
    };

    private static string Camel(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
