using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class AggregateRootSumLoweringTests
{
    private const string SrvIdentitySource = """
        vsir: 0.1
        kind: domain-type
        name: SrvIdentity
        shape: sum
        classification: aggregate-root

        identity:
          type: SrvIdentityId
          from: state.Document

        state:
          Document: Rut
          Address:
            optional: Location

        representation:
          Id:
            type: string
            mapping:
              stringify: identity
          Location:
            type:
              optional: Location.Repr
            mapping:
              map:
                source: state.Address
                bind: location
                value:
                  represent: location

        variants:
          NaturalIdentity:
            traits: [transform]
            state:
              Name: FullName
            representation:
              Names:
                type: string
                from: state.Name.Names
              FirstSurname:
                type: string
                from: state.Name.FirstSurname
              SecondSurname:
                type:
                  optional: string
                from: state.Name.SecondSurname
            input:
              Document: Rut
              Name: FullName
              Address:
                optional: Location
            construction:
              - refine:
                  state:
                    Document: input.Document
                    Address: input.Address
                    Name: input.Name

          LegalIdentity:
            traits: [transform]
            state:
              Name: CompanyName
            representation:
              Name:
                type: string
                from: state.Name.Value
            input:
              Document: Rut
              Name: CompanyName
              Address:
                optional: Location
            construction:
              - refine:
                  state:
                    Document: input.Document
                    Address: input.Address
                    Name: input.Name
        """;

    [Fact]
    public void SrvIdentity_sum_parses_shared_state_identity_and_variant_projections()
    {
        var parsed = VsirParser.Parse(SrvIdentitySource);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var document = parsed.Document!;
        Assert.Equal("aggregate-root", document.Classification);
        Assert.Equal(2, document.State.Fields.Count);
        Assert.Equal(2, document.Representation.Fields.Count);
        Assert.NotNull(document.Identity);
        Assert.Equal(new NamedVsirType("SrvIdentityId"), document.Identity!.Type);
        Assert.Equal("state.Document", document.Identity.From);
        Assert.Equal(2, document.Variants!.Count);

        var natural = Assert.Single(document.Variants, variant => variant.Name == "NaturalIdentity");
        Assert.Equal("state.Name.Names", Assert.Single(natural.Representation.Fields, field => field.Name == "Names").From);
        Assert.Equal(3, natural.Construction.Steps.OfType<RefineStep>().Count());
    }

    [Fact]
    public void SrvIdentity_sum_lowers_shared_aggregate_contract_and_representation()
    {
        var parsed = VsirParser.Parse(SrvIdentitySource);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var lowered = CSharpSumDomainTypeLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Identities.Domain.Aggregates", LoadRules()));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("public abstract class SrvIdentity(Rut document, Option<Location> address)", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("DomainType<SrvIdentity, SrvIdentity.Repr>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("AggregateRoot<SrvIdentity, SrvIdentityId>", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public abstract record Repr(string Id, Option<Location.Repr> Location);", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public SrvIdentityId Id { get; } = SrvIdentityId.New(document);", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public sealed class NaturalIdentity", lowered.Source, StringComparison.Ordinal);
        Assert.Contains(": base(document, address)", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Id.ToString()", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Address.Map(location => location.To())", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Name.Names", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Name.FirstSurname", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Name.SecondSurname", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegalIdentity", lowered.Source, StringComparison.Ordinal);
        Assert.Contains("Name.Value", lowered.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Aggregate_root_sum_fails_closed_without_identity_target_rule()
    {
        var parsed = VsirParser.Parse(SrvIdentitySource);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var lowered = CSharpSumDomainTypeLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Identities.Domain.Aggregates", LoadRules(includeIdentity: false)));

        Assert.False(lowered.IsSuccess);
        Assert.Contains(lowered.Diagnostics, diagnostic => diagnostic.Code == "CSL110");
    }

    private static CSharpLoweringRuleSet LoadRules(bool includeIdentity = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "vslices-aggregate-sum-" + Guid.NewGuid().ToString("N"));
        var csharp = Path.Combine(root, "csharp");
        Directory.CreateDirectory(csharp);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "manifest.yaml"),
                """
                targets:
                  csharp:
                    rules:
                      - csharp/rules.yaml
                """);

            var identityRule = includeIdentity
                ? """

                  - node: identity.construct
                    mode: deterministic
                    renderer: expression
                    bindings: [type, value]
                    template: "{type}.New({value})"
                """
                : string.Empty;

            File.WriteAllText(
                Path.Combine(csharp, "rules.yaml"),
                """
                rules:
                  - node: type.optional
                    mode: deterministic
                    renderer: type
                    bindings: [value]
                    template: "Option<{value}>"

                  - node: projection.stringify
                    mode: deterministic
                    renderer: expression
                    bindings: [value]
                    template: "{value}.ToString()"

                  - node: projection.represent
                    mode: deterministic
                    renderer: expression
                    bindings: [value]
                    template: "{value}.To()"

                  - node: projection.map
                    mode: deterministic
                    renderer: expression
                    bindings: [source, bind, value]
                    template: "{source}.Map({bind} => {value})"
                """ + identityRule);

            var loaded = CSharpLoweringRuleSet.Load(root);
            Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));
            return loaded.RuleSet!;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
