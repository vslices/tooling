using System.Text;
using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

/// <summary>
/// Lowers the canonical VSIR 0.1 semantic surface without flattening its expression tree.
/// Pre-normalized experimental grammars are migration inputs, not compatibility surfaces.
/// </summary>
public static class CSharpLanguageLowerer
{
    public static CSharpLoweringResult Lower(
        DomainTypeVsir document,
        CSharpLoweringContext context) =>
        LowerCanonical(document, context);

    private static CSharpLoweringResult LowerCanonical(
        DomainTypeVsir document,
        CSharpLoweringContext context)
    {
        var diagnostics = DomainTypeValidator.Validate(document, context.ValidationContext).ToList();
        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        ValidateTypes(document, context.Rules, diagnostics);
        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        var inputType = document.Construction.Input.IsScalar
            ? RenderType(document.Construction.Input.ScalarType!, context.Rules)
            : document.Name + ".Input";
        var references = InitialInputReferences(document.Construction.Input);
        var pipeline = new List<string>();

        foreach (var step in document.Construction.Steps)
        {
            switch (step)
            {
                case NormalizeStep normalize:
                    LowerNormalize(normalize, context.Rules, references, diagnostics);
                    break;
                case EnsureStep ensure:
                    LowerEnsure(document.Name, inputType, ensure, context.Rules, references, pipeline, diagnostics);
                    break;
                case ResolveStep resolve:
                    LowerResolve(document.Name, inputType, resolve, context.Rules, references, pipeline, diagnostics);
                    break;
                case ApplyStep apply:
                    LowerApply(document.Name, inputType, apply, context.Rules, references, pipeline, diagnostics);
                    break;
                case IntrinsicRefineStep refine:
                    LowerIntrinsicRefine(document.Name, inputType, refine, context.Rules, references, pipeline, diagnostics);
                    break;
            }
        }

        var stateBindings = StateBindings(document.State);

        if (document.Equality is not null)
            ValidateEqualityRules(document.Equality, context.Rules, stateBindings, diagnostics);

        var representationExpressions = new List<string>();
        foreach (var field in document.Representation.Fields)
        {
            if (!TryRepresentationExpression(document, field, context.Rules, stateBindings, out var expression, out var error))
            {
                diagnostics.Add(new("CSL060", error!));
                continue;
            }
            representationExpressions.Add(expression!);
        }

        if (diagnostics.Count > 0)
            return new(null, diagnostics);

        var directState = document.State.Fields.Where(field => field.From is null).ToArray();
        var stateExpressions = ResolveStateExpressions(document, directState, references);
        var source = new StringBuilder();
        source.AppendLine($"namespace {context.Namespace};");
        source.AppendLine();
        source.AppendLine($"public sealed class {document.Name} :");

        var contracts = Contracts(document, inputType).ToArray();
        for (var i = 0; i < contracts.Length; i++)
            source.AppendLine($"    {contracts[i]}{(i == contracts.Length - 1 ? string.Empty : ",")}");

        source.AppendLine("{");
        source.AppendLine($"    public readonly record struct Repr({Parameters(document.Representation.Fields, context.Rules)});");

        if (!document.Construction.Input.IsScalar)
        {
            source.AppendLine();
            source.AppendLine($"    public readonly record struct Input({Parameters(document.Construction.Input.Fields, context.Rules)});");
        }

        source.AppendLine();
        foreach (var field in directState)
            source.AppendLine($"    private readonly {RenderType(field.Type, context.Rules)} _{Camel(field.Name)};");

        foreach (var field in document.State.Fields.Where(field => field.From is not null))
        {
            source.AppendLine();
            source.AppendLine($"    public {RenderType(field.Type, context.Rules)} {field.Name} =>");
            source.AppendLine($"        {RenderSemanticReference(field.From!, stateBindings)};");
        }

        source.AppendLine();
        source.AppendLine($"    private {document.Name}({Parameters(directState, context.Rules, camelNames: true)}) =>");
        source.AppendLine($"        {ConstructorAssignment(directState)};");
        source.AppendLine();
        source.AppendLine($"    public static VSlices.Arrows.Req<{inputType}, {document.Name}>.Full Invariants =>");

        if (pipeline.Count == 0)
        {
            source.AppendLine($"        VSlices.Arrows.Req<{inputType}, {document.Name}>.Transform(({inputType} input) => Instance(input));");
        }
        else
        {
            for (var i = 0; i < pipeline.Count; i++)
                source.AppendLine((i == 0 ? "        " : "        >> ") + pipeline[i]);
            source.AppendLine($"        >> VSlices.Arrows.Req<{inputType}, {document.Name}>.Transform(({inputType} input) => Instance(input));");
        }

        source.AppendLine();
        source.AppendLine($"    private static {document.Name} Instance({inputType} input) =>");
        source.AppendLine($"        new({string.Join(", ", directState.Select(field => stateExpressions["state." + field.Name]))});");

        if (document.Equality is not null)
        {
            source.AppendLine();
            RenderEquality(source, document.Name, document.Equality, context.Rules, stateBindings);
        }

        if (document.RefinedFrom is not null)
        {
            var baseType = new NamedVsirType(document.RefinedFrom);
            var baseField = directState.Single(field => field.Type == baseType);
            source.AppendLine();
            source.AppendLine($"    public {document.RefinedFrom} ToBase() =>");
            source.AppendLine($"        _{Camel(baseField.Name)};");
        }

        source.AppendLine();
        source.AppendLine("    public Repr To() =>");
        source.AppendLine($"        new({string.Join(", ", representationExpressions)});");
        source.AppendLine("}");

        return new(source.ToString(), []);
    }

