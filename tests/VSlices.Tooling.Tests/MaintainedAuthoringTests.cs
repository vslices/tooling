namespace VSlices.Tooling.Tests;

public sealed class MaintainedAuthoringTests
{
    [Fact]
    public void Maintained_is_not_advertised_until_canonical_parser_validator_and_lowering_support_it()
    {
        Assert.DoesNotContain("maintained", VsirAuthoringContract.DomainTypeClassifications);

        var created = VsirTemplate.Create("IdentityType");
        Assert.True(created.IsSuccess, created.Error);

        var result = VsirMutationPipeline.Apply(
            created.Source!,
            [
                new(VsirMutationKind.Set, "kind", "domain-type"),
                new(VsirMutationKind.Set, "shape", "product"),
                new(VsirMutationKind.Set, "classification", "maintained")
            ]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE008:", result.Error);
    }

    [Fact]
    public void Maintained_classification_mutation_fails_closed()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            shape: product
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [new(VsirMutationKind.Set, "classification", "maintained")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE008:", result.Error);
    }

    [Fact]
    public void Values_are_not_a_public_affordance_on_the_current_end_to_end_surface()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Name: string
            representation:
              Name: string
            input: string
            construction:
              - refine:
                  value: input
                  as: state.Name
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.DoesNotContain(frontier, item => item.Path == "values");
    }
}
