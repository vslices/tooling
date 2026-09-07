namespace VSlices.Tooling.Tests;

public sealed class RepresentationTemplateTests
{
    [Fact]
    public void Create_emits_minimum_document_identity()
    {
        var result = RepresentationTemplate.Create(
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
        var result = RepresentationTemplate.Create(
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
    public void Create_rejects_classification_not_owned_by_kind()
    {
        var result = RepresentationTemplate.Create(
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
        var result = RepresentationTemplate.Create(
            name: "Example",
            kind: "domain-type",
            classification: classification,
            traits: [trait]);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("NEW007:", result.Error);
    }
}
