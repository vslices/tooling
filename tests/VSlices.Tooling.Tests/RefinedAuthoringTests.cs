using VSlices.Vsir;

namespace VSlices.Tooling.Tests;

public sealed class RefinedAuthoringTests
{
    [Fact]
    public void SrvIdentityId_refined_identifier_surface_can_be_authored_to_canonical_conformance()
    {
        var created = VsirTemplate.Create(
            "SrvIdentityId",
            "domain-type",
            "product",
            "identifier");

        Assert.True(created.IsSuccess, created.Error);
        var current = created.Source!;

        current = Apply(
            current,
            new(VsirMutationKind.Set, "state.Value", "Rut"),
            new(VsirMutationKind.Set, "representation.Value", "string"),
            new(VsirMutationKind.Add, "traits", "transform,refined"));

        var frontier = VsirMutationPipeline.Discover(current, out var error);
        Assert.Null(error);
        AssertCan(frontier, "refined-from", VsirMutationKind.Set);
        AssertCan(frontier, "equality", VsirMutationKind.Set);
        AssertCan(frontier, "input", VsirMutationKind.Set);
        AssertCan(frontier, "construction", VsirMutationKind.Set);

        current = Apply(
            current,
            new(VsirMutationKind.Set, "refined-from", "Rut"),
            new(VsirMutationKind.Set, "representation.Value.mapping", "{stringify: state.Value}"),
            new(VsirMutationKind.Set, "input", "Rut"),
            new(VsirMutationKind.Set, "equality", "{over: Rut, by: state.Value}"),
            new(VsirMutationKind.Set, "construction", "[{refine: {value: input, as: state.Value}}]"));

        var parsed = VsirParser.Parse(current);

        Assert.True(
            parsed.IsSuccess,
            string.Join(Environment.NewLine, parsed.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
        Assert.Equal("identifier", parsed.Document!.Classification);
        Assert.Equal("Rut", parsed.Document.RefinedFrom);
        Assert.Contains("refined", parsed.Document.Traits);
        Assert.Contains("transform", parsed.Document.Traits);
        Assert.DoesNotContain("identifier", parsed.Document.Traits);
        Assert.Equal(new NamedVsirType("Rut"), parsed.Document.Construction.Input.ScalarType);
        Assert.Equal("Rut", parsed.Document.Equality!.Over);
    }

    [Fact]
    public void Refined_from_without_refined_trait_fails_closed()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            traits: [transform]
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "refined-from", "Rut")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE048:", result.Error);
    }

    private static string Apply(string source, params VsirMutation[] mutations)
    {
        var result = VsirMutationPipeline.Apply(source, mutations);
        Assert.True(result.IsSuccess, result.Error);
        return result.Source!;
    }

    private static void AssertCan(
        IReadOnlyList<VsirPathContract> frontier,
        string path,
        VsirMutationKind operation)
    {
        var contract = Assert.Single(frontier, item => item.Path == path);
        Assert.Contains(operation, contract.Operations);
    }
}
