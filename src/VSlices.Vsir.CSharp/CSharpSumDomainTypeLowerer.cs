using System.Text;
using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

/// <summary>
/// C# realization for canonical sum-shaped Domain Types. The root realizes the
/// closed semantic family while each variant realizes its own transform,
/// state, representation and construction boundary.
/// </summary>
public static class CSharpSumDomainTypeLowerer
{
    public static CSharpLoweringResult Lower(
        DomainTypeVsir document,
        CSharpLoweringContext context)
    {
        if (!string.Equals(document.Shape, "sum", StringComparison.Ordinal))
            return new(null, [new("CSL100", "Sum lowerer requires shape 'sum'.")]);
        if (document.Variants is null || document.Variants.Count == 0)
            return new(null, [new("CSL101", "Sum lowering requires at least one variant.")]);

        var diagnostics = new List<VsirDiagnostic>();
        foreach (var variant in document.Variants)
            ValidateVariantTypes(variant, context.Rules, diagnostics);
        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        var source = new StringBuilder();
        source.AppendLine($"namespace {context.Namespace};");
        source.AppendLine();
        source.AppendLine($"public abstract class {document.Name} :");
        source.AppendLine($"    DomainType<{document.Name}, {document.Name}.Repr>");
        source.AppendLine("{");
        source.AppendLine("    public abstract record Repr;");
        source.AppendLine();
        source.AppendLine("    public abstract Repr To();");
        source.AppendLine("}");

        foreach (var variant in document.Variants)
        {
            source.AppendLine();
            LowerVariant(source, document, variant, context, diagnostics);
        }

        return diagnostics.Count == 0
            ? new(source.ToString(), [])
            : new(null, diagnostics);
    }

    private static void LowerVariant(
        StringBuilder source,
        DomainTypeVsir root,
        DomainTypeVariant variant,
        CSharpLoweringContext context,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var inputType = variant.Construction.Input.IsScalar
            ? RenderType(variant.Construction.Input.ScalarType!, context.Rules)
            : variant.Name + ".Input";

        var references = InitialInputReferences(variant.Construction.Input);
        var pipeline = new List<string>();

        foreach (var step in variant.Construction.Steps)
        {
            switch (step)
            {
                case NormalizeStep normalize:
                    LowerNormalize(normalize, context.Rules, references, diagnostics, variant.Name);
                    break;
                case EnsureStep ensure:
                    LowerEnsure(variant, inputType, ensure, context.Rules, references, pipeline, diagnostics);
                    break;
                case RefineStep:
                    break;
                default:
                    diagnostics.Add(new(
                        "CSL102",
                        $"Construction step '{step.GetType().Name}' is not yet supported inside sum variant '{variant.Name}'."));
                    break;
            }
        }

        var stateExpressions = ResolveStateExpressions(variant, references, diagnostics);
        if (diagnostics.Count > 0)
            return;

        source.AppendLine($"public sealed class {variant.Name} :");
        source.AppendLine($"    {root.Name},");
        source.AppendLine($"    Transform<{variant.Name}, {inputType}>");
        source.AppendLine("{");
        source.AppendLine($"    public sealed record Repr({Parameters(variant.Representation.Fields, context.Rules)})");
        source.AppendLine($"        : {root.Name}.Repr;");

        if (!variant.Construction.Input.IsScalar)
        {
            source.AppendLine();
            source.AppendLine($"    public readonly record struct Input({Parameters(variant.Construction.Input.Fields, context.Rules)});");
        }

        foreach (var field in variant.State.Fields)
        {
            source.AppendLine();
            source.AppendLine($"    public {RenderType(field.Type, context.Rules)} {field.Name} {{ get; }}");
        }

        source.AppendLine();
        source.AppendLine($"    private {variant.Name}({Parameters(variant.State.Fields, context.Rules, camelNames: true)}) =>");
        if (variant.State.Fields.Count == 1)
        {
            var field = variant.State.Fields[0];
            source.AppendLine($"        {field.Name} = {Camel(field.Name)};");
        }
        else
        {
            source.AppendLine($"        ({string.Join(", ", variant.State.Fields.Select(x => x.Name))}) =");
            source.AppendLine($"        ({string.Join(", ", variant.State.Fields.Select(x => Camel(x.Name)))});");
        }

        source.AppendLine();
        source.AppendLine($"    public static VSlices.Arrows.Req<{inputType}, {variant.Name}>.Full Invariants =>");
        if (pipeline.Count == 0)
        {
            source.AppendLine($"        VSlices.Arrows.Req<{inputType}, {variant.Name}>.Transform(({inputType} input) => Instance(input));");
        }
        else
        {
            for (var index = 0; index < pipeline.Count; index++)
                source.AppendLine((index == 0 ? "        " : "        >> ") + pipeline[index]);
            source.AppendLine("        * Instance;");
        }

        source.AppendLine();
        source.AppendLine($"    private static {variant.Name} Instance({inputType} input) =>");
        source.AppendLine($"        new({string.Join(", ", variant.State.Fields.Select(field => stateExpressions[field.Name]))});");

        source.AppendLine();
        source.AppendLine("    public override Repr To() =>");
        source.AppendLine($"        new({string.Join(", ", variant.Representation.Fields.Select(field => field.Name))});");
        source.AppendLine("}");
    }

