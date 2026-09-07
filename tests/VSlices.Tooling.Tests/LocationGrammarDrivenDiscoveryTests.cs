namespace VSlices.Tooling.Tests;

public sealed class LocationGrammarDrivenDiscoveryTests
{
    [Fact]
    public void Location_representation_mappings_expose_expression_grammar_and_executable_templates()
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
              Street: StreetName
              Extensions:
                sequence: StreetExtension
              Region:
                type: Region
                from: state.Commune.InProvince.InRegion
              Province:
                type: Province
                from: state.Commune.InProvince
            representation:
              CommuneId: string
              Street: string
              Ext:
                type:
                  sequence: string
            input:
              CommuneId: CommuneId
              Street: string
              Ext:
                sequence: string
            """;

        var frontier = VsirMutationPipeline.Discover(source, out var error);

        Assert.Null(error);

        foreach (var path in new[]
                 {
                     "representation.CommuneId.mapping",
                     "representation.Street.mapping",
                     "representation.Ext.mapping"
                 })
        {
            var affordance = Assert.Single(frontier, item => item.Path == path);
            Assert.Equal("expression", VsirGrammarDiscovery.ValueKind(affordance));
            Assert.Single(affordance.Operations);
            Assert.Contains(VsirMutationKind.Set, affordance.Operations);
            Assert.Contains(
                $"vslices update vsir Location --set \"{path}=<expression>\"",
                VsirCommandTemplates.For("Location", affordance));

            var grammar = Assert.IsType<VsirValueGrammar>(VsirGrammarDiscovery.For(affordance));
            Assert.Equal("expression", grammar.RootKind);
            Assert.Contains(grammar.Forms, form => form.Name == "represent");
            Assert.Contains(grammar.Forms, form => form.Name == "select");
            Assert.Contains(grammar.Forms, form => form.Name == "map");
        }
    }

    [Fact]
    public void Select_discovers_expression_source_so_represent_remains_an_explicit_composition()
    {
        var contract = MappingContract();
        var grammar = Assert.IsType<VsirValueGrammar>(VsirGrammarDiscovery.For(contract));

        var select = Assert.Single(grammar.Forms, form => form.Name == "select");
        Assert.Equal("{select: {source: <expression>, field: <field>}}", select.Template);
        Assert.Contains(select.Slots, slot => slot.Name == "source" && slot.ValueKind == "expression");
        Assert.Contains(select.Slots, slot => slot.Name == "field" && slot.ValueKind == "field");

        var represent = Assert.Single(grammar.Forms, form => form.Name == "represent");
        Assert.Equal("{represent: <semantic-reference>}", represent.Template);
        Assert.Contains(represent.Slots, slot => slot.Name == "value" && slot.ValueKind == "semantic-reference");
    }

    [Fact]
    public void Map_discovers_a_semantic_source_and_recursive_value_expression()
    {
        var contract = MappingContract();
        var grammar = Assert.IsType<VsirValueGrammar>(VsirGrammarDiscovery.For(contract));

        var map = Assert.Single(grammar.Forms, form => form.Name == "map");
        Assert.Equal(
            "{map: {source: <semantic-reference>, bind: <name>, value: <expression>}}",
            map.Template);
        Assert.Contains(map.Slots, slot => slot.Name == "source" && slot.ValueKind == "semantic-reference");
        Assert.Contains(map.Slots, slot => slot.Name == "bind" && slot.ValueKind == "name");
        Assert.Contains(map.Slots, slot => slot.Name == "value" && slot.ValueKind == "expression");
    }

    [Fact]
    public void Location_mapping_shapes_advertised_by_grammar_are_accepted_by_update()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: Location
            shape: product
            classification: value-object
            state:
              Commune: Commune
              Street: StreetName
              Extensions:
                sequence: StreetExtension
            representation:
              CommuneId: string
              Street: string
              Ext:
                type:
                  sequence: string
            """;

        var result = VsirMutationPipeline.Apply(
            source,
            [
                new(
                    VsirMutationKind.Set,
                    "representation.CommuneId.mapping",
                    "{select: {source: {represent: state.Commune}, field: Id}}"),
                new(
                    VsirMutationKind.Set,
                    "representation.Street.mapping",
                    "{select: {source: {represent: state.Street}, field: Value}}"),
                new(
                    VsirMutationKind.Set,
                    "representation.Ext.mapping",
                    "{map: {source: state.Extensions, bind: extension, value: {select: {source: {represent: extension}, field: Value}}}}")
            ]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("represent: state.Commune", result.Source);
        Assert.Contains("represent: state.Street", result.Source);
        Assert.Contains("source: state.Extensions", result.Source);
        Assert.Contains("represent: extension", result.Source);
    }

    private static VsirPathContract MappingContract() =>
        new(
            "representation.Street.mapping",
            "mapping",
            VsirFrontierStatus.Optional,
            "test",
            new HashSet<VsirMutationKind> { VsirMutationKind.Set });
}
