namespace VSlices.Tooling.Tests;

public sealed class LocationAuthoringTests
{
    [Fact]
    public void Structured_semantic_fields_can_be_established_and_replaced_with_set()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Commune: Commune
            representation:
              CommuneId: string
            """;

        var established = VsirMutationPipeline.Apply(
            source,
            [
                new(VsirMutationKind.Set, "state.Extensions", "{sequence: StreetExtension}"),
                new(VsirMutationKind.Set, "representation.Ext", "{type: {sequence: string}}"),
                new(VsirMutationKind.Set, "input.Ext", "{sequence: string}")
            ]);

        Assert.True(established.IsSuccess, established.Error);

        var replaced = VsirMutationPipeline.Apply(
            established.Source!,
            [new(VsirMutationKind.Set, "state.Extensions", "{sequence: StreetName}")]);

        Assert.True(replaced.IsSuccess, replaced.Error);
        var normalized = VsirSourceFormatter.FormatAfterMutation(replaced.Source!).Replace("\r\n", "\n");

        Assert.Contains("Extensions:\n    sequence: StreetName", normalized);
        Assert.Contains("Ext:\n    type:\n      sequence: string", normalized);
        Assert.Contains("input:\n  Ext:\n    sequence: string", normalized);
    }

    [Fact]
    public void Add_is_reserved_for_collection_semantics()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            shape: product
            classification: value-object
            state:
              Commune: Commune
            representation:
              CommuneId: string
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Add, "state.Extensions", "{sequence: StreetExtension}")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE044:", result.Error);
    }

    [Fact]
    public void Structured_semantic_fields_fail_closed_on_arbitrary_mapping_shapes()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            shape: product
            classification: value-object
            state:
              Commune: Commune
            representation:
              CommuneId: string
            """;

