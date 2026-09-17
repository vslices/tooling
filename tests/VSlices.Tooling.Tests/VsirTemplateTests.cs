namespace VSlices.Tooling.Tests;

public sealed class VsirTemplateTests
{
    [Fact]
    public void Create_emits_only_progressive_identity()
    {
        var result = VsirTemplate.Create("StreetName");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "vsir: 0.1\n" +
            "name: StreetName\n",
            result.Source!.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Create_rejects_missing_name()
    {
        var result = VsirTemplate.Create("   ");

        Assert.False(result.IsSuccess);
        Assert.StartsWith("NEW001:", result.Error);
    }
}
