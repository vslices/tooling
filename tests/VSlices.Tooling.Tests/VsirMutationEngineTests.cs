namespace VSlices.Tooling.Tests;

public sealed class VsirMutationEngineTests
{
    private const string Named = """
        vsir: 0.1
        name: StreetName
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Related_scalar_facts_can_be_established_in_one_transaction_regardless_of_order(bool reverse)
    {
        VsirMutation[] mutations =
        [
            new(VsirMutationKind.Set, "kind", "domain-type"),
            new(VsirMutationKind.Set, "classification", "value-object")
        ];

        if (reverse)
            Array.Reverse(mutations);

        var result = VsirMutationEngine.Apply(Named, mutations);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("kind: domain-type", result.Source);
        Assert.Contains("classification: value-object", result.Source);
    }

    [Fact]
    public void Invalid_late_mutation_rejects_the_complete_candidate()
    {
        var result = VsirMutationEngine.Apply(
            Named,
            [
                new(VsirMutationKind.Set, "kind", "domain-type"),
                new(VsirMutationKind.Set, "classification", "not-a-classification")
            ]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Source);
        Assert.StartsWith("UPDATE008:", result.Error);
    }

    [Fact]
    public void Discovery_from_named_artifact_exposes_kind_without_non_semantic_metadata_surfaces()
    {
        var frontier = VsirMutationEngine.Discover(Named, out var error);

        Assert.Null(error);
        Assert.DoesNotContain(frontier, item => item.Path == "tags");
        var kind = Assert.Single(frontier, item => item.Path == "kind");
        Assert.Equal(VsirFrontierStatus.Required, kind.Status);
        Assert.Contains("artifact family", kind.Meaning, StringComparison.Ordinal);
        Assert.Equal(["domain-type"], kind.AllowedValues);
    }

    [Fact]
    public void Discovery_after_kind_stops_at_shape_before_shape_dependent_decisions()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.DoesNotContain(frontier, item => item.Path == "tags");
        var shape = Assert.Single(frontier, item => item.Path == "shape");
        Assert.Equal(VsirFrontierStatus.Required, shape.Status);
        Assert.Equal(["product"], shape.AllowedValues);
        Assert.DoesNotContain(frontier, item => item.Path == "classification");
        Assert.DoesNotContain(frontier, item => item.Path == "state");
        Assert.DoesNotContain(frontier, item => item.Path == "representation");
    }

    [Fact]
    public void Value_object_discovery_exposes_required_state_representation_and_transform_capability()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);

        var state = Assert.Single(frontier, item => item.Path == "state");
        Assert.Equal(VsirFrontierStatus.Required, state.Status);
        Assert.Single(state.Operations);
        Assert.Contains(VsirMutationKind.Set, state.Operations);
        Assert.Contains("state.Value", state.Meaning, StringComparison.Ordinal);

        var representation = Assert.Single(frontier, item => item.Path == "representation");
        Assert.Equal(VsirFrontierStatus.Required, representation.Status);
        Assert.Single(representation.Operations);
        Assert.Contains(VsirMutationKind.Set, representation.Operations);
        Assert.Contains("representation.Value", representation.Meaning, StringComparison.Ordinal);

        var traits = Assert.Single(frontier, item => item.Path == "traits");
        Assert.Equal(VsirFrontierStatus.Required, traits.Status);
        Assert.Contains(VsirMutationKind.Add, traits.Operations);
        Assert.Contains(VsirMutationKind.Remove, traits.Operations);
        Assert.Contains(VsirMutationKind.Set, traits.Operations);
        Assert.Equal(["transform", "identifier", "refined"], traits.AllowedValues);
        Assert.Contains("requires transform", traits.Meaning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Transform_trait_exposes_required_input_and_optional_construction_affordance()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        var input = Assert.Single(frontier, item => item.Path == "input");
        Assert.Equal(VsirFrontierStatus.Required, input.Status);
        Assert.Equal("map<property, declaration> | scalar semantic type", input.ValueKind);
        Assert.Single(input.Operations);
        Assert.Contains(VsirMutationKind.Set, input.Operations);
        Assert.Contains("before Domain Type validity", input.Meaning, StringComparison.Ordinal);

        var construction = Assert.Single(frontier, item => item.Path == "construction");
        Assert.Equal(VsirFrontierStatus.Optional, construction.Status);
        Assert.Equal("sequence<step>", construction.ValueKind);
        Assert.Single(construction.Operations);
        Assert.Contains(VsirMutationKind.Set, construction.Operations);
    }

    [Fact]
    public void Transform_obligations_stop_being_reported_once_input_and_construction_exist()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            construction:
              - ensure:
                  condition:
                    intrinsic: non-empty
                    args:
                      value: input.Value
                  failure:
                    message: Debes especificar una calle
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.DoesNotContain(frontier, item => item.Path == "input");
        Assert.DoesNotContain(frontier, item => item.Path == "construction");
        Assert.Contains(frontier, item => item.Path == "traits");
    }

    [Fact]
    public void Discovery_keeps_state_and_representation_authoring_surfaces_after_they_are_present()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            state:
              Value: string
            representation:
              Value: string
            """;

