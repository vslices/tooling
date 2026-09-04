using VSlices.Vsir;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirParserSemanticConservationTests
{
    [Fact]
    public void Unsupported_root_semantics_are_rejected_instead_of_silently_discarded()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: TicketCodeLike
            classification: value-object
            shape: product
            traits: [transform]

            state:
              Value: string

            representation:
              Value: string

            construction:
              input:
                Value: string
              steps:
                - ensure:
                    condition:
                      intrinsic: non-empty
                      value: input.Value
                    failure:
                      message: Debes especificar el codigo

            equality:
              intrinsic: ordinal-equals
              by: state.Value
            """;

        var parsed = VsirParser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic =>
            diagnostic.Code == "VSIR104" && diagnostic.Message.Contains("equality", StringComparison.Ordinal));
    }
}