    private static void LowerNormalize(
        NormalizeStep normalize,
        CSharpLoweringRuleSet rules,
        Dictionary<string, string> references,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var node = $"intrinsic.{normalize.Intrinsic}";
        if (!rules.TryRenderDeterministicExpression(
                node,
                new Dictionary<string, string> { ["value"] = ResolveReference(normalize.Target, references) },
                out var expression))
        {
            diagnostics.Add(new("CSL031", $"No deterministic C# normalization rule is available for '{node}'."));
            return;
        }
        references[normalize.Target] = expression;
    }

    private static void LowerEnsure(
        string domain,
        string inputType,
        EnsureStep ensure,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> references,
        ICollection<string> pipeline,
        ICollection<VsirDiagnostic> diagnostics)
    {
        if (!TryDescribeCondition(ensure.Condition, rules, references, out var node, out var bindings, out var nestedError))
        {
            diagnostics.Add(new("CSL011", nestedError!));
            return;
        }

        if (!rules.TryRenderDeterministicExpression(node!, bindings!, out var expression))
        {
            diagnostics.Add(new("CSL010", $"No deterministic C# lowering rule is available for '{node}'."));
            return;
        }

        pipeline.Add(
            $"VSlices.Arrows.Req<{inputType}, {domain}>.Ensure(({inputType} input) => {expression}, Fail: {RenderFailure(ensure, inputType, references, rules)})");
    }

    private static void LowerResolve(
        string domain,
        string inputType,
        ResolveStep resolve,
        CSharpLoweringRuleSet rules,
        Dictionary<string, string> references,
        ICollection<string> pipeline,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var id = ResolveReference(resolve.Id, references);
        var common = new Dictionary<string, string>
        {
            ["source"] = resolve.Source,
            ["id"] = id
        };

        if (!rules.TryRenderDeterministicExpression("construction.resolve.condition", common, out var condition))
        {
            diagnostics.Add(new("CSL070", "No deterministic C# lowering rule is available for 'construction.resolve.condition'."));
            return;
        }
        if (!rules.TryRenderDeterministicExpression("construction.resolve.value", common, out var value))
        {
            diagnostics.Add(new("CSL071", "No deterministic C# lowering rule is available for 'construction.resolve.value'."));
            return;
        }

        pipeline.Add(
            $"VSlices.Arrows.Req<{inputType}, {domain}>.Ensure(({inputType} input) => {condition}, Fail: {Quote(resolve.FailureMessage)})");
        references[resolve.As] = value;
    }

