using System.Text;
using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

/// <summary>
/// C# realization for canonical sum-shaped Domain Types. Shared state and
/// representation live on the root while variants realize their local state,
/// representation and transform boundary.
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
        ValidateRootTypes(document, context.Rules, diagnostics);
        foreach (var variant in document.Variants)
            ValidateVariantTypes(variant, context.Rules, diagnostics);
        ValidateIdentity(document, context.Rules, diagnostics);
        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        var source = new StringBuilder();
        source.AppendLine($"namespace {context.Namespace};");
        source.AppendLine();
        RenderRoot(source, document, context.Rules, diagnostics);

        foreach (var variant in document.Variants)
        {
            source.AppendLine();
            LowerVariant(source, document, variant, context, diagnostics);
        }

        return diagnostics.Count == 0
            ? new(source.ToString(), [])
            : new(null, diagnostics);
    }

    private static void RenderRoot(
        StringBuilder source,
        DomainTypeVsir document,
        CSharpLoweringRuleSet rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var constructor = document.State.Fields.Count == 0
            ? string.Empty
            : $"({Parameters(document.State.Fields, rules, camelNames: true)})";
        source.AppendLine($"public abstract class {document.Name}{constructor} :");

        var contracts = RootContracts(document, rules).ToArray();
        for (var index = 0; index < contracts.Length; index++)
            source.AppendLine($"    {contracts[index]}{(index == contracts.Length - 1 ? string.Empty : ",")}");

        source.AppendLine("{");
        if (document.Representation.Fields.Count == 0)
        {
            source.AppendLine("    public abstract record Repr;");
        }
        else
        {
            source.AppendLine($"    public abstract record Repr({Parameters(document.Representation.Fields, rules)});");
        }

        foreach (var field in document.State.Fields)
        {
            source.AppendLine();
            source.AppendLine($"    public {RenderType(field.Type, rules)} {field.Name} {{ get; }} = {Camel(field.Name)};");
        }

        if (document.Identity is not null)
        {
            var identitySource = RenderRootConstructorReference(document.Identity.From);
            if (!rules.TryRenderDeterministicExpression(
                    "identity.construct",
                    new Dictionary<string, string>
                    {
                        ["type"] = RenderType(document.Identity.Type, rules),
                        ["value"] = identitySource
                    },
                    out var identityExpression))
            {
                diagnostics.Add(new("CSL110", "Target Ruleset does not provide deterministic aggregate identity construction."));
            }
            else
            {
                source.AppendLine();
                source.AppendLine($"    public {RenderType(document.Identity.Type, rules)} Id {{ get; }} = {identityExpression};");
            }
        }

        source.AppendLine();
        source.AppendLine("    public abstract Repr To();");
        source.AppendLine("}");
    }

    private static IEnumerable<string> RootContracts(DomainTypeVsir document, CSharpLoweringRuleSet rules)
    {
        yield return $"DomainType<{document.Name}, {document.Name}.Repr>";

        if (document.Classification == "aggregate-root" && document.Identity is not null)
            yield return $"AggregateRoot<{document.Name}, {RenderType(document.Identity.Type, rules)}>";
        else if (document.Classification == "entity" && document.Identity is not null)
            yield return $"Entity<{document.Name}, {RenderType(document.Identity.Type, rules)}>";
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

        var effectiveState = root.State.Fields.Concat(variant.State.Fields).ToArray();
        var stateExpressions = ResolveStateExpressions(variant, effectiveState, references, diagnostics);
        var effectiveRepresentation = root.Representation.Fields.Concat(variant.Representation.Fields).ToArray();
        var representationExpressions = new List<string>(effectiveRepresentation.Length);
        foreach (var field in root.Representation.Fields)
        {
            if (!TryRepresentationExpression(root.RepresentationMapping, field, context.Rules, out var expression, out var error))
                diagnostics.Add(new("CSL060", $"Variant '{variant.Name}': {error}"));
            else
                representationExpressions.Add(expression!);
        }
        foreach (var field in variant.Representation.Fields)
        {
            if (!TryRepresentationExpression(variant.RepresentationMapping, field, context.Rules, out var expression, out var error))
                diagnostics.Add(new("CSL060", $"Variant '{variant.Name}': {error}"));
            else
                representationExpressions.Add(expression!);
        }

        if (diagnostics.Count > 0)
            return;

        source.AppendLine($"public sealed class {variant.Name} :");
        source.AppendLine($"    {root.Name},");
        source.AppendLine($"    Transform<{variant.Name}, {inputType}>");
        source.AppendLine("{");

        if (effectiveRepresentation.Length == 0)
        {
            source.AppendLine($"    public sealed record Repr : {root.Name}.Repr;");
        }
        else
        {
            source.AppendLine($"    public sealed record Repr({Parameters(effectiveRepresentation, context.Rules)})");
            var baseArguments = string.Join(", ", root.Representation.Fields.Select(field => field.Name));
            source.AppendLine(root.Representation.Fields.Count == 0
                ? $"        : {root.Name}.Repr;"
                : $"        : {root.Name}.Repr({baseArguments});");
        }

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
        source.AppendLine($"    private {variant.Name}({Parameters(effectiveState, context.Rules, camelNames: true)})");
        if (root.State.Fields.Count > 0)
            source.AppendLine($"        : base({string.Join(", ", root.State.Fields.Select(field => Camel(field.Name)))})");
        source.AppendLine("    {");
        foreach (var field in variant.State.Fields)
            source.AppendLine($"        {field.Name} = {Camel(field.Name)};");
        source.AppendLine("    }");

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
        source.AppendLine($"        new({string.Join(", ", effectiveState.Select(field => stateExpressions[field.Name]))});");

        source.AppendLine();
        source.AppendLine("    public override Repr To() =>");
        source.AppendLine($"        new({string.Join(", ", representationExpressions)});");
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
        IReadOnlyList<Field> effectiveState,
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

        foreach (var state in effectiveState)
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

    private static bool TryRepresentationExpression(
        RepresentationMapping? mapping,
        Field field,
        CSharpLoweringRuleSet rules,
        out string? expression,
        out string? error)
    {
        if (mapping?.Fields.TryGetValue(field.Name, out var projection) == true)
            return TryRenderProjection(projection, rules, new Dictionary<string, string>(StringComparer.Ordinal), out expression, out error);

        expression = RenderSemanticReference(field.From ?? "state." + field.Name, new Dictionary<string, string>(StringComparer.Ordinal));
        error = null;
        return true;
    }

    private static bool TryRenderProjection(
        RepresentationProjection projection,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> bindings,
        out string? expression,
        out string? error)
    {
        expression = null;
        error = null;

        switch (projection)
        {
            case ReferenceProjection reference:
                expression = RenderSemanticReference(reference.Value, bindings);
                return true;

            case StringifyProjection stringify:
                return TryRenderRule(
                    rules,
                    "projection.stringify",
                    new Dictionary<string, string> { ["value"] = RenderSemanticReference(stringify.Value, bindings) },
                    out expression,
                    out error);

            case RepresentProjection represent:
                if (!TryRenderProjection(represent.Value, rules, bindings, out var represented, out error))
                    return false;
                return TryRenderRule(
                    rules,
                    "projection.represent",
                    new Dictionary<string, string> { ["value"] = represented! },
                    out expression,
                    out error);

            case SelectProjection select:
                if (!TryRenderProjection(select.Source, rules, bindings, out var selectedSource, out error))
                    return false;
                return TryRenderRule(
                    rules,
                    "projection.select",
                    new Dictionary<string, string> { ["source"] = selectedSource!, ["field"] = select.Field },
                    out expression,
                    out error);

            case MapProjection map:
                if (!TryRenderProjection(map.Source, rules, bindings, out var mappedSource, out error))
                    return false;
                var nested = new Dictionary<string, string>(bindings, StringComparer.Ordinal) { [map.Bind] = map.Bind };
                if (!TryRenderProjection(map.Value, rules, nested, out var mappedValue, out error))
                    return false;
                return TryRenderRule(
                    rules,
                    "projection.map",
                    new Dictionary<string, string>
                    {
                        ["source"] = mappedSource!,
                        ["bind"] = map.Bind,
                        ["value"] = mappedValue!
                    },
                    out expression,
                    out error);

            case IntrinsicProjection intrinsic:
            {
                var values = new List<string>();
                foreach (var value in intrinsic.Values)
                {
                    if (!TryRenderProjection(value, rules, bindings, out var rendered, out error))
                        return false;
                    values.Add(rendered!);
                }
                return TryRenderRule(
                    rules,
                    $"intrinsic.{intrinsic.Intrinsic}",
                    new Dictionary<string, string> { ["values"] = string.Join(", ", values) },
                    out expression,
                    out error);
            }

            default:
                error = $"Unsupported representation projection '{projection.GetType().Name}'.";
                return false;
        }
    }

    private static bool TryRenderRule(
        CSharpLoweringRuleSet rules,
        string node,
        IReadOnlyDictionary<string, string> bindings,
        out string? expression,
        out string? error)
    {
        if (!rules.TryRenderDeterministicExpression(node, bindings, out var rendered))
        {
            expression = null;
            error = $"No deterministic C# lowering rule is available for '{node}'.";
            return false;
        }
        expression = rendered;
        error = null;
        return true;
    }

    private static string RenderSemanticReference(
        string reference,
        IReadOnlyDictionary<string, string> bindings)
    {
        if (reference == "identity")
            return "Id";
        if (bindings.TryGetValue(reference, out var bound))
            return bound;
        if (reference.StartsWith("state.", StringComparison.Ordinal))
            return reference["state.".Length..];
        return reference;
    }

    private static string RenderRootConstructorReference(string reference)
    {
        if (!reference.StartsWith("state.", StringComparison.Ordinal))
            return reference;
        var tail = reference["state.".Length..];
        var separator = tail.IndexOf('.', StringComparison.Ordinal);
        var root = separator < 0 ? tail : tail[..separator];
        var remainder = separator < 0 ? string.Empty : tail[separator..];
        return Camel(root) + remainder;
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

    private static void ValidateIdentity(
        DomainTypeVsir document,
        CSharpLoweringRuleSet rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (document.Classification is not ("aggregate-root" or "entity"))
            return;
        if (document.Identity is null)
        {
            diagnostics.Add(new("CSL109", $"Classification '{document.Classification}' requires identity semantics."));
            return;
        }
        if (!rules.TryRenderDeterministicExpression(
                "identity.construct",
                new Dictionary<string, string>
                {
                    ["type"] = RenderType(document.Identity.Type, rules),
                    ["value"] = "value"
                },
                out _))
            diagnostics.Add(new("CSL110", "Target Ruleset does not provide deterministic aggregate identity construction."));
    }

    private static void ValidateRootTypes(
        DomainTypeVsir document,
        CSharpLoweringRuleSet rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        foreach (var type in document.State.Fields
                     .Concat(document.Representation.Fields)
                     .Select(x => x.Type))
            ValidateType(type, rules, diagnostics);
        if (document.Identity is not null)
            ValidateType(document.Identity.Type, rules, diagnostics);
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
        CSharpLiteral.String(value);
}
