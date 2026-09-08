using VSlices.Vsir;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirDiagnosticLocationTests
{
    private const string Source = """
        vsir: 0.1
        kind: domain-type
        name: Name
        shape: sum
        classification: value-object
        state: {}
        representation: {}
        variants:
          FullName: {}
        """;

    [Fact]
    public void Existing_invalid_scalar_points_to_semantic_node_and_value_location()
    {
        var result = VsirParser.Parse(Source);
        var diagnostic = Assert.Single(result.Diagnostics, value => value.Code == "VSIR203");

        Assert.Equal("shape", diagnostic.SemanticPath);
        Assert.NotNull(diagnostic.Source);
        Assert.Equal(4, diagnostic.Source!.Line);
        Assert.Equal(8, diagnostic.Source.Column);
    }

    [Fact]
    public void Unsupported_root_semantic_points_to_unsupported_key_location()
    {
        var result = VsirParser.Parse(Source);
        var diagnostic = Assert.Single(result.Diagnostics, value =>
            value.Code == "VSIR104" && value.Message.Contains("variants", StringComparison.Ordinal));

        Assert.Equal("variants", diagnostic.SemanticPath);
        Assert.NotNull(diagnostic.Source);
        Assert.Equal(8, diagnostic.Source!.Line);
        Assert.Equal(1, diagnostic.Source.Column);
    }

    [Fact]
    public void Missing_required_node_reports_semantic_path_without_fabricating_coordinates()
    {
        var result = VsirParser.Parse(Source);
        var diagnostic = Assert.Single(result.Diagnostics, value => value.Code == "VSIR111");

        Assert.Equal("input", diagnostic.SemanticPath);
        Assert.Null(diagnostic.Source);
    }
}
