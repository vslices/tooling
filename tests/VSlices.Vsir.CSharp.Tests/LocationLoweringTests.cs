using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class LocationLoweringTests
{
    private static string RulesetPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ruleset");

    [Fact]
    public void Normalized_Location_preserves_composed_representation_expressions()
    {
        var parsed = VsirLanguageParser.Parse(LocationSource);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var document = parsed.Document!;

        var commune = Assert.IsType<SelectProjection>(document.RepresentationMapping!.Fields["CommuneId"]);
        var representedCommune = Assert.IsType<RepresentProjection>(commune.Source);
        Assert.Equal("state.Commune", Assert.IsType<ReferenceProjection>(representedCommune.Value).Value);
        Assert.Equal("Id", commune.Field);

        var street = Assert.IsType<SelectProjection>(document.RepresentationMapping.Fields["Street"]);
        Assert.IsType<RepresentProjection>(street.Source);

        var extensions = Assert.IsType<MapProjection>(document.RepresentationMapping.Fields["Ext"]);
        Assert.Equal("state.Extensions", Assert.IsType<ReferenceProjection>(extensions.Source).Value);
        Assert.Equal("extension", extensions.Bind);
        var selected = Assert.IsType<SelectProjection>(extensions.Value);
        Assert.IsType<RepresentProjection>(selected.Source);

        Assert.Equal("state.Commune.InProvince.InRegion", document.State.Fields.Single(x => x.Name == "Region").From);
        Assert.Equal("state.Commune.InProvince", document.State.Fields.Single(x => x.Name == "Province").From);
        Assert.Contains(document.Construction.Steps, step => step is ResolveStep);
        Assert.Equal(2, document.Construction.Steps.Count(step => step is ApplyStep));
    }

    [Fact]
    public void Normalized_Location_lowers_the_same_semantics_authored_by_the_CLI()
    {
        var parsed = VsirLanguageParser.Parse(LocationSource);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var rules = LoadLocationRules();

        var lowered = CSharpLanguageLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Identities.Domain.Entities", rules));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("record struct Repr(string CommuneId, string Street, Seq<string> Ext)", lowered.Source);
        Assert.Contains("record struct Input(CommuneId CommuneId, string Street, Seq<string> Ext)", lowered.Source);
        Assert.Contains("private readonly Commune _commune;", lowered.Source);
        Assert.Contains("private readonly StreetName _street;", lowered.Source);
        Assert.Contains("private readonly Seq<StreetExtension> _extensions;", lowered.Source);
        Assert.DoesNotContain("private readonly Region _region;", lowered.Source);
        Assert.DoesNotContain("private readonly Province _province;", lowered.Source);
        Assert.Contains("public Region Region =>", lowered.Source);
        Assert.Contains("_commune.InProvince.InRegion", lowered.Source);
        Assert.Contains("public Province Province =>", lowered.Source);
        Assert.Contains("_commune.InProvince", lowered.Source);

        Assert.Contains("exists((Commune value) => value.Id == input.CommuneId)", lowered.Source);
        Assert.Contains("Apply(StreetName.Invariants", lowered.Source);
        Assert.Contains("ApplySeq(StreetExtension.Invariants", lowered.Source);
        Assert.Contains("first((Commune value) => value.Id == input.CommuneId)", lowered.Source);
        Assert.Contains("StreetName.New(new StreetName.Input(input.Street))", lowered.Source);
        Assert.Contains("StreetExtension.New(input.Ext.Map(item => new StreetExtension.Input(item)))", lowered.Source);

        Assert.Contains("_commune.To().Id", lowered.Source);
        Assert.Contains("_street.To().Value", lowered.Source);
        Assert.Contains("_extensions.Map(extension => extension.To().Value)", lowered.Source);
    }

    [Fact]
    public void Select_does_not_insert_represent_implicitly()
    {
        var source = LocationSource.Replace(
            "source: { represent: state.Street }",
            "source: state.Street",
            StringComparison.Ordinal);
        var parsed = VsirLanguageParser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var lowered = CSharpLanguageLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Identities.Domain.Entities", LoadLocationRules()));

        Assert.True(lowered.IsSuccess, string.Join(Environment.NewLine, lowered.Diagnostics));
        Assert.Contains("_street.Value", lowered.Source);
        Assert.DoesNotContain("_street.To().Value", lowered.Source);
    }

    [Fact]
    public void Missing_projection_rule_fails_closed()
    {
        var parsed = VsirLanguageParser.Parse(LocationSource);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var rules = LoadLocationRules(includeRepresent: false);
        var lowered = CSharpLanguageLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext("Identities.Domain.Entities", rules));

        Assert.False(lowered.IsSuccess);
        Assert.Contains(lowered.Diagnostics, diagnostic =>
            diagnostic.Code == "CSL060" &&
            diagnostic.Message.Contains("projection.represent", StringComparison.Ordinal));
    }

    private static CSharpLoweringRuleSet LoadLocationRules(bool includeRepresent = true)
    {
        var additional = new List<CSharpLoweringRule>
        {
            new("type.sequence", "deterministic", "type", ["value"], "Seq<{value}>"),
            new("projection.select", "deterministic", "expression", ["source", "field"], "{source}.{field}"),
            new("projection.map", "deterministic", "expression", ["source", "bind", "value"], "{source}.Map({bind} => {value})"),
            new("construction.resolve.condition", "deterministic", "expression", ["source", "id"], "exists(({source} value) => value.Id == {id})"),
            new("construction.resolve.value", "deterministic", "expression", ["source", "id"], "first(({source} value) => value.Id == {id})"),
            new("construction.apply.input", "deterministic", "expression", ["over", "arguments"], "new {over}.Input({arguments})"),
            new("construction.apply.value", "deterministic", "expression", ["over", "input"], "{over}.New({input})"),
            new("construction.apply-sequence.input", "deterministic", "expression", ["source", "bind", "over", "arguments"], "{source}.Map({bind} => new {over}.Input({arguments}))"),
            new("construction.apply-sequence.value", "deterministic", "expression", ["over", "input"], "{over}.New({input})")
        };
        if (includeRepresent)
            additional.Add(new("projection.represent", "deterministic", "expression", ["value"], "{value}.To()"));

        var loaded = CSharpLoweringRuleSet.Load(RulesetPath, additional);
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));
        return loaded.RuleSet!;
    }

    private const string LocationSource = """
        vsir: 0.1
        kind: domain-type
        name: Location
        shape: product
        classification: value-object
        traits: [transform]

        state:
          Commune: Commune
          Street: StreetName
          Extensions:
            sequence: StreetExtension
          Region:
            type: Region
            from: state.Commune.InProvince.InRegion
          Province:
            type: Province
            from: state.Commune.InProvince

        representation:
          CommuneId:
            type: string
            mapping:
              select:
                source: { represent: state.Commune }
                field: Id
          Street:
            type: string
            mapping:
              select:
                source: { represent: state.Street }
                field: Value
          Ext:
            type:
              sequence: string
            mapping:
              map:
                source: state.Extensions
                bind: extension
                value:
                  select:
                    source: { represent: extension }
                    field: Value

        input:
          CommuneId: CommuneId
          Street: string
          Ext:
            sequence: string

        construction:
          - resolve:
              source: Commune
              id: input.CommuneId
              as: commune
              failure:
                message: Debes especificar una comuna existente
          - apply:
              over: StreetName
              input:
                Value: input.Street
              as: street
          - apply:
              over: StreetExtension
              input:
                source: input.Ext
                map:
                  Value: item
              as: extensions
          - refine:
              state:
                Commune: commune
                Street: street
                Extensions: extensions
        """;
}
