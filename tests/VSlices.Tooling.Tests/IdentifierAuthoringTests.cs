using VSlices.Vsir;

namespace VSlices.Tooling.Tests;

public sealed class IdentifierAuthoringTests
{
    [Fact]
    public void Identifier_is_authored_as_a_semantic_trait_on_value_object()
    {
        Assert.Contains("identifier", VsirAuthoringContract.ExplicitDomainTypeTraits);
        Assert.DoesNotContain("identifier", VsirAuthoringContract.DomainTypeClassifications);

        var source = """
            vsir: 0.1
            kind: domain-type
            name: SrvIdentityId
            shape: product
            classification: value-object
            traits: [transform, identifier]
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        var equality = Assert.Single(frontier, item => item.Path == "equality");
        Assert.Equal(VsirFrontierStatus.Required, equality.Status);
        Assert.Equal("strategy", equality.ValueKind);
        Assert.Single(equality.Operations);
        Assert.Contains(VsirMutationKind.Set, equality.Operations);
    }

    [Fact]
    public void Identifier_trait_supports_setting_equality_and_canonical_parser_accepts_it()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: SrvIdentityId
            classification: value-object
            shape: product
            traits: [transform, identifier]
            state:
              Value: Rut
            representation:
              Value:
                type: Rut
                from: state.Value
            input: Rut
            construction:
              - refine:
                  value: input
                  as: state.Value
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "equality", "{over: Rut, by: state.Value}")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("equality:", result.Source);
        Assert.Contains("over: Rut", result.Source);
        Assert.Contains("by: state.Value", result.Source);

        var parsed = VsirParser.Parse(result.Source!);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
    }

    [Fact]
    public void Equality_without_identifier_trait_fails_closed()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            classification: value-object
            shape: product
            traits: [transform]
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "equality", "{over: Rut, by: state.Value}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE031:", result.Error);
    }
}