        var frontier = VsirMutationEngine.Discover(source, out var error);

        Assert.Null(error);
        Assert.Contains(frontier, item => item.Path == "state");
        Assert.Contains(frontier, item => item.Path == "representation");
        Assert.Contains(frontier, item => item.Path == "traits");
        Assert.DoesNotContain(frontier, item => item.Path == "tags");
    }

    [Fact]
    public void Traits_can_be_added_after_domain_type_classification()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(source, [new(VsirMutationKind.Add, "traits", "transform")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("traits: [transform]", result.Source);
    }

    [Fact]
    public void Unknown_explicit_trait_is_rejected_fail_closed()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(source, [new(VsirMutationKind.Add, "traits", "unknown")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE026:", result.Error);
        Assert.Contains("transform", result.Error);
    }

    [Fact]
    public void State_and_representation_properties_can_be_added_atomically()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            """;

        var result = VsirMutationEngine.Apply(
            source,
            [
                new(VsirMutationKind.Add, "state.Value", "string"),
                new(VsirMutationKind.Add, "representation.Value", "string")
            ]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("state:", result.Source);
        Assert.Contains("Value: string", result.Source);
        Assert.Contains("representation:", result.Source);
    }

    [Fact]
    public void Add_rejects_existing_map_property_and_set_requires_existing_map_property()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            state:
              Value: string
            """;

        var duplicate = VsirMutationEngine.Apply(source, [new(VsirMutationKind.Add, "state.Value", "Rut")]);
        Assert.False(duplicate.IsSuccess);
        Assert.StartsWith("UPDATE021:", duplicate.Error);

        var missing = VsirMutationEngine.Apply(source, [new(VsirMutationKind.Set, "state.Other", "string")]);
        Assert.False(missing.IsSuccess);
        Assert.StartsWith("UPDATE022:", missing.Error);
    }

    [Fact]
    public void Set_replaces_an_existing_property_type()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: WrappedRut
            shape: product
            classification: value-object
            state:
              Value: string
            """;

        var result = VsirMutationEngine.Apply(source, [new(VsirMutationKind.Set, "state.Value", "Rut")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("Value: Rut", result.Source);
    }

    [Fact]
    public void Remove_can_delete_a_map_property_but_not_the_last_required_property()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            shape: product
            classification: value-object
            state:
              Street: string
              Number: string
            """;

        var first = VsirMutationEngine.Apply(source, [new(VsirMutationKind.Remove, "state.Number", "ignored")]);
        Assert.True(first.IsSuccess, first.Error);
        Assert.DoesNotContain("Number:", first.Source);

        var last = VsirMutationEngine.Apply(first.Source!, [new(VsirMutationKind.Remove, "state.Street", "ignored")]);
        Assert.False(last.IsSuccess);
        Assert.StartsWith("UPDATE024:", last.Error);
    }

    [Fact]
    public void State_from_can_be_established_changed_and_removed_without_losing_the_field_type()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            shape: product
            classification: value-object
            state:
              Region: Region
            """;

        var established = VsirMutationEngine.Apply(source, [new(VsirMutationKind.Set, "state.Region.from", "state.Commune.InProvince.InRegion")]);
        Assert.True(established.IsSuccess, established.Error);
        Assert.Contains("type: Region", established.Source);
        Assert.Contains("from: state.Commune.InProvince.InRegion", established.Source);

        var changed = VsirMutationEngine.Apply(established.Source!, [new(VsirMutationKind.Set, "state.Region.from", "state.Commune.Region")]);
        Assert.True(changed.IsSuccess, changed.Error);
        Assert.Contains("from: state.Commune.Region", changed.Source);

        var removed = VsirMutationEngine.Apply(changed.Source!, [new(VsirMutationKind.Remove, "state.Region.from", "ignored")]);
        Assert.True(removed.IsSuccess, removed.Error);
        Assert.Contains("Region: Region", removed.Source);
        Assert.DoesNotContain("from:", removed.Source);
    }

    [Fact]
    public void Unsupported_deep_representation_path_remains_fail_closed()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            representation:
              Value: string
            """;

        var result = VsirMutationEngine.Apply(source, [new(VsirMutationKind.Set, "representation.Value.mapping.stringify", "state.Value")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE004:", result.Error);
    }
}
