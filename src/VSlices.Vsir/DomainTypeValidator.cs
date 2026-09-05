using System.Text.RegularExpressions;

namespace VSlices.Vsir;

public static class DomainTypeValidator
{
    private static readonly HashSet<string> SupportedTraits =
        new(["transform", "identifier", "refined"], StringComparer.Ordinal);

    private static readonly HashSet<string> SupportedNormalizeIntrinsics =
        new(["trim"], StringComparer.Ordinal);

    private static readonly Regex TypeReferencePattern = new(
        "^[A-Za-z_][A-Za-z0-9_.]*(<[A-Za-z0-9_.,<> ]+>)?$",
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

        if (document.Construction.Input.IsScalar)
            Require(IsTypeReference(document.Construction.Input.ScalarType!), "VSIR208", $"Invalid construction input type reference '{document.Construction.Input.ScalarType}'.");

        if (document.RefinedFrom is not null)
            Require(IsTypeReference(document.RefinedFrom), "VSIR222", $"Invalid refined-from type reference '{document.RefinedFrom}'.");

        var isRefined = document.Traits.Contains("refined", StringComparer.Ordinal);
        if (isRefined)
        {
            Require(document.RefinedFrom is not null, "VSIR223", "Trait 'refined' requires canonical 'refined-from' semantics.");
            Require(document.Construction.Input.IsScalar, "VSIR224", "The currently evidenced refined domain-type shape requires a scalar construction input.");

            if (document.RefinedFrom is not null && document.Construction.Input.IsScalar)
            {
                Require(
                    string.Equals(document.RefinedFrom, document.Construction.Input.ScalarType, StringComparison.Ordinal),
                    "VSIR225",
                    $"Refined construction input '{document.Construction.Input.ScalarType}' must match refined-from '{document.RefinedFrom}'.");
            }
        }
        else
        {
            Require(document.RefinedFrom is null, "VSIR226", "'refined-from' requires trait 'refined'.");
        }

        foreach (var refine in document.Construction.Steps.OfType<RefineStep>())
            ValidateRefine(refine);

        foreach (var stateField in document.State.Fields)
        {
            var established = TryConstructionSourceForState(stateField.Name, out var sourceType);
            Require(established && string.Equals(sourceType, stateField.Type, StringComparison.Ordinal), "VSIR209",
                $"Cannot establish state.{stateField.Name} deterministically from construction input. A mapping/refinement is required.");
        }

        ValidateRepresentationMapping();

        foreach (var representationField in document.Representation.Fields)
        {
            var matchingState = document.State.Fields.SingleOrDefault(x => x.Name == representationField.Name);
            var mapped = document.RepresentationMapping?.Fields.ContainsKey(representationField.Name) == true;
            Require(
                mapped || (matchingState is not null && matchingState.Type == representationField.Type),
                "VSIR210",
                $"Cannot project representation.{representationField.Name} deterministically from state. A representation mapping is required.");
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
                _ => string.Empty
            };

            Require(TryInputReferenceType(value, out _), "VSIR211",
                $"Only construction input references are supported, got '{value}'.");
        }

        if (document.Equality is not null)
        {
            var equality = document.Equality;
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
                Require(IsTypeReference(equality.Over), "VSIR227", $"Invalid equality over type reference '{equality.Over}'.");
                if (equalityField is not null)
                {
                    Require(
                        string.Equals(equalityField.Type, equality.Over, StringComparison.Ordinal),
                        "VSIR228",
                        $"Equality over '{equality.Over}' does not match state field type '{equalityField.Type}'.");
                }
            }
        }

        if (document.Traits.Contains("identifier", StringComparer.Ordinal))
            Require(document.Equality is not null, "VSIR216",
                "Trait 'identifier' requires explicit equality semantics because the Framework Identifier contract is a discrete space.");

        if (isRefined && document.RefinedFrom is not null)
        {
            var baseStateFields = document.State.Fields
                .Where(x => string.Equals(x.Type, document.RefinedFrom, StringComparison.Ordinal))
                .ToArray();
            Require(baseStateFields.Length == 1, "VSIR229",
                $"The currently evidenced refined domain-type shape requires exactly one state field of refined-from type '{document.RefinedFrom}'.");
        }

        return diagnostics;

        void ValidateRefine(RefineStep refine)
        {
            Require(refine.As.StartsWith("state.", StringComparison.Ordinal), "VSIR230",
                $"Refine target must be a state reference, got '{refine.As}'.");

            if (!TryInputReferenceType(refine.Value, out var inputType))
            {
                diagnostics.Add(new("VSIR231", $"Refine value must reference construction input, got '{refine.Value}'."));
                return;
            }

            if (!refine.As.StartsWith("state.", StringComparison.Ordinal))
                return;

            var stateName = refine.As["state.".Length..];
            var stateField = document.State.Fields.SingleOrDefault(x => x.Name == stateName);
            Require(stateField is not null, "VSIR232", $"Refine references unknown state field '{stateName}'.");
            if (stateField is not null)
            {
                Require(
                    string.Equals(stateField.Type, inputType, StringComparison.Ordinal),
                    "VSIR233",
                    $"Refine source type '{inputType}' does not match state.{stateName} type '{stateField.Type}'.");
            }
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

                switch (pair.Value)
                {
                    case StringifyProjection stringify:
                    {
                        Require(representationField.Type == "string", "VSIR235",
                            $"Stringify projection requires representation.{pair.Key} to be string, got '{representationField.Type}'.");
                        Require(stringify.Value.StartsWith("state.", StringComparison.Ordinal), "VSIR236",
                            $"Stringify projection requires a state reference, got '{stringify.Value}'.");
                        if (stringify.Value.StartsWith("state.", StringComparison.Ordinal))
                        {
                            var stateName = stringify.Value["state.".Length..];
                            Require(document.State.Fields.Any(x => x.Name == stateName), "VSIR237",
                                $"Stringify projection references unknown state field '{stateName}'.");
                        }
                        break;
                    }
                }
            }
        }

        bool TryConstructionSourceForState(string stateName, out string? type)
        {
            if (!document.Construction.Input.IsScalar)
            {
                var matchingInput = document.Construction.Input.Fields.SingleOrDefault(x => x.Name == stateName);
                if (matchingInput is not null)
                {
                    type = matchingInput.Type;
                    return true;
                }
            }

            var refine = document.Construction.Steps
                .OfType<RefineStep>()
                .SingleOrDefault(x => x.As == "state." + stateName);
            if (refine is not null && TryInputReferenceType(refine.Value, out var refinedType))
            {
                type = refinedType;
                return true;
            }

            type = null;
            return false;
        }

        bool TryInputReferenceType(string reference, out string? type)
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

    private static bool IsTypeReference(string type) =>
        !string.IsNullOrWhiteSpace(type) && TypeReferencePattern.IsMatch(type);
}
