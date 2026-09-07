using System.Text.RegularExpressions;

namespace VSlices.Vsir;

public static class DomainTypeValidator
{
    private static readonly HashSet<string> SupportedTraits =
        new(["transform", "identifier", "refined"], StringComparer.Ordinal);

    private static readonly HashSet<string> SupportedNormalizeIntrinsics =
        new(["trim"], StringComparer.Ordinal);

    private static readonly Regex NamedTypeReferencePattern = new(
        "^[A-Za-z_][A-Za-z0-9_.]*(<[A-Za-z0-9_.,<> ]+>)?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex TypeConstructorPattern = new(
        "^[A-Za-z_][A-Za-z0-9_.-]*$",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<VsirDiagnostic> Validate(
        DomainTypeVsir document,
        VsirValidationContext? validationContext = null)
    {
        validationContext ??= VsirValidationContext.Empty;
        var semanticExtensions = validationContext.SemanticExtensions;
        var diagnostics = new List<VsirDiagnostic>();

        Require(document.Version == "0.1", "VSIR200", "Only VSIR 0.1 is supported.");
        Require(document.Kind == "domain-type", "VSIR201", "Only kind 'domain-type' is supported.");
        Require(document.Classification == "value-object", "VSIR202", "Only classification 'value-object' is supported.");
        Require(document.Shape == "product", "VSIR203", "Only shape 'product' is supported.");

        var duplicateTraits = document.Traits
            .GroupBy(x => x, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (var duplicate in duplicateTraits)
            diagnostics.Add(new("VSIR217", $"Trait '{duplicate}' is declared more than once. Traits are an unordered set of capabilities."));

        foreach (var trait in document.Traits.Distinct(StringComparer.Ordinal))
            Require(SupportedTraits.Contains(trait), "VSIR218", $"Unsupported trait '{trait}'.");

        Require(document.Traits.Contains("transform", StringComparer.Ordinal), "VSIR204", "The experimental domain-type model currently requires trait 'transform'.");
        Require(document.State.Fields.Count > 0, "VSIR205", "State must contain at least one field.");
        Require(document.Representation.Fields.Count > 0, "VSIR206", "Representation must contain at least one field.");
        Require(document.Construction.Input.IsScalar || document.Construction.Input.Fields.Count > 0, "VSIR207", "Construction input must contain at least one field or declare a scalar nominal type.");

        foreach (var field in document.State.Fields.Concat(document.Representation.Fields).Concat(document.Construction.Input.Fields))
            Require(IsTypeReference(field.Type), "VSIR208", $"Invalid semantic type reference '{field.Type}' on '{field.Name}'.");

        if (document.Construction.Input.ScalarType is not null)
            Require(IsTypeReference(document.Construction.Input.ScalarType), "VSIR208", $"Invalid construction input type reference '{document.Construction.Input.ScalarType}'.");

        if (document.RefinedFrom is not null)
            Require(IsNamedTypeReference(document.RefinedFrom), "VSIR222", $"Invalid refined-from type reference '{document.RefinedFrom}'.");

        var isRefined = document.Traits.Contains("refined", StringComparer.Ordinal);
        if (isRefined)
        {
            Require(document.RefinedFrom is not null, "VSIR223", "Trait 'refined' requires canonical 'refined-from' semantics.");
            Require(document.Construction.Input.IsScalar, "VSIR224", "The currently evidenced refined domain-type shape requires a scalar construction input.");

            if (document.RefinedFrom is not null && document.Construction.Input.IsScalar)
            {
                Require(
                    document.Construction.Input.ScalarType == new NamedVsirType(document.RefinedFrom),
                    "VSIR225",
                    $"Refined construction input '{document.Construction.Input.ScalarType}' must match refined-from '{document.RefinedFrom}'.");
            }
        }
        else
        {
            Require(document.RefinedFrom is null, "VSIR226", "'refined-from' requires trait 'refined'.");
        }

        ValidateStateSources();
        ValidateConstructionBindings();
        ValidateRepresentationMapping();

        foreach (var representationField in document.Representation.Fields)
        {
            var matchingState = document.State.Fields.SingleOrDefault(x => x.Name == representationField.Name);
            var mapped = document.RepresentationMapping?.Fields.ContainsKey(representationField.Name) == true;
            var directFrom = !string.IsNullOrWhiteSpace(representationField.From);
            Require(
                mapped || directFrom || (matchingState is not null && matchingState.Type == representationField.Type),
                "VSIR210",
                $"Cannot project representation.{representationField.Name} deterministically from state. A direct source or representation mapping is required.");
        }

        foreach (var normalize in document.Construction.Steps.OfType<NormalizeStep>())
        {
            Require(
                SupportedNormalizeIntrinsics.Contains(normalize.Intrinsic) || semanticExtensions.DeclaresNormalize(normalize.Intrinsic),
                "VSIR221",
                $"Unsupported normalize intrinsic '{normalize.Intrinsic}'.");

            Require(TryInputReferenceType(normalize.Target, out _), "VSIR219",
                $"Normalize requires a construction input reference, got '{normalize.Target}'.");
        }

        foreach (var ensure in document.Construction.Steps.OfType<EnsureStep>())
        {
            var value = ensure.Condition switch
            {
                NonEmptyCondition x => x.Value,
                NotWhitespaceCondition x => x.Value,
                LengthAtMostCondition x => x.Value,
                LengthBetweenCondition x => x.Value,
                _ => string.Empty
            };

            Require(TryInputReferenceType(value, out _), "VSIR211",
                $"Only construction input references are supported by the current ensure boundary, got '{value}'.");
        }

        if (document.Equality is not null)
            ValidateEquality(document.Equality);

        if (document.Traits.Contains("identifier", StringComparer.Ordinal))
            Require(document.Equality is not null, "VSIR216",
                "Trait 'identifier' requires explicit equality semantics because the Framework Identifier contract is a discrete space.");

        if (isRefined && document.RefinedFrom is not null)
        {
            var baseType = new NamedVsirType(document.RefinedFrom);
            var baseStateFields = document.State.Fields
                .Where(x => x.Type == baseType)
                .ToArray();
            Require(baseStateFields.Length == 1, "VSIR229",
                $"The currently evidenced refined domain-type shape requires exactly one state field of refined-from type '{document.RefinedFrom}'.");
        }

        return diagnostics;

        void ValidateStateSources()
        {
            foreach (var field in document.State.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.From))
                    continue;

                Require(field.From.StartsWith("state.", StringComparison.Ordinal), "VSIR238",
                    $"Derived state.{field.Name} requires a state reference, got '{field.From}'.");

                if (!field.From.StartsWith("state.", StringComparison.Ordinal))
                    continue;

                var firstSegment = field.From["state.".Length..].Split('.', 2)[0];
                Require(document.State.Fields.Any(x => x.Name == firstSegment), "VSIR239",
                    $"Derived state.{field.Name} references unknown state field '{firstSegment}'.");
                Require(firstSegment != field.Name, "VSIR240",
                    $"Derived state.{field.Name} must not derive directly from itself.");
            }
        }

        void ValidateConstructionBindings()
        {
            var bindings = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in document.Construction.Steps)
            {
                switch (step)
                {
                    case ResolveStep resolve:
                        Require(!string.IsNullOrWhiteSpace(resolve.Source), "VSIR241", "Resolve requires source.");
                        Require(TryInputReferenceType(resolve.Id, out _), "VSIR242",
                            $"Resolve id must currently reference transform input, got '{resolve.Id}'.");
                        Require(bindings.Add(resolve.As), "VSIR243",
                            $"Construction binding '{resolve.As}' is declared more than once.");
                        break;

                    case ApplyStep apply:
                        ValidateApplyInput(apply.Input);
                        Require(bindings.Add(apply.As), "VSIR243",
                            $"Construction binding '{apply.As}' is declared more than once.");
                        break;

                    case RefineStep refine:
                        ValidateRefine(refine, bindings);
                        break;
                }
            }

            foreach (var stateField in document.State.Fields.Where(field => string.IsNullOrWhiteSpace(field.From)))
            {
                var established = TryConstructionSourceForState(stateField.Name, bindings, out var sourceType);
                Require(established && (sourceType is null || sourceType == stateField.Type), "VSIR209",
                    $"Cannot establish state.{stateField.Name} deterministically from construction input or an explicit construction binding.");
            }
        }

