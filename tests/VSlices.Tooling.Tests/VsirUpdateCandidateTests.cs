using VSlices.Vsir;

namespace VSlices.Tooling.Tests;

public sealed class VsirUpdateCandidateTests
{
    [Fact]
    public async Task Invalid_projection_expression_is_rejected_without_modifying_the_artifact()
    {
        using var project = new ToolingTestProject();
        var path = WriteProduct(project.Root, "Probe", "string", "string");
        var before = File.ReadAllText(path);

        var result = await project.Run(
            project.Root,
            "update", "vsir", path,
            "--set", "representation.Value.mapping={banana: state.Value}");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("UPDATE050", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task Adding_mapping_to_structural_type_shorthand_expands_type_without_losing_it()
    {
        using var project = new ToolingTestProject();
        var path = WriteProduct(project.Root, "ProbeSequence", "sequence: string", "sequence: string");

        var result = await project.Run(
            project.Root,
            "update", "vsir", path,
            "--set", "representation.Value.mapping={map: {source: state.Value, bind: item, value: item}}");

        Assert.Equal(0, result.ExitCode);
        var source = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.Contains("Value:\n    type:\n      sequence: string\n    mapping:", source, StringComparison.Ordinal);
        var parsed = VsirParser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
    }

    [Fact]
    public async Task Legitimately_incomplete_progressive_artifact_remains_writable()
    {
        using var project = new ToolingTestProject();
        var created = await project.Run(project.Root, "new", "vsir", "PartialProbe");
        Assert.Equal(0, created.ExitCode);

        var updated = await project.Run(
            project.Root,
            "update", "vsir", "PartialProbe",
            "--set", "kind=domain-type");

        Assert.Equal(0, updated.ExitCode);
        Assert.Contains("kind: domain-type", File.ReadAllText(Path.Combine(project.Root, "PartialProbe.vsir")));
    }

    private static string WriteProduct(
        string root,
        string name,
        string stateType,
        string representationType)
    {
        var path = Path.Combine(root, name + ".vsir");
        var stateDeclaration = stateType.Contains(':', StringComparison.Ordinal)
            ? $"\n    {stateType}"
            : " " + stateType;
        var representationDeclaration = representationType.Contains(':', StringComparison.Ordinal)
            ? $"\n    {representationType}"
            : " " + representationType;

        File.WriteAllText(path, $"""
            vsir: 0.1
            kind: domain-type
            name: {name}
            shape: product
            classification: value-object
            traits: [transform]

            state:
              Value:{stateDeclaration}

            representation:
              Value:{representationDeclaration}

            input:
              Value:{stateDeclaration}
            """);
        return path;
    }
}
