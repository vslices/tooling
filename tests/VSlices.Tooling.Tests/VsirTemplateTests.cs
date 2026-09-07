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
    public void Create_emits_known_domain_type_context()
    {
        var result = VsirTemplate.Create(
            name: "SrvIdentityId",
            kind: "domain-type",
            classification: "identifier",
            shape: "product",
            traits: ["refined", "transform"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "vsir: 0.1\n" +
            "kind: domain-type\n" +
            "name: SrvIdentityId\n" +
            "classification: identifier\n" +
            "shape: product\n" +
            "traits: [refined, transform]\n",
            result.Source!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Create_rejects_kind_specific_choices_before_kind_is_known()
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

    [Theory]
    [InlineData("identifier", "identifier")]
    [InlineData("maintained", "maintained")]
    [InlineData("aggregate-root", "aggregate-root")]
    [InlineData("aggregate-root", "entity")]
    public void Create_rejects_traits_inferred_from_classification(
        string classification,
        string trait)
    {
        var result = VsirTemplate.Create(
            name: "Example",
            kind: "domain-type",
            classification: classification,
            traits: [trait]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("NEW007:", result.Error);
    }
}
