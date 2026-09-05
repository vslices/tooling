namespace VSlices.Vsir;

public static class DomainTypeValidator
{
    private static readonly HashSet<string> SupportedTraits =
        new(["transform", "identifier"], StringComparer.Ordinal);

    private static readonly HashSet<string> SupportedNormalizeIntrinsics =
        new(["trim"], StringComparer.Ordinal);

    public static IReadOnlyList<VsirDiagnostic> Validate(
        DomainTypeVsir document,
        VsirSemanticExtensions? semanticExtensions = null)
    {
        semanticExtensions ??= VsirSemanticExtensions.None;
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
        {
            Require(
                SupportedTraits.Contains(trait),
                "VSIR218",
                $"Unsupported trait '{trait}'.");
        }

        Require(
            document.Traits.Contains("transform", StringComparer.Ordinal),
            "VSIR204",
            "The experimental domain-type model currently requires trait 'transform'.");
        Require(document.State.Fields.Count > 0, "VSIR205", "State must contain at least one field.");
        Require(document.Representation.Fields.Count > 0, "VSIR206", "Representation must contain at least one field.");
        Require(document.Construction.Input.Fields.Count > 0, "VSIR207", "Construction input must contain at least one field.");

        foreach (var field in document.State.Fields.Concat(document.Representation.Fields).Concat(document.Construction.Input.Fields))
            Require(field.Type == "string", "VSIR208", $"Unsupported field type '{field.Type}' on '{field.Name}'.");

        foreach (var stateField in document.State.Fields)
        {
            var matchingInput = document.Construction.Input.Fields.SingleOrDefault(x => x.Name == stateField.Name);
            Require(matchingInput is not null && matchingInput.Type == stateField.Type, "VSIR209",
                $"Cannot establish state.{stateField.Name} deterministically from construction input. A mapping/refinement is required.");
        }

        foreach (var representationField in document.Representation.Fields)
        {
            var matchingState = document.State.Fields.SingleOrDefault(x => x.Name == representationField.Name);
            Require(matchingState is not null && matchingState.Type == representationField.Type, "VSIR210",
                $"Cannot project representation.{representationField.Name} deterministically from state. A representation mapping is required.");
        }

        foreach (var normalize in document.Construction.Steps.OfType<NormalizeStep>())
        {
            Require(
                SupportedNormalizeIntrinsics.Contains(normalize.Intrinsic) || semanticExtensions.DeclaresNormalize(normalize.Intrinsic),
                "VSIR221",
                $"Unsupported normalize intrinsic '{normalize.Intrinsic}'.");

            Require(
                normalize.Target.StartsWith("input.", StringComparison.Ordinal),
                "VSIR219",
                $"Normalize currently requires a construction input target, got '{normalize.Target}'.");

            if (normalize.Target.StartsWith("input.", StringComparison.Ordinal))
            {
                var fieldName = normalize.Target["input.".Length..];
                Require(
                    document.Construction.Input.Fields.Any(x => x.Name == fieldName),
                    "VSIR220",
                    $"Normalize references unknown input field '{fieldName}'.");
            }
        }

        foreach (var ensure in document.Construction.Steps.OfType<EnsureStep>())
        {
            var value = ensure.Condition switch
            {
                NonEmptyCondition x => x.Value,
                LengthAtMostCondition x => x.Value,
                _ => string.Empty
            };

            Require(value.StartsWith("input.", StringComparison.Ordinal), "VSIR211",
                $"Only construction input references are supported, got '{value}'.");

            if (value.StartsWith("input.", StringComparison.Ordinal))
            {
                var fieldName = value["input.".Length..];
                Require(document.Construction.Input.Fields.Any(x => x.Name == fieldName), "VSIR212",
                    $"Condition references unknown input field '{fieldName}'.");
            }
        }

        if (document.Equality is not null)
        {
            Require(
                document.Equality.Intrinsic == "ordinal-equals",
                "VSIR213",
                $"Unsupported equality intrinsic '{document.Equality.Intrinsic}'.");

            Require(
                document.Equality.By.StartsWith("state.", StringComparison.Ordinal),
                "VSIR214",
                $"Equality currently requires a state reference, got '{document.Equality.By}'.");

            if (document.Equality.By.StartsWith("state.", StringComparison.Ordinal))
            {
                var fieldName = document.Equality.By["state.".Length..];
                Require(
                    document.State.Fields.Any(x => x.Name == fieldName),
                    "VSIR215",
                    $"Equality references unknown state field '{fieldName}'.");
            }
        }

        if (document.Traits.Contains("identifier", StringComparer.Ordinal))
        {
            Require(
                document.Equality is not null,
                "VSIR216",
                "Trait 'identifier' requires explicit equality semantics because the Framework Identifier contract is a discrete space.");
        }

        return diagnostics;

        void Require(bool condition, string code, string message)
        {
            if (!condition)
                diagnostics.Add(new(code, message));
        }
    }
}
