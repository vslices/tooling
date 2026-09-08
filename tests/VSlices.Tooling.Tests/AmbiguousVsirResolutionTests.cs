namespace VSlices.Tooling.Tests;

public sealed class AmbiguousVsirResolutionTests
{
    [Fact]
    public async Task Lower_lists_ambiguous_vsir_candidates_as_relative_paths()
    {
        using var project = new ToolingTestProject();

        var first = Path.Combine(project.Root, "Products", "A");
        var second = Path.Combine(project.Root, "Products", "B");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        File.WriteAllText(Path.Combine(first, "IncidentTypeReference.vsir"), "vsir: 0.1");
        File.WriteAllText(Path.Combine(second, "IncidentTypeReference.vsir"), "vsir: 0.1");

        var result = await project.Run(project.Root, "lower", "IncidentTypeReference");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("CLI002", result.StandardError, StringComparison.Ordinal);

        var candidateLines = result.StandardError
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line.Trim()[2..].Replace('\\', '/'))
            .ToArray();

        Assert.Equal(2, candidateLines.Length);
        Assert.Contains("Products/A/IncidentTypeReference.vsir", candidateLines, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Products/B/IncidentTypeReference.vsir", candidateLines, StringComparer.OrdinalIgnoreCase);
    }
}
