using VSlices.Vsir;

namespace VSlices.Tooling.Tests;

public sealed class DiagnosticLocationRenderingTests
{
    [Fact]
    public void Diagnostic_header_includes_semantic_node_and_line_column_when_known()
    {
        var diagnostic = new VsirDiagnostic(
            "VSIR203",
            "Only shape 'product' is supported.",
            SemanticPath: "shape",
            Source: new VsirSourceSpan(4, 8, 4, 11));

        Assert.Equal(
            "VSIR203 [shape @ 4:8]",
            CommandInfrastructure.DiagnosticHeader(diagnostic));
    }

    [Fact]
    public void Missing_node_keeps_semantic_path_without_fake_coordinates()
    {
        var diagnostic = new VsirDiagnostic(
            "VSIR111",
            "Transform semantics require root input.",
            SemanticPath: "input");

        Assert.Equal(
            "VSIR111 [input]",
            CommandInfrastructure.DiagnosticHeader(diagnostic));
    }
}
