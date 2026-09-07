namespace VSlices.Tooling.Tests;

public sealed class VsirTemplateTests
{
    [Fact]
    public void Create_with_only_name_emits_progressive_named_artifact()
    {
        var result = VsirTemplate.Create(name: "StreetName");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "vsir: 0.1\n" +
            "name: StreetName\n",
            result.Source!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Create_with_tags_does_not_require_kind()
    {
        var result = VsirTemplate.Create(
            name: "StreetName",
            tags: ["addressing", "street"]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "vsir: 0.1\n" +
            "name: StreetName\n" +
            "tags: ['addressing', 'street']\n",
            result.Source!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Create_with_kind_emits_kind_bound_artifact()
    {
        var result = VsirTemplate.Create(
            name: "StreetName",
            kind: "domain-type");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "vsir: 0.1\n" +
            "kind: domain-type\n" +
            "name: StreetName\n",
            result.Source!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Create_with_classification_and_tags_emits_currently_implemented_semantics()
    {
        var result = VsirTemplate.Create(
            name: "StreetName",
            kind: "domain-type",
            classification: "value-object",
            tags: ["addressing"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "vsir: 0.1\n" +
            "kind: domain-type\n" +
            "name: StreetName\n" +
            "tags: ['addressing']\n" +
            "classification: value-object\n",
            result.Source!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Create_rejects_classification_before_kind_is_known()
    {
        var result = VsirTemplate.Create(
            name: "StreetName",
            classification: "value-object");

        Assert.False(result.IsSuccess);
        Assert.StartsWith("NEW002:", result.Error);
    }

    [Fact]
    public void Create_rejects_classification_not_owned_by_kind()
    {
        var result = VsirTemplate.Create(
            name: "Example",
            kind: "domain-type",
            classification: "feature");

        Assert.False(result.IsSuccess);
        Assert.StartsWith("NEW004:", result.Error);
    }

    [Fact]
    public void Create_rejects_duplicate_tags()
    {
        var result = VsirTemplate.Create(
            name: "StreetName",
            tags: ["addressing", "addressing"]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("NEW008:", result.Error);
    }

    [Fact]
    public void Entity_is_available_as_a_domain_type_classification()
    {
        var result = VsirTemplate.Create(
            name: "Customer",
            kind: "domain-type",
            classification: "entity");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains("classification: entity", result.Source);
    }
}