    private static void LowerApply(
        string domain,
        string inputType,
        ApplyStep apply,
        CSharpLoweringRuleSet rules,
        Dictionary<string, string> references,
        ICollection<string> pipeline,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var over = apply.Over;
        switch (apply.Input)
        {
            case DirectApplyInput direct:
            {
                var arguments = string.Join(", ", direct.Fields.Values.Select(value => ResolveReference(value, references)));
                var inputBindings = new Dictionary<string, string>
                {
                    ["over"] = over,
                    ["arguments"] = arguments
                };
                if (!rules.TryRenderDeterministicExpression("construction.apply.input", inputBindings, out var nestedInput) ||
                    !rules.TryRenderDeterministicExpression(
                        "construction.apply.value",
                        new Dictionary<string, string> { ["over"] = over, ["input"] = nestedInput },
                        out var value))
                {
                    diagnostics.Add(new("CSL072", "No deterministic C# lowering rule is available for direct construction apply."));
                    return;
                }

                pipeline.Add(
                    $"VSlices.Arrows.Req<{inputType}, {domain}>.Apply({over}.Invariants, To: ({inputType} input) => {nestedInput})");
                references[apply.As] = value;
                break;
            }
            case MappedApplyInput mapped:
            {
                var source = ResolveReference(mapped.Source, references);
                const string bind = "item";
                var arguments = string.Join(", ", mapped.Map.Values.Select(value => value == "item" ? bind : ResolveReference(value, references)));
                var sequenceBindings = new Dictionary<string, string>
                {
                    ["source"] = source,
                    ["bind"] = bind,
                    ["over"] = over,
                    ["arguments"] = arguments
                };
                if (!rules.TryRenderDeterministicExpression("construction.apply-sequence.input", sequenceBindings, out var nestedInputs) ||
                    !rules.TryRenderDeterministicExpression(
                        "construction.apply-sequence.value",
                        new Dictionary<string, string> { ["over"] = over, ["input"] = nestedInputs },
                        out var value))
                {
                    diagnostics.Add(new("CSL073", "No deterministic C# lowering rule is available for mapped construction apply."));
                    return;
                }

                pipeline.Add(
                    $"VSlices.Arrows.Req<{inputType}, {domain}>.ApplySeq({over}.Invariants, To: ({inputType} input) => {nestedInputs})");
                references[apply.As] = value;
                break;
            }
        }
    }

    private static void LowerIntrinsicRefine(
        string domain,
        string inputType,
        IntrinsicRefineStep refine,
        CSharpLoweringRuleSet rules,
        Dictionary<string, string> references,
        ICollection<string> pipeline,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var value = ResolveReference(refine.Value, references);
        var bindings = new Dictionary<string, string> { ["value"] = value };
        var conditionNode = $"refine.{refine.Intrinsic}.condition";
        if (!rules.TryRenderDeterministicExpression(conditionNode, bindings, out var condition))
        {
            diagnostics.Add(new("CSL080", $"No deterministic C# refinement condition rule is available for '{conditionNode}'."));
            return;
        }

        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var output in refine.As)
        {
            var node = $"refine.{refine.Intrinsic}.output.{output.Key}";
            if (!rules.TryRenderDeterministicExpression(node, bindings, out var expression))
            {
                diagnostics.Add(new("CSL081", $"No deterministic C# refinement output rule is available for '{node}'."));
                continue;
            }
            outputs[output.Value] = expression;
        }

        if (outputs.Count != refine.As.Count)
            return;

