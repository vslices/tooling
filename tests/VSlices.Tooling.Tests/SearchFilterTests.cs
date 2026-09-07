namespace VSlices.Tooling.Tests;

public sealed class SearchFilterTests
{
    private const string Tagged = """
        vsir: 0.1
        kind: domain-type
        name: StreetName
        tags: [addressing, identity-service]
        classification: value-object
        """;

    [Fact]
    public void Contains_matches_sequence_member()
    {
        var parsed = SearchFilter.Parse("tags:contains:identity-service");

        Assert.Null(parsed.Error);
        Assert.True(parsed.Filter!.Matches(Tagged));
    }

    [Fact]
    public void Equals_matches_scalar_property()
    {
        var parsed = SearchFilter.Parse("classification:equals:value-object");

        Assert.Null(parsed.Error);
        Assert.True(parsed.Filter!.Matches(Tagged));
    }

    [Fact]
    public void Missing_property_does_not_match()
    {
        var parsed = SearchFilter.Parse("tags:contains:accounts");

        Assert.Null(parsed.Error);
        Assert.False(parsed.Filter!.Matches("vsir: 0.1\nname: Example\n"));
    }

    [Fact]
    public void Filter_value_may_contain_colons()
    {
        var parsed = SearchFilter.Parse("name:contains:Street:Name");

        Assert.Null(parsed.Error);
        Assert.Equal("Street:Name", parsed.Filter!.Value);
    }

    [Fact]
    public void Unsupported_operator_fails_closed()
    {
        var parsed = SearchFilter.Parse("tags:starts-with:identity");

        Assert.Null(parsed.Filter);
        Assert.StartsWith("SEARCH002:", parsed.Error);
    }
}
