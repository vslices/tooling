using VSlices.Vsir;

namespace VSlices.Tooling.Tests;

public sealed class VsirMetadataAuthoringTests
{
    private const string Named = """
        vsir: 0.1
        name: StreetName
        """;

    [Fact]
    public void Tags_contract_is_always_collection_valued_and_semantically_inert()
    {
        var contract = VsirMetadataAuthoring.TagsContract;

        Assert.Equal("tags", contract.Path);
        Assert.Equal("set<string>", contract.ValueKind);
        Assert.Equal(VsirFrontierStatus.Optional, contract.Status);
        Assert.Contains(VsirMutationKind.Add, contract.Operations);
        Assert.Contains(VsirMutationKind.Remove, contract.Operations);
        Assert.Contains(VsirMutationKind.Set, contract.Operations);
        Assert.Contains("no domain-semantic authority", contract.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public void Tags_can_be_added_before_semantic_kind_is_known()
    {
        var result = VsirMetadataAuthoring.Apply(
            Named,
            [new(VsirMutationKind.Add, "tags", "ticket,identity")]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("tags: [ticket, identity]", result.Source);
    }

    [Fact]
    public void Tags_support_set_add_and_remove_without_touching_semantic_content()
    {
        var set = VsirMetadataAuthoring.Apply(
            Named,
            [new(VsirMutationKind.Set, "tags", "ticket,serviu")]);
        Assert.True(set.IsSuccess, set.Error);

        var add = VsirMetadataAuthoring.Apply(
            set.Source!,
            [new(VsirMutationKind.Add, "tags", "migration")]);
        Assert.True(add.IsSuccess, add.Error);
        Assert.Contains("tags: [ticket, serviu, migration]", add.Source);

        var remove = VsirMetadataAuthoring.Apply(
            add.Source!,
            [new(VsirMutationKind.Remove, "tags", "serviu")]);
        Assert.True(remove.IsSuccess, remove.Error);
        Assert.Contains("tags: [ticket, migration]", remove.Source);
        Assert.Contains("name: StreetName", remove.Source);
    }

    [Fact]
    public void Duplicate_hand_authored_tags_fail_closed()
    {
        var source = """
            vsir: 0.1
            name: StreetName
            tags: [ticket, ticket]
            """;

        var result = VsirMetadataAuthoring.Apply(
            source,
            [new(VsirMutationKind.Add, "tags", "serviu")]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("UPDATE047:", result.Error);
    }

    [Fact]
    public void Tags_remove_template_targets_a_member_not_the_whole_assertion()
    {
        var commands = VsirCommandTemplates.For("StreetName", VsirMetadataAuthoring.TagsContract);

        Assert.Contains("vslices update vsir StreetName --add \"tags=<value>\"", commands);
        Assert.Contains("vslices update vsir StreetName --remove \"tags=<value>\"", commands);
    }

    [Fact]
    public void Semantic_parser_ignores_valid_tags_metadata()
    {
        var semantic = """
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

        var tagged = semantic + "tags: [ticket, identity]\n";

        var baseline = VsirParser.Parse(semantic);
        var withTags = VsirParser.Parse(tagged);

        Assert.Equal(
            baseline.Diagnostics.Select(diagnostic => diagnostic.Code),
            withTags.Diagnostics.Select(diagnostic => diagnostic.Code));
        Assert.DoesNotContain(withTags.Diagnostics, diagnostic => diagnostic.Code is "VSIR150" or "VSIR151");
    }

    [Fact]
    public void Semantic_parser_rejects_malformed_tags_metadata_explicitly()
    {
        var source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            tags: ticket
            """;

        var result = VsirParser.Parse(source);

        Assert.Null(result.Document);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "VSIR150");
    }
}
