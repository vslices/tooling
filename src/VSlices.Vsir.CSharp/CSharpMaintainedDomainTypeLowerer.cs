using System.Text;
using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

/// <summary>
/// C# realization for maintained Domain Types. The VSIR values section owns the
/// closed set of valid instances; no transform surface is synthesized.
/// </summary>
public static class CSharpMaintainedDomainTypeLowerer
{
    public static CSharpLoweringResult Lower(
        DomainTypeVsir document,
        CSharpLoweringContext context)
    {
        if (!string.Equals(document.Classification, "maintained", StringComparison.Ordinal))
            return new(null, [new("CSL110", "Maintained lowerer requires classification 'maintained'.")]);
        if (document.Values is null || document.Values.Count == 0)
            return new(null, [new("CSL111", "Maintained lowering requires at least one maintained value.")]);
        if (document.Equality is null)
            return new(null, [new("CSL112", "Maintained lowering requires explicit equality semantics.")]);

        var diagnostics = new List<VsirDiagnostic>();
        foreach (var field in document.State.Fields.Concat(document.Representation.Fields))
            ValidateType(field.Type, context.Rules, diagnostics);

        foreach (var state in document.State.Fields)
        {
            if (state.Type != new NamedVsirType("string"))
            {
                diagnostics.Add(new(
                    "CSL113",
                    $"Maintained value literal lowering is currently evidenced only for string state; state.{state.Name} is '{state.Type}'."));
            }
        }

        ValidateEqualityRules(document.Equality, context.Rules, diagnostics);
        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        var source = new StringBuilder();
        source.AppendLine($"namespace {context.Namespace};");
        source.AppendLine();
        source.AppendLine($"public sealed class {document.Name} :");
        source.AppendLine($"    Maintained<{document.Name}, {document.Name}.Repr>");
        source.AppendLine("{");
        source.AppendLine($"    public readonly record struct Repr({Parameters(document.Representation.Fields, context.Rules)});");

        foreach (var field in document.State.Fields)
        {
            source.AppendLine();
            source.AppendLine($"    public {RenderType(field.Type, context.Rules)} {field.Name} {{ get; }}");
        }

        source.AppendLine();
        source.AppendLine($"    private {document.Name}({Parameters(document.State.Fields, context.Rules, camelNames: true)}) =>");
        source.AppendLine($"        {ConstructorAssignment(document.State.Fields)};");

        foreach (var value in document.Values)
        {
            source.AppendLine();
            source.AppendLine($"    public static {document.Name} {value.Name} {{ get; }} =");
            source.AppendLine($"        new({string.Join(", ", document.State.Fields.Select(field => RenderMaintainedLiteral(field, value)))});");
        }

        source.AppendLine();
        source.AppendLine($"    public static Seq<{document.Name}> All {{ get; }} =");
        source.AppendLine("    [");
        for (var index = 0; index < document.Values.Count; index++)
            source.AppendLine($"        {document.Values[index].Name}{(index == document.Values.Count - 1 ? string.Empty : ",")}");
        source.AppendLine("    ];");

        source.AppendLine();
        RenderEquality(source, document.Name, document.Equality, context.Rules);

        source.AppendLine();
        source.AppendLine("    public Repr To() =>");
        source.AppendLine($"        new({string.Join(", ", document.Representation.Fields.Select(RenderRepresentationExpression))});");
        source.AppendLine("}");

        return new(source.ToString(), []);
    }

    private static string RenderRepresentationExpression(Field field)
    {
        if (!string.IsNullOrWhiteSpace(field.From))
            return RenderStateReference(field.From!);
        return field.Name;
    }

    private static string RenderStateReference(string reference)
    {
        if (!reference.StartsWith("state.", StringComparison.Ordinal))
            throw new InvalidOperationException($"Validated maintained representation reference '{reference}' is not state-based.");
        return reference["state.".Length..];
    }

    private static string RenderMaintainedLiteral(Field field, MaintainedValue value)
    {
        if (!value.State.TryGetValue(field.Name, out var literal))
            throw new InvalidOperationException($"Validated maintained value '{value.Name}' does not establish state.{field.Name}.");
        return field.Type == new NamedVsirType("string") ? Quote(literal) : literal;
    }

