namespace VSlices.Tooling.Tests;

public sealed class VsirArtifactStateTests
{
    [Fact]
    public async Task Discovery_reports_progressive_validity_without_claiming_conformance_or_lowerability()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        File.WriteAllText(Path.Combine(project.Root, "Example.vsir"), """
            vsir: 0.1
            name: Example
            """);

        var result = await project.Run(project.Root, "discovery", "vsir", "Example.vsir");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("progressive validity: valid", result.StandardOutput);
        Assert.Contains("conformance: incomplete", result.StandardOutput);
        Assert.Contains("missing required: kind", result.StandardOutput);
        Assert.Contains("lowerability: not evaluated by discovery", result.StandardOutput);
    }

    [Fact]
    public async Task Discovery_reports_conforming_for_complete_canonical_VSIR()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        project.WriteStreetName();

        var result = await project.Run(project.Root, "discovery", "vsir", "StreetName.vsir");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("progressive validity: valid", result.StandardOutput);
        Assert.Contains("conformance: conforming", result.StandardOutput);
        Assert.DoesNotContain("missing required:", result.StandardOutput);
        Assert.Contains("public semantic authoring: represented by the immediate frontier", result.StandardOutput);
        Assert.Contains("lowerability: not evaluated by discovery", result.StandardOutput);
    }

    [Fact]
    public async Task Canonical_sum_can_conform_while_public_semantic_authoring_remains_gated()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        File.WriteAllText(Path.Combine(project.Root, "Name.vsir"), """
            vsir: 0.1
            kind: domain-type
            name: Name
            shape: sum
            classification: value-object

            state: {}
            representation: {}

            variants:
              FullName:
                traits: [transform]
                state:
                  Names: string
                  FirstSurname: string
                  SecondSurname:
                    optional: string
                representation:
                  Names: string
                  FirstSurname: string
                  SecondSurname:
                    optional: string
                input:
                  Names: string
                  FirstSurname: string
                  SecondSurname:
                    optional: string
                construction:
                  - ensure:
                      condition:
                        intrinsic: non-empty
                        args:
                          value: input.Names
                      failure:
                        message: Debes especificar al menos un nombre
                  - ensure:
                      condition:
                        intrinsic: non-empty
                        args:
                          value: input.FirstSurname
                      failure:
                        message: Debes especificar el primer apellido
                  - ensure:
                      condition:
                        intrinsic: length-at-most
                        args:
                          value:
                            intrinsic: concat-space
                            values:
                              - input.Names
                              - input.FirstSurname
                              - input.SecondSurname
                          max: 92
                      failure:
                        message: El nombre debe tener 92 caracteres o menos
                  - refine:
                      state:
                        Names: input.Names
                        FirstSurname: input.FirstSurname
                        SecondSurname: input.SecondSurname

              CompanyName:
                traits: [transform]
                state:
                  Value: string
                representation:
                  Value: string
                input:
                  Value: string
                construction:
                  - ensure:
                      condition:
                        intrinsic: non-empty
                        args:
                          value: input.Value
                      failure:
                        message: Debes especificar el nombre de la empresa
                  - ensure:
                      condition:
                        intrinsic: length-at-most
                        args:
                          value: input.Value
                          max: 92
                      failure:
                        message: El nombre debe tener 92 caracteres o menos
                  - refine:
                      state:
                        Value: input.Value
            """);

        var result = await project.Run(project.Root, "discovery", "vsir", "Name.vsir");
        var output = result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("conformance: conforming", output);
        Assert.Contains("public semantic authoring: gated for this conforming form", output);
        Assert.DoesNotContain("\nshape\n", output);
        Assert.DoesNotContain("\nclassification\n", output);
        Assert.Contains("\ntags\n", output);
    }

    [Fact]
    public async Task Canonical_maintained_can_conform_while_public_semantic_authoring_remains_gated()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        File.WriteAllText(Path.Combine(project.Root, "IdentityType.vsir"), """
            vsir: 0.1
            kind: domain-type
            name: IdentityType
            shape: product
            classification: maintained

            state:
              Name: string

            representation:
              Value:
                type: string
                from: state.Name

            values:
              Natural:
                state:
                  Name: Natural
              Juridical:
                state:
                  Name: Juridica

            equality:
              intrinsic: ordinal-equals
              by: state.Name
            """);

        var result = await project.Run(project.Root, "discovery", "vsir", "IdentityType.vsir");
        var output = result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("conformance: conforming", output);
        Assert.Contains("public semantic authoring: gated for this conforming form", output);
        Assert.DoesNotContain("\nclassification\n", output);
        Assert.DoesNotContain("\nrepresentation.Value.from\n", output);
        Assert.Contains("\ntags\n", output);
    }

    [Fact]
    public void Assess_checks_required_nested_paths_instead_of_only_their_root()
    {
        const string source = """
            vsir: 0.1
            name: Example
            state:
              Other: string
            """;
        var frontier = new[]
        {
            new VsirPathContract(
                "state.Value",
                "semantic-field-declaration",
                VsirFrontierStatus.Required,
                "Required nested test assertion.",
                new HashSet<VsirMutationKind> { VsirMutationKind.Set })
        };

        var state = VsirArtifactState.Assess(source, frontier);

        Assert.Equal(VsirProgressiveValidity.Valid, state.ProgressiveValidity);
        Assert.Equal(VsirConformanceState.Incomplete, state.Conformance);
        Assert.Equal(new[] { "state.Value" }, state.MissingRequiredPaths);
    }

    [Fact]
    public async Task Discovery_reports_repairable_invalid_assertion_as_progressively_valid_and_conformance_invalid()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        File.WriteAllText(Path.Combine(project.Root, "Repairable.vsir"), """
            vsir: 0.1
            kind: domain-type
            name: Repairable
            shape: triangle
            """);

        var result = await project.Run(project.Root, "discovery", "vsir", "Repairable.vsir");
        var output = result.StandardError + result.StandardOutput;

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("progressive validity: valid", output);
        Assert.Contains("conformance: invalid", output);
        Assert.Contains("Conformance diagnostics:", output);
        Assert.DoesNotContain("VSIR-AUTH", output);
        Assert.Contains("shape", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("values: product", output);
        Assert.DoesNotContain("\nstate\n", result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Tags_do_not_change_conformance_assessment()
    {
        const string source = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input: string
            construction:
              - refine:
                  value: input
                  as: state.Value
            """;
        const string tagged = """
            vsir: 0.1
            kind: domain-type
            name: StreetName
            tags: [ticket, serviu]
            shape: product
            classification: value-object
            traits: [transform]
            state:
              Value: string
            representation:
              Value: string
            input: string
            construction:
              - refine:
                  value: input
                  as: state.Value
            """;

        var frontier = VsirMutationPipeline.Discover(source, out var sourceError);
        var taggedFrontier = VsirMutationPipeline.Discover(tagged, out var taggedError);
        Assert.Null(sourceError);
        Assert.Null(taggedError);

        var baseline = VsirArtifactState.Assess(source, frontier);
        var withTags = VsirArtifactState.Assess(tagged, taggedFrontier);

        Assert.Equal(baseline.ProgressiveValidity, withTags.ProgressiveValidity);
        Assert.Equal(baseline.Conformance, withTags.Conformance);
        Assert.Equal(baseline.MissingRequiredPaths, withTags.MissingRequiredPaths);
        Assert.Equal(
            baseline.ConformanceDiagnostics.Select(x => x.Code),
            withTags.ConformanceDiagnostics.Select(x => x.Code));
    }

    [Fact]
    public async Task Discovery_rejects_an_artifact_without_progressive_identity()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        File.WriteAllText(Path.Combine(project.Root, "Broken.vsir"), """
            vsir: 0.1
            kind: domain-type
            """);

        var result = await project.Run(project.Root, "discovery", "vsir", "Broken.vsir");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("DISC017", result.StandardError + result.StandardOutput);
        Assert.DoesNotContain("Immediate frontier:", result.StandardOutput);
    }
}
