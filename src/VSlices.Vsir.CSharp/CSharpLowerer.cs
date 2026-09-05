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

        var validationReferences = InitialInputReferences(document.Construction.Input.Fields);
        foreach (var step in document.Construction.Steps)
        {
            switch (step)
            {
                case NormalizeStep normalize:
                {
                    var (node, bindings) = DescribeNormalization(normalize, validationReferences);
                    if (!context.Rules.TryRenderDeterministicExpression(node, bindings, out var normalizedExpression))
                    {
                        diagnostics.Add(new(
                            "CSL031",
                            $"No deterministic C# normalization rule is available for '{node}'."));
                        break;
                    }

                    validationReferences[normalize.Target] = normalizedExpression;
                    break;
                }
                case EnsureStep ensure:
                {
                    if (ensure.FailureMessage.Contains("{length}", StringComparison.Ordinal) &&
                        ensure.Condition is not LengthAtMostCondition)
                    {
                        diagnostics.Add(new(
                            "CSL001",
                            "Placeholder '{length}' is currently only defined for length-at-most failures."));
                    }

                    var (node, bindings) = DescribeCondition(ensure.Condition, validationReferences);
                    if (!context.Rules.TryRenderDeterministicExpression(node, bindings, out _))
                    {
                        diagnostics.Add(new(
                            "CSL010",
                            $"No deterministic C# lowering rule is available for '{node}'."));
                    }

                    break;
                }
            }
        }

        if (document.Equality is not null)
            ValidateEqualityRules(document.Equality, context.Rules, diagnostics);

        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        var typeName = document.Name;
        var inputFields = document.Construction.Input.Fields;
        var reprFields = document.Representation.Fields;
        var stateFields = document.State.Fields;
        var isIdentifier = document.Traits.Contains("identifier", StringComparer.Ordinal);
        var renderedReferences = InitialInputReferences(inputFields);
        var renderedEnsures = new List<string>();

        foreach (var step in document.Construction.Steps)
        {
            switch (step)
            {
                case NormalizeStep normalize:
                {
                    var (node, bindings) = DescribeNormalization(normalize, renderedReferences);
                    if (!context.Rules.TryRenderDeterministicExpression(node, bindings, out var normalizedExpression))
                        throw new InvalidOperationException($"Validated normalization rule '{node}' became unavailable.");

                    renderedReferences[normalize.Target] = normalizedExpression;
                    break;
                }
                case EnsureStep ensure:
                    renderedEnsures.Add(RenderEnsure(typeName, ensure, context.Rules, renderedReferences));
                    break;
            }
        }

        var source = new StringBuilder();
        source.AppendLine($"namespace {context.Namespace};");
        source.AppendLine();
        source.AppendLine($"public sealed class {typeName} :");
        source.AppendLine(isIdentifier
            ? $"    Identifier<{typeName}, {typeName}.Repr>,"
            : $"    DomainType<{typeName}, {typeName}.Repr>,");
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

        if (renderedEnsures.Count == 0)
        {
            source.AppendLine($"        VSlices.Arrows.Req<Input, {typeName}>.Transform((Input input) => Instance(input));");
        }
        else
        {
            for (var i = 0; i < renderedEnsures.Count; i++)
            {
                var prefix = i == 0 ? "        " : "        >> ";
                source.AppendLine(prefix + renderedEnsures[i]);
            }

            source.AppendLine("        * Instance;");
        }

        source.AppendLine();
        source.AppendLine($"    private static {typeName} Instance(Input input) =>");
        source.AppendLine($"        new({string.Join(", ", stateFields.Select(x => ResolveReference("input." + x.Name, renderedReferences)))});");

        if (document.Equality is not null)
        {
            source.AppendLine();
            RenderEquality(source, typeName, document.Equality, context.Rules);
        }

        source.AppendLine();
        source.AppendLine("    public Repr To() =>");
        source.AppendLine($"        new({string.Join(", ", reprFields.Select(x => "_" + Camel(x.Name)))});");
        source.AppendLine("}");

        return new(source.ToString(), []);
    }

    private static void ValidateEqualityRules(
        EqualitySemantics equality,
        CSharpLoweringRuleSet rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var field = EqualityStateField(equality);
        var member = "_" + Camel(field);

        if (!rules.TryRenderDeterministicExpression(
                EqualityNode(equality, "equals"),
                new Dictionary<string, string>
                {
                    ["left"] = member,
                    ["right"] = "other." + member
                },
                out _))
        {
            diagnostics.Add(new(
                "CSL021",
                $"No deterministic C# equality rule is available for '{equality.Intrinsic}'."));
        }

        if (!rules.TryRenderDeterministicExpression(
                EqualityNode(equality, "hash"),
                new Dictionary<string, string>
                {
                    ["value"] = member
                },
                out _))
        {
            diagnostics.Add(new(
                "CSL022",
                $"No deterministic C# hash rule is available for equality '{equality.Intrinsic}'."));
        }
    }

    private static void RenderEquality(
        StringBuilder source,
        string typeName,
        EqualitySemantics equality,
        CSharpLoweringRuleSet rules)
    {
        var field = EqualityStateField(equality);
        var member = "_" + Camel(field);

        if (!rules.TryRenderDeterministicExpression(
                EqualityNode(equality, "equals"),
                new Dictionary<string, string>
                {
                    ["left"] = member,
                    ["right"] = "other." + member
                },
                out var equalsExpression))
        {
            throw new InvalidOperationException("Validated equality rule became unavailable.");
        }

        if (!rules.TryRenderDeterministicExpression(
                EqualityNode(equality, "hash"),
                new Dictionary<string, string>
                {
                    ["value"] = member
                },
                out var hashExpression))
        {
            throw new InvalidOperationException("Validated equality hash rule became unavailable.");
        }

        source.AppendLine($"    public bool Equals({typeName}? other) =>");
        source.AppendLine($"        other is not null && {equalsExpression};");
        source.AppendLine();
        source.AppendLine("    public override bool Equals(object? obj) =>");
        source.AppendLine($"        Equals(obj as {typeName});");
        source.AppendLine();
        source.AppendLine("    public override int GetHashCode() =>");
        source.AppendLine($"        {hashExpression};");
        source.AppendLine();
        source.AppendLine($"    public static bool operator ==({typeName}? left, {typeName}? right) =>");
        source.AppendLine("        Equals(left, right);");
        source.AppendLine();
        source.AppendLine($"    public static bool operator !=({typeName}? left, {typeName}? right) =>");
        source.AppendLine("        !(left == right);");
    }

    private static string EqualityNode(EqualitySemantics equality, string operation) =>
        $"equality.{equality.Intrinsic}.{operation}";

    private static string EqualityStateField(EqualitySemantics equality) =>
        equality.By["state.".Length..];

    private static string RenderEnsure(
        string typeName,
        EnsureStep ensure,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> references)
    {
        var (node, bindings) = DescribeCondition(ensure.Condition, references);
        if (!rules.TryRenderDeterministicExpression(node, bindings, out var expression))
            throw new InvalidOperationException($"Validated lowering rule '{node}' became unavailable.");

        var predicate =
            $"VSlices.Arrows.Req<Input, {typeName}>.Ensure((Input input) => {expression}";

        return $"{predicate}, Fail: {RenderFailure(ensure, references)})";
    }

    private static (string Node, IReadOnlyDictionary<string, string> Bindings) DescribeNormalization(
        NormalizeStep normalize,
        IReadOnlyDictionary<string, string> references) =>
        ($"intrinsic.{normalize.Intrinsic}", new Dictionary<string, string>
        {
            ["value"] = ResolveReference(normalize.Target, references)
        });

    private static (string Node, IReadOnlyDictionary<string, string> Bindings) DescribeCondition(
        Condition condition,
        IReadOnlyDictionary<string, string> references) =>
        condition switch
        {
            NonEmptyCondition x =>
                ("intrinsic.non-empty", new Dictionary<string, string>
                {
                    ["value"] = ResolveReference(x.Value, references)
                }),
            NotWhitespaceCondition x =>
                ("intrinsic.not-whitespace", new Dictionary<string, string>
                {
                    ["value"] = ResolveReference(x.Value, references)
                }),
            LengthAtMostCondition x =>
                ("intrinsic.length-at-most", new Dictionary<string, string>
                {
                    ["value"] = ResolveReference(x.Value, references),
                    ["max"] = x.Max.ToString()
                }),
            _ => throw new InvalidOperationException("Unsupported condition reached C# lowering.")
        };

    private static string RenderFailure(
        EnsureStep ensure,
        IReadOnlyDictionary<string, string> references)
    {
        var literal = Quote(ensure.FailureMessage);
        if (!ensure.FailureMessage.Contains("{length}", StringComparison.Ordinal))
            return literal;

        var condition = (LengthAtMostCondition)ensure.Condition;
        return $"(Input input) => {literal}.Replace(\"{{length}}\", {ResolveReference(condition.Value, references)}.Length.ToString())";
    }

    private static Dictionary<string, string> InitialInputReferences(IReadOnlyList<Field> fields) =>
        fields.ToDictionary(
            field => "input." + field.Name,
            field => "input." + field.Name,
            StringComparer.Ordinal);

    private static string ResolveReference(
        string reference,
        IReadOnlyDictionary<string, string> references) =>
        references.TryGetValue(reference, out var expression)
            ? expression
            : reference;

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
