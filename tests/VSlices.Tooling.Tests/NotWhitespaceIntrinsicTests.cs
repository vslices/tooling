namespace VSlices.Tooling.Tests;

public sealed class NotWhitespaceIntrinsicTests
{
    [Fact]
    public async Task Real_cli_lowers_not_whitespace_through_ruleset_renderer()
    {
        using var project = new ToolingTestProject();
        project.WriteConfiguration();
        ToolingTestProject.WriteValidRuleset(project.RulesetRoot);

        var vsir = Path.Combine(project.Root, "Description.vsir");
        File.WriteAllText(vsir, """
            vsir: 0.1
            kind: domain-type
            name: Description
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
                      intrinsic: not-whitespace
                      value: input.Value
                    failure:
                      message: required
            """);

        var result = await project.Run(
            project.Root,
            "transpile",
            vsir,
            "--namespace",
            "Tests.Domain",
            "--stdout");

        var output = result.StandardOutput + result.StandardError;
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("VSIR102", output, StringComparison.Ordinal);
        Assert.Contains("!string.IsNullOrWhiteSpace(input.Value)", result.StandardOutput, StringComparison.Ordinal);
    }
}
