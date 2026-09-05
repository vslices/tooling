using System.Diagnostics;

namespace VSlices.Tooling.Tests;

internal sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed class ToolingTestProject : IDisposable
{
    public ToolingTestProject()
    {
        Root = Path.Combine(Path.GetTempPath(), "vslices-tooling-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string VslicesRoot => Path.Combine(Root, ".vslices");
    public string RulesetRoot => Path.Combine(VslicesRoot, "ruleset");
    public string ExtensionsRoot => Path.Combine(VslicesRoot, "extensions");
    public string LineageRoot => Path.Combine(VslicesRoot, "lineage");

    public void WriteConfiguration(
        string? rulesetSource = null,
        string? rulesetRef = null,
        bool bootstrap = true)
    {
        Directory.CreateDirectory(VslicesRoot);
        var refText = string.IsNullOrWhiteSpace(rulesetRef)
            ? string.Empty
            : $"  ref: {rulesetRef}{Environment.NewLine}";
        var lineage = bootstrap
            ? """
              lineage:
                bootstrap:
                  convention: existing-materialization
              """ + Environment.NewLine
            : string.Empty;

        File.WriteAllText(Path.Combine(VslicesRoot, "config.yaml"), $"""
            version: 0.1
            targets:
              default: csharp
            ruleset:
              source: {rulesetSource ?? "https://github.com/vslices/ruleset"}
            {refText}{lineage}updates:
              source: https://github.com/vslices/tooling
              channel: preview
            """);
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
              - node: intrinsic.not-whitespace
                mode: deterministic
                renderer: expression
                template: "!string.IsNullOrWhiteSpace({value})"
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

    public string WriteStreetName(int max = 30, string? directory = null)
    {
        var targetDirectory = directory ?? Root;
        Directory.CreateDirectory(targetDirectory);
        var path = Path.Combine(targetDirectory, "StreetName.vsir");
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

    public async Task<CliResult> Run(string workingDirectory, params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        var cli = Path.Combine(repositoryRoot, "src", "VSlices.Tooling", "bin", "Release", "net10.0", "vslices.dll");
        Assert.True(File.Exists(cli), $"Expected built CLI at '{cli}'.");

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(cli);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start VSlices CLI.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliResult(process.ExitCode, await stdout, await stderr);
    }

    public string BaselineFor(string materializationPath) =>
        Path.Combine(
            LineageRoot,
            "csharp",
            Path.GetRelativePath(Root, materializationPath) + ".baseline");

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "tooling.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate tooling.slnx from test output.");
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}
