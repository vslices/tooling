using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class NewCommands
{
    /// <summary>Creates a VSIR representation template from currently known semantic choices.</summary>
    /// <param name="name">Semantic name of the represented artifact.</param>
    /// <param name="kind">VSIR artifact kind. Current supported value: domain-type.</param>
    /// <param name="classification">Optional classification valid for the selected kind.</param>
    /// <param name="shape">Optional structural shape valid for the selected kind.</param>
    /// <param name="traits">Optional comma-separated additional traits. Inferred traits must not be repeated.</param>
    public static int Representation(
        [Argument] string name,
        string kind,
        string? classification = null,
        string? shape = null,
        string? traits = null)
    {
        var explicitTraits = string.IsNullOrWhiteSpace(traits)
            ? Array.Empty<string>()
            : traits.Split(',', StringSplitOptions.TrimEntries);

        var result = RepresentationTemplate.Create(
            name,
            kind,
            classification,
            shape,
            explicitTraits);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Error);
            return 2;
        }

        Console.Write(result.Source);
        return 0;
    }
}