        void ValidateApplyInput(ApplyInput input)
        {
            switch (input)
            {
                case DirectApplyInput direct:
                    foreach (var value in direct.Fields.Values)
                        Require(TryInputReferenceType(value, out _), "VSIR244",
                            $"Direct apply input currently requires input references, got '{value}'.");
                    break;
                case MappedApplyInput mapped:
                    Require(TryInputReferenceType(mapped.Source, out _), "VSIR245",
                        $"Mapped apply source currently requires an input reference, got '{mapped.Source}'.");
                    Require(mapped.Map.Count > 0, "VSIR246", "Mapped apply requires at least one input mapping.");
                    break;
            }
        }

        void ValidateRefine(RefineStep refine, IReadOnlySet<string> bindings)
        {
            Require(refine.As.StartsWith("state.", StringComparison.Ordinal), "VSIR230",
                $"Refine target must be a state reference, got '{refine.As}'.");

            if (!refine.As.StartsWith("state.", StringComparison.Ordinal))
                return;

            var stateName = refine.As["state.".Length..];
            var stateField = document.State.Fields.SingleOrDefault(x => x.Name == stateName);
            Require(stateField is not null, "VSIR232", $"Refine references unknown state field '{stateName}'.");
            if (stateField is null)
                return;

            if (TryInputReferenceType(refine.Value, out var inputType))
            {
                Require(stateField.Type == inputType, "VSIR233",
                    $"Refine source type '{inputType}' does not match state.{stateName} type '{stateField.Type}'.");
                return;
            }

            Require(bindings.Contains(refine.Value), "VSIR231",
                $"Refine value must reference transform input or a previously established construction binding, got '{refine.Value}'.");
        }

