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
    public void Create_with_classification_emits_only_semantic_VSIR_fields()
    {
        var result = VsirTemplate.Create(
            name: "StreetName",
            kind: "domain-type",
            classification: "value-object");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "vsir: 0.1\n" +
            "kind: domain-type\n" +
            "name: StreetName\n" +
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