        var multipleConstructors = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "state.Extensions", "{sequence: StreetExtension, optional: StreetExtension}")]);

        Assert.False(multipleConstructors.IsSuccess);
        Assert.StartsWith("UPDATE043:", multipleConstructors.Error);

        var expandedWithUnauthorizedSource = VsirMutationPipeline.Apply(
            source,
            [new(VsirMutationKind.Set, "representation.Ext", "{type: {sequence: string}, mapping: {invent: state.Commune}}")]);

        Assert.False(expandedWithUnauthorizedSource.IsSuccess);
        Assert.StartsWith("UPDATE043:", expandedWithUnauthorizedSource.Error);
    }

    [Fact]
    public void Location_can_be_authored_step_by_step_only_through_discovered_application_surfaces()
    {
        var created = VsirTemplate.Create(
            "Location",
            "domain-type",
            "product",
            "value-object");

        Assert.True(created.IsSuccess, created.Error);
        var current = created.Source!;

        var initialFrontier = VsirMutationPipeline.Discover(current, out var initialError);
        Assert.Null(initialError);
        Assert.Contains(initialFrontier, item => item.Path == "state" && item.Operations.Contains(VsirMutationKind.Set));
        Assert.Contains(initialFrontier, item => item.Path == "representation" && item.Operations.Contains(VsirMutationKind.Set));
        Assert.Contains(initialFrontier, item => item.Path == "traits" && item.Operations.Contains(VsirMutationKind.Add));

        var stateAffordance = Assert.Single(initialFrontier, item => item.Path == "state");
        Assert.Contains(
            "vslices update vsir Location --set \"state.<property>=<semantic-field-declaration>\"",
            VsirCommandTemplates.For("Location", stateAffordance));

        var core = VsirMutationPipeline.Apply(
            current,
            [
                new(VsirMutationKind.Set, "state.Commune", "Commune"),
                new(VsirMutationKind.Set, "state.Street", "StreetName"),
                new(VsirMutationKind.Set, "state.Extensions", "{sequence: StreetExtension}"),
                new(VsirMutationKind.Set, "state.Region", "Region"),
                new(VsirMutationKind.Set, "state.Province", "Province"),
                new(VsirMutationKind.Set, "representation.CommuneId", "string"),
                new(VsirMutationKind.Set, "representation.Street", "string"),
                new(VsirMutationKind.Set, "representation.Ext", "{type: {sequence: string}}"),
                new(VsirMutationKind.Add, "traits", "transform")
            ]);

        Assert.True(core.IsSuccess, core.Error);
        current = core.Source!;

        var transformFrontier = VsirMutationPipeline.Discover(current, out var transformError);
        Assert.Null(transformError);
        Assert.Contains(transformFrontier, item => item.Path == "input" && item.Status == VsirFrontierStatus.Required && item.Operations.Contains(VsirMutationKind.Set));
        Assert.Contains(transformFrontier, item => item.Path == "construction" && item.Status == VsirFrontierStatus.Required);
        Assert.Contains(transformFrontier, item => item.Path == "state.Region.from" && item.Operations.Count == 1 && item.Operations.Contains(VsirMutationKind.Set));
        Assert.Contains(transformFrontier, item => item.Path == "state.Province.from" && item.Operations.Count == 1 && item.Operations.Contains(VsirMutationKind.Set));
        Assert.Contains(transformFrontier, item => item.Path == "representation.CommuneId.mapping" && item.Operations.Count == 1 && item.Operations.Contains(VsirMutationKind.Set));
        Assert.Contains(transformFrontier, item => item.Path == "representation.Street.mapping" && item.Operations.Count == 1 && item.Operations.Contains(VsirMutationKind.Set));
        Assert.Contains(transformFrontier, item => item.Path == "representation.Ext.mapping" && item.Operations.Count == 1 && item.Operations.Contains(VsirMutationKind.Set));

        var inputAffordance = Assert.Single(transformFrontier, item => item.Path == "input");
        var inputCommands = VsirCommandTemplates.For("Location", inputAffordance);
        Assert.Contains("vslices update vsir Location --set \"input.<property>=<semantic-field-declaration>\"", inputCommands);
        Assert.Contains("vslices update vsir Location --set \"input=<scalar-semantic-type>\"", inputCommands);

        var semanticRelations = VsirMutationPipeline.Apply(
            current,
            [
                new(VsirMutationKind.Set, "state.Region.from", "state.Commune.InProvince.InRegion"),
                new(VsirMutationKind.Set, "state.Province.from", "state.Commune.InProvince"),
                new(VsirMutationKind.Set, "input.CommuneId", "CommuneId"),
                new(VsirMutationKind.Set, "input.Street", "string"),
                new(VsirMutationKind.Set, "input.Ext", "{sequence: string}"),
                new(VsirMutationKind.Set, "representation.CommuneId.mapping", "{select: {source: {represent: state.Commune}, field: Id}}"),
                new(VsirMutationKind.Set, "representation.Street.mapping", "{select: {source: {represent: state.Street}, field: Value}}"),
                new(VsirMutationKind.Set, "representation.Ext.mapping", "{map: {source: state.Extensions, bind: extension, value: {select: {source: {represent: extension}, field: Value}}}}")
            ]);

        Assert.True(semanticRelations.IsSuccess, semanticRelations.Error);
        current = semanticRelations.Source!;

        var constructionFrontier = VsirMutationPipeline.Discover(current, out var constructionError);
        Assert.Null(constructionError);
        Assert.DoesNotContain(constructionFrontier, item => item.Path == "input");
        Assert.Contains(constructionFrontier, item => item.Path == "construction" && item.Operations.Contains(VsirMutationKind.Set));
        Assert.DoesNotContain(constructionFrontier, item => item.Path == "representation.CommuneId.from");
        Assert.DoesNotContain(constructionFrontier, item => item.Path == "representation.Street.from");
        Assert.DoesNotContain(constructionFrontier, item => item.Path == "representation.Ext.from");

        var construction = """
            [{resolve: {source: Commune, id: input.CommuneId, as: commune, failure: {message: 'Debes especificar una comuna existente'}}}, {apply: {over: StreetName, input: {Value: input.Street}, as: street}}, {apply: {over: StreetExtension, input: {source: input.Ext, map: {Value: item}}, as: extensions}}, {refine: {state: {Commune: commune, Street: street, Extensions: extensions}}}]
            """;

        var completed = VsirMutationPipeline.Apply(
            current,
            [new(VsirMutationKind.Set, "construction", construction)]);

        Assert.True(completed.IsSuccess, completed.Error);
        current = completed.Source!;

        var completeFrontier = VsirMutationPipeline.Discover(current, out var completeError);
        Assert.Null(completeError);
        Assert.DoesNotContain(completeFrontier, item => item.Path == "input");
        Assert.DoesNotContain(completeFrontier, item => item.Path == "construction");

        var normalized = VsirSourceFormatter.FormatAfterMutation(current).Replace("\r\n", "\n");
        Assert.Contains("Extensions:\n    sequence: StreetExtension", normalized);
        Assert.Contains("type:\n      sequence: string", normalized);
        Assert.Contains("input:\n  CommuneId: CommuneId\n  Street: string\n  Ext:\n    sequence: string", normalized);
        Assert.Contains("from: state.Commune.InProvince.InRegion", normalized);
        Assert.Contains("select:", normalized);
        Assert.Contains("represent: state.Commune", normalized);
        Assert.Contains("map:\n        source: state.Extensions", normalized);
        Assert.Contains("resolve:", normalized);
        Assert.Contains("over: StreetExtension", normalized);
        Assert.Contains("Extensions: extensions", normalized);
    }
}