        void ValidateRepresentationMapping()
        {
            if (document.RepresentationMapping is null)
                return;

            foreach (var pair in document.RepresentationMapping.Fields)
            {
                var representationField = document.Representation.Fields.SingleOrDefault(x => x.Name == pair.Key);
                Require(representationField is not null, "VSIR234",
                    $"Representation mapping references unknown representation field '{pair.Key}'.");
                if (representationField is null)
                    continue;

                ValidateProjection(pair.Value, pair.Key, representationField.Type, new HashSet<string>(StringComparer.Ordinal));
            }
        }

        void ValidateProjection(
            RepresentationProjection projection,
            string fieldName,
            VsirType targetType,
            IReadOnlySet<string> bindings)
        {
            switch (projection)
            {
                case ReferenceProjection reference:
                    ValidateSemanticReference(reference.Value, bindings, fieldName);
                    break;
                case StringifyProjection stringify:
                    Require(targetType == new NamedVsirType("string"), "VSIR235",
                        $"Stringify projection requires representation.{fieldName} to be string, got '{targetType}'.");
                    ValidateSemanticReference(stringify.Value, bindings, fieldName);
                    break;
                case RepresentProjection represent:
                    ValidateProjection(represent.Value, fieldName, targetType, bindings);
                    break;
                case SelectProjection select:
                    ValidateProjection(select.Source, fieldName, targetType, bindings);
                    Require(!string.IsNullOrWhiteSpace(select.Field), "VSIR247", "Select requires a field name.");
                    break;
                case MapProjection map:
                {
                    ValidateProjection(map.Source, fieldName, targetType, bindings);
                    Require(!string.IsNullOrWhiteSpace(map.Bind), "VSIR248", "Map requires a binding name.");
                    var nested = new HashSet<string>(bindings, StringComparer.Ordinal) { map.Bind };
                    ValidateProjection(map.Value, fieldName, targetType, nested);
                    break;
                }
                case IntrinsicProjection intrinsic:
                    Require(!string.IsNullOrWhiteSpace(intrinsic.Intrinsic), "VSIR249", "Intrinsic projection requires an intrinsic name.");
                    foreach (var value in intrinsic.Values)
                        ValidateProjection(value, fieldName, targetType, bindings);
                    break;
                default:
                    diagnostics.Add(new("VSIR250", $"Unsupported representation projection for '{fieldName}'."));
                    break;
            }
        }

