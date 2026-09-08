using VSlices.Vsir;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirDiagnosticLocationTests
{
    [Fact]
    public void Existing_invalid_scalar_points_to_semantic_node_and_value_location()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: scalar
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            """;

        var result = VsirParser.Parse(source);
        var diagnostic = Assert.Single(result.Diagnostics, value => value.Code == "VSIR203");

        Assert.Equal("shape", diagnostic.SemanticPath);
        Assert.NotNull(diagnostic.Source);
        Assert.Equal(5, diagnostic.Source!.Line);
        Assert.Equal(8, diagnostic.Source.Column);
    }

    [Fact]
    public void Unsupported_root_semantic_points_to_unsupported_key_location()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input:
              Value: string
            imaginary-root: true
            """;

        var result = VsirParser.Parse(source);
        var diagnostic = Assert.Single(result.Diagnostics, value =>
            value.Code == "VSIR104" && value.Message.Contains("imaginary-root", StringComparison.Ordinal));

        Assert.Equal("imaginary-root", diagnostic.SemanticPath);
        Assert.NotNull(diagnostic.Source);
        Assert.Equal(14, diagnostic.Source!.Line);
        Assert.Equal(1, diagnostic.Source.Column);
    }

    [Fact]
    public void Missing_required_node_reports_semantic_path_without_fabricating_coordinates()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: Example
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            """;

        var result = VsirParser.Parse(source);
        var diagnostic = Assert.Single(result.Diagnostics, value => value.Code == "VSIR111");

        Assert.Equal("input", diagnostic.SemanticPath);
        Assert.Null(diagnostic.Source);
    }
}
