using VSlices.Vsir;

namespace VSlices.Tooling.Tests;

public sealed class IdentifierAuthoringTests
{
    [Fact]
    public void Identifier_is_evidenced_as_both_classification_and_explicit_capability()
    {
        Assert.Contains("identifier", VsirAuthoringContract.DomainTypeClassifications);
        Assert.Contains("identifier", VsirAuthoringContract.ExplicitDomainTypeTraits);

        var source = """
            vsir: 0.1
            kind: domain-type
            name: TicketId
            shape: product
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        var classification = Assert.Single(frontier, item => item.Path == "classification");
        Assert.Contains("value-object", classification.AllowedValues!);
        Assert.Contains("identifier", classification.AllowedValues!);
    }

    [Fact]
    public void Identifier_classification_requires_equality_in_discovery()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: TicketId
            shape: product
            classification: identifier
            traits: [transform]
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
    public void Identifier_trait_requires_equality_for_value_object_classification()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: TicketCode
            shape: product
            classification: value-object
            traits: [transform, identifier]
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        var equality = Assert.Single(frontier, item => item.Path == "equality");
        Assert.Equal(VsirFrontierStatus.Required, equality.Status);
    }

    [Fact]
    public void TicketId_classification_supports_equality_and_direct_construction()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: TicketId
            classification: identifier
            shape: product
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "equality", "{intrinsic: ordinal-equals, by: state.Value}")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("equality:", result.Source);
        Assert.Contains("intrinsic: ordinal-equals", result.Source);
        Assert.Contains("by: state.Value", result.Source);
        Assert.DoesNotContain("construction:", result.Source);

        var parsed = VsirParser.Parse(result.Source!);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
        Assert.Equal("identifier", parsed.Document!.Classification);
        Assert.DoesNotContain("identifier", parsed.Document.Traits);
        Assert.Empty(parsed.Document.Construction.Steps);
    }

    [Fact]
    public void TicketCode_identifier_trait_supports_equality_without_identifier_classification()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: TicketCode
            classification: value-object
            shape: product
            traits: [transform, identifier]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "equality", "{intrinsic: ordinal-equals, by: state.Value}")]);

        Assert.True(result.IsSuccess, result.Error);
        var parsed = VsirParser.Parse(result.Source!);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
        Assert.Equal("value-object", parsed.Document!.Classification);
        Assert.Contains("identifier", parsed.Document.Traits);
        Assert.NotNull(parsed.Document.Equality);
    }

    [Fact]
    public void Equality_without_identifier_semantics_fails_closed()
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
            [new(VsirMutationKind.Set, "equality", "{intrinsic: ordinal-equals, by: state.Value}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE031:", result.Error);
    }
}