    private static void ValidateEqualityRules(
        EqualitySemantics equality,
        CSharpLoweringRuleSet rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var member = equality.By.StartsWith("state.", StringComparison.Ordinal)
            ? equality.By["state.".Length..]
            : equality.By;
        var node = equality.Intrinsic is not null ? $"equality.{equality.Intrinsic}" : "equality.over";
        if (!rules.TryRenderDeterministicExpression(
                node + ".equals",
                new Dictionary<string, string> { ["left"] = member, ["right"] = "other." + member },
                out _))
            diagnostics.Add(new("CSL021", "No deterministic C# equality rule is available for maintained equality."));
        if (!rules.TryRenderDeterministicExpression(
                node + ".hash",
                new Dictionary<string, string> { ["value"] = member },
                out _))
            diagnostics.Add(new("CSL022", "No deterministic C# equality hash rule is available for maintained equality."));
    }

    private static void RenderEquality(
        StringBuilder source,
        string typeName,
        EqualitySemantics equality,
        CSharpLoweringRuleSet rules)
    {
        var member = equality.By["state.".Length..];
        var node = equality.Intrinsic is not null ? $"equality.{equality.Intrinsic}" : "equality.over";
        rules.TryRenderDeterministicExpression(
            node + ".equals",
            new Dictionary<string, string> { ["left"] = member, ["right"] = "other." + member },
            out var equalsExpression);
        rules.TryRenderDeterministicExpression(
            node + ".hash",
            new Dictionary<string, string> { ["value"] = member },
            out var hashExpression);

        source.AppendLine($"    public bool Equals({typeName}? other) =>");
        source.AppendLine($"        other is not null && {equalsExpression};");
        source.AppendLine();
        source.AppendLine("    public override bool Equals(object? obj) =>");
        source.AppendLine($"        Equals(obj as {typeName});");
        source.AppendLine();
        source.AppendLine("    public override int GetHashCode() =>");
        source.AppendLine($"        {hashExpression};");
    }

    private static void ValidateType(VsirType type, CSharpLoweringRuleSet rules, ICollection<VsirDiagnostic> diagnostics)
    {
        if (type is not UnaryVsirType unary)
            return;
        ValidateType(unary.Value, rules, diagnostics);
        if (!rules.TryRenderDeterministicType(
                $"type.{unary.Constructor}",
                new Dictionary<string, string> { ["value"] = "T" },
                out _))
            diagnostics.Add(new("CSL050", $"Target Ruleset does not provide a deterministic type realization for semantic constructor '{unary.Constructor}'."));
    }

    private static string RenderType(VsirType type, CSharpLoweringRuleSet rules) => type switch
    {
        NamedVsirType named => named.Name,
        UnaryVsirType unary => RenderUnaryType(unary, rules),
        _ => throw new InvalidOperationException($"Unsupported semantic type model '{type.GetType().Name}'.")
    };

    private static string RenderUnaryType(UnaryVsirType unary, CSharpLoweringRuleSet rules)
    {
        var value = RenderType(unary.Value, rules);
        if (!rules.TryRenderDeterministicType(
                $"type.{unary.Constructor}",
                new Dictionary<string, string> { ["value"] = value },
                out var rendered))
            throw new InvalidOperationException($"Validated type rule 'type.{unary.Constructor}' became unavailable.");
        return rendered;
    }

    private static string Parameters(IReadOnlyList<Field> fields, CSharpLoweringRuleSet rules, bool camelNames = false) =>
        string.Join(", ", fields.Select(field =>
            $"{RenderType(field.Type, rules)} {(camelNames ? Camel(field.Name) : field.Name)}"));

    private static string ConstructorAssignment(IReadOnlyList<Field> fields)
    {
        if (fields.Count == 1)
            return $"{fields[0].Name} = {Camel(fields[0].Name)}";
        return $"({string.Join(", ", fields.Select(x => x.Name))}) = ({string.Join(", ", fields.Select(x => Camel(x.Name)))})";
    }

    private static string Camel(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
