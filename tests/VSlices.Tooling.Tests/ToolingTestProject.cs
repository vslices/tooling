namespace VSlices.Tooling.Tests;

internal sealed class ToolingTestProject : IDisposable
{
    private readonly string _previousCurrentDirectory;

    public ToolingTestProject()
    {
        Root = Path.Combine(Path.GetTempPath(), "vslices-tooling-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        _previousCurrentDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Root;
    }

    public string Root { get; }
    public string VslicesRoot => Path.Combine(Root, ".vslices");
    public string RulesetRoot => Path.Combine(VslicesRoot, "ruleset");
    public string LineageRoot => Path.Combine(VslicesRoot, "lineage");

    public async Task WriteConfiguration(
        string? rulesetSource = null,
        string? rulesetRef = null,
        string? convention = ProjectConfiguration.DefaultLineageBootstrapConvention)
    {
        await ProjectConfiguration.WriteAsync(
            Root,
            new ProjectConfiguration(
                ProjectConfiguration.CurrentVersion,
                "csharp",
                rulesetSource ?? ProjectConfiguration.OfficialRulesetSource,
                rulesetRef,
                ProjectConfiguration.OfficialToolingSource,
                ProjectConfiguration.DefaultUpdateChannel,
                null,
                convention),
            CancellationToken.None);
    }

    public static void WriteValidRuleset(string root, string? marker = null)
    {
        Directory.CreateDirectory(Path.Combine(root, "csharp"));
        File.WriteAllText(Path.Combine(root, "manifest.yaml"), """
            targets:
              csharp:
                rules:
                  - csharp/intrinsics.yaml
            """);
        File.WriteAllText(Path.Combine(root, "csharp", "intrinsics.yaml"), """
            rules:
              - node: intrinsic.non-empty
                mode: deterministic
                renderer: expression
                template: "!string.IsNullOrEmpty({value})"
              - node: intrinsic.length-at-most
                mode: deterministic
                renderer: expression
                template: "{value}.Length <= {max}"
              - node: equality.ordinal-equals.equals
                mode: deterministic
                renderer: expression
                template: "string.Equals({left}, {right}, StringComparison.Ordinal)"
              - node: equality.ordinal-equals.hash
                mode: deterministic
                renderer: expression
                template: "StringComparer.Ordinal.GetHashCode({value})"
            """);
        if (marker is not null)
            File.WriteAllText(Path.Combine(root, marker), marker);
    }

    public string WriteStreetName(int max = 30)
    {
        var path = Path.Combine(Root, "StreetName.vsir");
        File.WriteAllText(path, $$"""
            vsir: 0.1
            kind: domain-type
            name: StreetName
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
                      message: required
                - ensure:
                    condition:
                      intrinsic: length-at-most
                      value: input.Value
                      max: {{max}}
                    failure:
                      message: too long
            """);
        return path;
    }

    public VSlicesProjectContext Context() =>
        VSlicesProjectContext.FindFrom(Root)
        ?? throw new InvalidOperationException("Expected test project context.");

    public void Dispose()
    {
        Environment.CurrentDirectory = _previousCurrentDirectory;
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}
