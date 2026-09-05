using VSlices.Vsir;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class VsirValidationContextTests
{
    [Fact]
    public void Semantic_extension_authority_is_external_to_the_parsed_document()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: ContextProbe
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
                - normalize:
                    target: input.Value
                    intrinsic: project-normalize
            """;

        var context = new VsirValidationContext(
            new VsirSemanticExtensions(
                new HashSet<string>(["project-normalize"], StringComparer.Ordinal)));

        var parsed = VsirParser.Parse(source, context);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Contains(
            DomainTypeValidator.Validate(parsed.Document!),
            diagnostic => diagnostic.Code == "VSIR221");
        Assert.DoesNotContain(
            DomainTypeValidator.Validate(parsed.Document!, context),
            diagnostic => diagnostic.Code == "VSIR221");
    }
}