    private static void LowerNormalize(
        NormalizeStep normalize,
        CSharpLoweringRuleSet rules,
        IDictionary<string, string> references,
        ICollection<VsirDiagnostic> diagnostics,
        string variantName)
    {
        if (!references.TryGetValue(normalize.Target, out var current))
        {
            diagnostics.Add(new("CSL103", $"Normalize in variant '{variantName}' references unknown input '{normalize.Target}'."));
            return;
        }

        var node = $"intrinsic.{normalize.Intrinsic}";
        if (!rules.TryRenderDeterministicExpression(
                node,
                new Dictionary<string, string> { ["value"] = current },
                out var rendered))
        {
            diagnostics.Add(new("CSL031", $"No deterministic C# normalization rule is available for '{node}'."));
            return;
        }

        references[normalize.Target] = rendered;
    }

    private static void LowerEnsure(
        DomainTypeVariant variant,
        string inputType,
        EnsureStep ensure,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> references,
        ICollection<string> pipeline,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!TryRenderCondition(ensure.Condition, rules, references, out var expression, out var error))
        {
            diagnostics.Add(new("CSL011", $"Variant '{variant.Name}': {error}"));
            return;
        }

        pipeline.Add(
            $"VSlices.Arrows.Req<{inputType}, {variant.Name}>.Ensure(({inputType} input) => {expression}, Fail: {Quote(ensure.FailureMessage)})");
    }

    private static bool TryRenderCondition(
        Condition condition,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> references,
        out string expression,
        out string? error)
    {
        string node;
        SemanticExpression value;
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        switch (condition)
        {
            case NonEmptyCondition nonEmpty:
                node = "intrinsic.non-empty";
                value = nonEmpty.Value;
                break;
            case NotWhitespaceCondition notWhitespace:
                node = "intrinsic.not-whitespace";
                value = notWhitespace.Value;
                break;
            case LengthAtMostCondition lengthAtMost:
                node = "intrinsic.length-at-most";
                value = lengthAtMost.Value;
                bindings["max"] = lengthAtMost.Max.ToString();
                break;
            case LengthBetweenCondition lengthBetween:
                node = "intrinsic.length-between";
                value = lengthBetween.Value;
                bindings["min"] = lengthBetween.Min.ToString();
                bindings["max"] = lengthBetween.Max.ToString();
                break;
            default:
                expression = string.Empty;
                error = "Unsupported condition reached sum C# lowering.";
                return false;
        }

        if (!TryRenderSemanticExpression(value, rules, references, out var renderedValue, out error))
        {
            expression = string.Empty;
            return false;
        }

        bindings["value"] = renderedValue!;
        if (!rules.TryRenderDeterministicExpression(node, bindings, out expression))
        {
            error = $"No deterministic C# lowering rule is available for '{node}'.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryRenderSemanticExpression(
        SemanticExpression semanticExpression,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> references,
        out string? expression,
        out string? error)
    {
        switch (semanticExpression)
        {
            case SemanticReferenceExpression reference:
                if (!references.TryGetValue(reference.Value, out var renderedReference))
                {
                    expression = null;
                    error = $"Semantic expression references unknown variant input '{reference.Value}'.";
                    return false;
                }

                expression = renderedReference;
                error = null;
                return true;

            case SemanticIntrinsicExpression intrinsic:
            {
                var values = new List<string>(intrinsic.Values.Count);
                foreach (var operand in intrinsic.Values)
                {
                    if (!TryRenderSemanticExpression(operand, rules, references, out var rendered, out error))
                    {
                        expression = null;
                        return false;
                    }
                    values.Add(rendered!);
                }

                var node = $"intrinsic.{intrinsic.Intrinsic}";
                if (!rules.TryRenderDeterministicExpression(
                        node,
                        new Dictionary<string, string> { ["values"] = string.Join(", ", values) },
                        out var renderedExpression))
                {
                    expression = null;
                    error = $"No deterministic C# lowering rule is available for nested semantic expression '{node}'.";
                    return false;
                }

                expression = renderedExpression;
                error = null;
                return true;
            }

            default:
                expression = null;
                error = $"Unsupported semantic expression '{semanticExpression.GetType().Name}'.";
                return false;
        }
    }

    private static Dictionary<string, string> ResolveStateExpressions(
        DomainTypeVariant variant,
        IReadOnlyDictionary<string, string> references,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var refine in variant.Construction.Steps.OfType<RefineStep>())
        {
            if (!refine.As.StartsWith("state.", StringComparison.Ordinal))
                continue;
            var stateName = refine.As["state.".Length..];
            if (references.TryGetValue(refine.Value, out var rendered))
                result[stateName] = rendered;
            else if (variant.Construction.Input.IsScalar && refine.Value == "input")
                result[stateName] = "input";
        }

        foreach (var state in variant.State.Fields)
        {
            if (result.ContainsKey(state.Name))
                continue;
            var direct = "input." + state.Name;
            if (references.TryGetValue(direct, out var rendered))
                result[state.Name] = rendered;
            else
                diagnostics.Add(new("CSL104", $"Variant '{variant.Name}' cannot establish state.{state.Name} deterministically."));
        }

        return result;
    }

    private static Dictionary<string, string> InitialInputReferences(ConstructionInput input)
    {
        if (input.IsScalar)
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["input"] = "input" };

        return input.Fields.ToDictionary(
            field => "input." + field.Name,
            field => "input." + field.Name,
            StringComparer.Ordinal);
    }

    private static void ValidateVariantTypes(
        DomainTypeVariant variant,
        CSharpLoweringRuleSet rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        foreach (var type in variant.State.Fields
                     .Concat(variant.Representation.Fields)
                     .Concat(variant.Construction.Input.Fields)
                     .Select(x => x.Type))
            ValidateType(type, rules, diagnostics);
        if (variant.Construction.Input.ScalarType is not null)
            ValidateType(variant.Construction.Input.ScalarType, rules, diagnostics);
    }

    private static void ValidateType(
        VsirType type,
        CSharpLoweringRuleSet rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (type is not UnaryVsirType unary)
            return;
        ValidateType(unary.Value, rules, diagnostics);
        if (!rules.TryRenderDeterministicType(
                $"type.{unary.Constructor}",
                new Dictionary<string, string> { ["value"] = "T" },
                out _))
        {
            diagnostics.Add(new(
                "CSL050",
                $"Target Ruleset does not provide a deterministic type realization for semantic constructor '{unary.Constructor}'."));
        }
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

    private static string Parameters(
        IReadOnlyList<Field> fields,
        CSharpLoweringRuleSet rules,
        bool camelNames = false) =>
        string.Join(", ", fields.Select(field =>
            $"{RenderType(field.Type, rules)} {(camelNames ? Camel(field.Name) : field.Name)}"));

    private static string Camel(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