        void ValidateSemanticReference(string reference, IReadOnlySet<string> bindings, string fieldName)
        {
            if (bindings.Contains(reference))
                return;

            if (reference.StartsWith("state.", StringComparison.Ordinal))
            {
                var stateName = reference["state.".Length..].Split('.', 2)[0];
                Require(document.State.Fields.Any(x => x.Name == stateName), "VSIR237",
                    $"Representation mapping for '{fieldName}' references unknown state field '{stateName}'.");
                return;
            }

            Require(false, "VSIR251", $"Representation mapping for '{fieldName}' references unknown semantic value '{reference}'.");
        }

        void ValidateEquality(EqualitySemantics equality)
        {
            Require(equality.By.StartsWith("state.", StringComparison.Ordinal), "VSIR214",
                $"Equality currently requires a state reference, got '{equality.By}'.");

            Field? equalityField = null;
            if (equality.By.StartsWith("state.", StringComparison.Ordinal))
            {
                var fieldName = equality.By["state.".Length..];
                equalityField = document.State.Fields.SingleOrDefault(x => x.Name == fieldName);
                Require(equalityField is not null, "VSIR215",
                    $"Equality references unknown state field '{fieldName}'.");
            }

            if (equality.Intrinsic is not null)
            {
                Require(equality.Intrinsic == "ordinal-equals", "VSIR213",
                    $"Unsupported equality intrinsic '{equality.Intrinsic}'.");
            }
            else if (equality.Over is not null)
            {
                Require(IsNamedTypeReference(equality.Over), "VSIR227", $"Invalid equality over type reference '{equality.Over}'.");
                if (equalityField is not null)
                {
                    Require(equalityField.Type == new NamedVsirType(equality.Over), "VSIR228",
                        $"Equality over '{equality.Over}' does not match state field type '{equalityField.Type}'.");
                }
            }
        }

        bool TryConstructionSourceForState(
            string stateName,
            IReadOnlySet<string> bindings,
            out VsirType? type)
        {
            var refine = document.Construction.Steps
                .OfType<RefineStep>()
                .SingleOrDefault(x => x.As == "state." + stateName);
            if (refine is not null)
            {
                if (TryInputReferenceType(refine.Value, out var refinedType))
                {
                    type = refinedType;
                    return true;
                }

                if (bindings.Contains(refine.Value))
                {
                    type = null;
                    return true;
                }
            }

            if (!document.Construction.Input.IsScalar)
            {
                var matchingInput = document.Construction.Input.Fields.SingleOrDefault(x => x.Name == stateName);
                if (matchingInput is not null)
                {
                    type = matchingInput.Type;
                    return true;
                }
            }

            type = null;
            return false;
        }

        bool TryInputReferenceType(string reference, out VsirType? type)
        {
            if (document.Construction.Input.IsScalar)
            {
                if (reference == "input")
                {
                    type = document.Construction.Input.ScalarType;
                    return true;
                }

                type = null;
                return false;
            }

            if (!reference.StartsWith("input.", StringComparison.Ordinal))
            {
                type = null;
                return false;
            }

            var fieldName = reference["input.".Length..];
            var field = document.Construction.Input.Fields.SingleOrDefault(x => x.Name == fieldName);
            type = field?.Type;
            return field is not null;
        }

        void Require(bool condition, string code, string message)
        {
            if (!condition)
                diagnostics.Add(new(code, message));
        }
    }

    private static bool IsTypeReference(VsirType type) =>
        type switch
        {
            NamedVsirType named => IsNamedTypeReference(named.Name),
            UnaryVsirType unary =>
                !string.IsNullOrWhiteSpace(unary.Constructor) &&
                TypeConstructorPattern.IsMatch(unary.Constructor) &&
                IsTypeReference(unary.Value),
            _ => false
        };

    private static bool IsNamedTypeReference(string type) =>
        !string.IsNullOrWhiteSpace(type) && NamedTypeReferencePattern.IsMatch(type);
}