        pipeline.Add(
            $"VSlices.Arrows.Req<{inputType}, {domain}>.Ensure(({inputType} input) => {condition}, Fail: {Quote(refine.FailureMessage)})");
        foreach (var output in outputs)
            references[output.Key] = output.Value;
    }

    private static IReadOnlyDictionary<string, string> ResolveStateExpressions(
        DomainTypeVsir document,
        IReadOnlyList<Field> directState,
        IReadOnlyDictionary<string, string> references)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in directState)
        {
            var stateReference = "state." + field.Name;
            var refine = document.Construction.Steps
                .OfType<RefineStep>()
                .SingleOrDefault(step => step.As == stateReference);
            result[stateReference] = refine is not null
                ? ResolveReference(refine.Value, references)
                : ResolveReference("input." + field.Name, references);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> StateBindings(ProductShape state) =>
        state.Fields.ToDictionary(
            field => "state." + field.Name,
            field => field.From is null ? "_" + Camel(field.Name) : field.Name,
            StringComparer.Ordinal);

    private static bool TryRepresentationExpression(
        DomainTypeVsir document,
        Field field,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> stateBindings,
        out string? expression,
        out string? error)
    {
        if (document.RepresentationMapping?.Fields.TryGetValue(field.Name, out var projection) == true)
            return TryRenderProjection(projection, rules, stateBindings, out expression, out error);

        error = null;
        expression = field.From is not null
            ? RenderSemanticReference(field.From, stateBindings)
            : RenderSemanticReference("state." + field.Name, stateBindings);
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
            {
                var value = RenderSemanticReference(stringify.Value, bindings);
                return TryRenderRule(rules, "projection.stringify", new Dictionary<string, string> { ["value"] = value }, out expression, out error);
            }

            case RepresentProjection represent:
            {
                if (!TryRenderProjection(represent.Value, rules, bindings, out var value, out error))
                    return false;
                return TryRenderRule(rules, "projection.represent", new Dictionary<string, string> { ["value"] = value! }, out expression, out error);
            }

            case SelectProjection select:
            {
                if (!TryRenderProjection(select.Source, rules, bindings, out var source, out error))
                    return false;
                return TryRenderRule(
                    rules,
                    "projection.select",
                    new Dictionary<string, string> { ["source"] = source!, ["field"] = select.Field },
                    out expression,
                    out error);
            }

            case MapProjection map:
            {
                if (!TryRenderProjection(map.Source, rules, bindings, out var source, out error))
                    return false;
                var nested = new Dictionary<string, string>(bindings, StringComparer.Ordinal) { [map.Bind] = map.Bind };
                if (!TryRenderProjection(map.Value, rules, nested, out var value, out error))
                    return false;
                return TryRenderRule(
                    rules,
                    "projection.map",
                    new Dictionary<string, string>
                    {
                        ["source"] = source!,
                        ["bind"] = map.Bind,
                        ["value"] = value!
                    },
                    out expression,
                    out error);
            }

            case IntrinsicProjection intrinsic:
            {
                var values = new List<string>();
                foreach (var item in intrinsic.Values)
                {
                    if (!TryRenderProjection(item, rules, bindings, out var value, out error))
                        return false;
                    values.Add(value!);
                }
                return TryRenderRule(
                    rules,
                    $"intrinsic.{intrinsic.Intrinsic}",
                    new Dictionary<string, string> { ["values"] = string.Join(", ", values) },
                    out expression,
                    out error);
            }

            default:
                error = $"Unsupported representation expression '{projection.GetType().Name}'.";
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
        if (rules.TryRenderDeterministicExpression(node, bindings, out var rendered))
        {
            expression = rendered;
            error = null;
            return true;
        }

        expression = null;
        error = $"No deterministic C# representation rule is available for '{node}'.";
        return false;
    }

    private static string RenderSemanticReference(
        string reference,
        IReadOnlyDictionary<string, string> bindings)
    {
        if (bindings.TryGetValue(reference, out var bound))
            return bound;

        // Bindings describe semantic roots, not only exact leaf strings. This lets
        // a derived reference such as state.Value.Length realize through the
        // concrete accessor selected for state.Value without inventing a field
        // named _length for the derived state coordinate.
        var prefix = bindings.Keys
            .Where(key => reference.StartsWith(key + ".", StringComparison.Ordinal))
            .OrderByDescending(key => key.Length)
            .FirstOrDefault();
        if (prefix is not null)
            return bindings[prefix] + reference[prefix.Length..];

        if (!reference.StartsWith("state.", StringComparison.Ordinal))
            return reference;

        var path = reference["state.".Length..].Split('.');
        return "_" + Camel(path[0]) + (path.Length == 1 ? string.Empty : "." + string.Join(".", path.Skip(1)));
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

    private static string ResolveReference(string reference, IReadOnlyDictionary<string, string> references) =>
        references.TryGetValue(reference, out var expression) ? expression : reference;

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
                expression = ResolveReference(reference.Value, references);
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

    private static bool TryDescribeCondition(
        Condition condition,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> references,
        out string? node,
        out IReadOnlyDictionary<string, string>? bindings,
        out string? error)
    {
        SemanticExpression value;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        switch (condition)
        {
            case NonEmptyCondition x:
                node = "intrinsic.non-empty";
                value = x.Value;
                break;
            case NotWhitespaceCondition x:
                node = "intrinsic.not-whitespace";
                value = x.Value;
                break;
            case LengthAtMostCondition x:
                node = "intrinsic.length-at-most";
                value = x.Value;
                result["max"] = x.Max.ToString();
                break;
            case LengthBetweenCondition x:
                node = "intrinsic.length-between";
                value = x.Value;
                result["min"] = x.Min.ToString();
                result["max"] = x.Max.ToString();
                break;
            default:
                node = null;
                bindings = null;
                error = "Unsupported condition reached C# lowering.";
                return false;
        }

        if (!TryRenderSemanticExpression(value, rules, references, out var renderedValue, out error))
        {
            bindings = null;
            return false;
        }

        result["value"] = renderedValue!;
        bindings = result;
        error = null;
        return true;
    }

    private static string RenderFailure(
        EnsureStep ensure,
        string inputType,
        IReadOnlyDictionary<string, string> references,
        CSharpLoweringRuleSet rules)
    {
        var literal = Quote(ensure.FailureMessage);
        if (!ensure.FailureMessage.Contains("{length}", StringComparison.Ordinal))
            return literal;
        if (ensure.Condition is not LengthAtMostCondition condition)
            return literal;
        if (!TryRenderSemanticExpression(condition.Value, rules, references, out var value, out _))
            return literal;
        return $"({inputType} input) => {literal}.Replace(\"{{length}}\", {value}.Length.ToString())";
    }

    private static IEnumerable<string> Contracts(DomainTypeVsir document, string inputType)
    {
        var hasIdentifierCapability =
            document.Classification == "identifier" ||
            document.Traits.Contains("identifier", StringComparer.Ordinal);
        var isRefined = document.Traits.Contains("refined", StringComparer.Ordinal);

        yield return $"DomainType<{document.Name}, {document.Name}.Repr>";
        if (hasIdentifierCapability)
            yield return $"Identifier<{document.Name}>";
        if (isRefined)
            yield return $"Refined<{document.Name}, {document.RefinedFrom}>";
        yield return $"Transform<{document.Name}, {inputType}>";
    }

    private static void ValidateTypes(
        DomainTypeVsir document,
        CSharpLoweringRuleSet rules,
        ICollection<VsirDiagnostic> diagnostics)
    {
        foreach (var type in document.State.Fields
                     .Concat(document.Representation.Fields)
                     .Concat(document.Construction.Input.Fields)
                     .Select(field => field.Type))
            ValidateType(type, rules, diagnostics);
        if (document.Construction.Input.ScalarType is not null)
            ValidateType(document.Construction.Input.ScalarType, rules, diagnostics);
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

    private static string Parameters(
        IReadOnlyList<Field> fields,
        CSharpLoweringRuleSet rules,
        bool camelNames = false) =>
        string.Join(", ", fields.Select(field =>
            $"{RenderType(field.Type, rules)} {(camelNames ? Camel(field.Name) : field.Name)}"));

    private static string ConstructorAssignment(IReadOnlyList<Field> fields)
    {
        if (fields.Count == 1)
            return $"_{Camel(fields[0].Name)} = {Camel(fields[0].Name)}";
        var left = string.Join(", ", fields.Select(field => "_" + Camel(field.Name)));
        var right = string.Join(", ", fields.Select(field => Camel(field.Name)));
        return $"({left}) = ({right})";
    }

    private static void ValidateEqualityRules(
        EqualitySemantics equality,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> stateBindings,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var member = RenderSemanticReference(equality.By, stateBindings);
        var otherMember = "other." + member;
        if (!rules.TryRenderDeterministicExpression(
                EqualityNode(equality, "equals"),
                new Dictionary<string, string> { ["left"] = member, ["right"] = otherMember },
                out _))
            diagnostics.Add(new("CSL021", "No deterministic C# equality rule is available."));
        if (!rules.TryRenderDeterministicExpression(
                EqualityNode(equality, "hash"),
                new Dictionary<string, string> { ["value"] = member },
                out _))
            diagnostics.Add(new("CSL022", "No deterministic C# equality hash rule is available."));
    }

    private static void RenderEquality(
        StringBuilder source,
        string typeName,
        EqualitySemantics equality,
        CSharpLoweringRuleSet rules,
        IReadOnlyDictionary<string, string> stateBindings)
    {
        var member = RenderSemanticReference(equality.By, stateBindings);
        var otherMember = "other." + member;
        rules.TryRenderDeterministicExpression(
            EqualityNode(equality, "equals"),
            new Dictionary<string, string> { ["left"] = member, ["right"] = otherMember },
            out var equalsExpression);
        rules.TryRenderDeterministicExpression(
            EqualityNode(equality, "hash"),
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

    private static string EqualityNode(EqualitySemantics equality, string operation) =>
        equality.Intrinsic is not null ? $"equality.{equality.Intrinsic}.{operation}" : $"equality.over.{operation}";

    private static string Camel(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static string Quote(string value) =>
        CSharpLiteral.String(value);
}
