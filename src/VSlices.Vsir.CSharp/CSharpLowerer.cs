using System.Text;
using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

public sealed record CSharpLoweringContext(string Namespace);

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

        foreach (var ensure in document.Construction.Steps.OfType<EnsureStep>())
        {
            if (ensure.FailureMessage.Contains("{length}", StringComparison.Ordinal) &&
                ensure.Condition is not LengthAtMostCondition)
            {
                diagnostics.Add(new(
                    "CSL001",
                    "Placeholder '{length}' is currently only defined for length-at-most failures."));
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
            source.AppendLine(prefix + RenderEnsure(typeName, ensures[i]));
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

    private static string RenderEnsure(string typeName, EnsureStep ensure)
    {
        var predicate = ensure.Condition switch
        {
            NonEmptyCondition x =>
                $"VSlices.Arrows.Req<Input, {typeName}>.Ensure((Input input) => !string.IsNullOrEmpty({Reference(x.Value)})",
            LengthAtMostCondition x =>
                $"VSlices.Arrows.Req<Input, {typeName}>.Ensure((Input input) => {Reference(x.Value)}.Length <= {x.Max}",
            _ => throw new InvalidOperationException("Unsupported condition reached C# lowering.")
        };

        return $"{predicate}, Fail: {RenderFailure(ensure)})";
    }

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
