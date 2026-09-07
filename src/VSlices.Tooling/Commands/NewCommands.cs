using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class NewCommands
{
    /// <summary>Creates a progressive VSIR artifact from the semantic facts currently known.</summary>
    /// <param name="name">Semantic name of the concept being introduced.</param>
    /// <param name="kind">Optional VSIR artifact kind. Current supported value: domain-type.</param>
    /// <param name="classification">Optional classification valid for the selected kind.</param>
    /// <param name="shape">Optional structural shape valid for the selected kind.</param>
    /// <param name="traits">Optional comma-separated additional traits. Inferred traits must not be repeated.</param>
    /// <param name="tags">Optional comma-separated organizational tags. Tags do not require a kind.</param>
    /// <param name="output">-o, Optional output path. By default &lt;name&gt;.vsir is created in the current directory.</param>
    /// <param name="stdout">Write the result to standard output instead of creating a file. Equivalent to -o -.</param>
    /// <param name="force">Replace an existing output explicitly.</param>
    public static async Task<int> Vsir(
        [Argument] string name,
        string? kind = null,
        string? classification = null,
        string? shape = null,
        string? traits = null,
        string? tags = null,
        string? output = null,
        bool stdout = false,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var explicitTraits = string.IsNullOrWhiteSpace(traits)
            ? Array.Empty<string>()
            : traits.Split(',', StringSplitOptions.TrimEntries);

        var explicitTags = string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags.Split(',', StringSplitOptions.TrimEntries);

        var result = VsirTemplate.Create(
            name,
            kind,
            classification,
            shape,
            explicitTraits,
            explicitTags);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Error);
            return 2;
        }

        var defaultPath = Path.GetFullPath(
            name.EndsWith(".vsir", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".vsir",
            Environment.CurrentDirectory);

        return await CommandInfrastructure.WriteResult(
            result.Source!,
            defaultPath,
            output,
            stdout,
            overwrite: force,
            cancellationToken);
    }
}
