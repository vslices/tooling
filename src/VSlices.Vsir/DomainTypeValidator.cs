namespace VSlices.Vsir;

public static class DomainTypeValidator
{
    public static IReadOnlyList<VsirDiagnostic> Validate(DomainTypeVsir document)
    {
        var diagnostics = new List<VsirDiagnostic>();

        Require(document.Version == "0.1", "VSIR200", "Only VSIR 0.1 is supported.");
        Require(document.Kind == "domain-type", "VSIR201", "Only kind 'domain-type' is supported.");
        Require(document.Classification == "value-object", "VSIR202", "Only classification 'value-object' is supported.");
        Require(document.Shape == "product", "VSIR203", "Only shape 'product' is supported.");
        Require(
            document.Traits.SequenceEqual(["transform"]) ||
            document.Traits.SequenceEqual(["identifier", "transform"]),
            "VSIR204",
            "The experimental model currently supports traits [transform] or [identifier, transform].");
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

        return diagnostics;

        void Require(bool condition, string code, string message)
        {
            if (!condition)
                diagnostics.Add(new(code, message));
        }
    }
}
