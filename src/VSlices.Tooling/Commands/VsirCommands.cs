using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class VsirCommands
{
    /// <summary>Produces a deterministic projection from a VSIR artifact.</summary>
    /// <param name="subject">VSIR symbol or path. The .vsir extension is optional.</param>
    /// <param name="to">-to, Optional target language. Inferred when exactly one supported target is installed.</param>
    /// <param name="output">-o, Optional output path. By default a sibling materialization is written.</param>
    /// <param name="stdout">Write the result to standard output instead of a file. Equivalent to -o -.</param>
    /// <param name="force">Replace an existing output explicitly.</param>
    /// <param name="namespace">-n, Optional namespace override. By default VSlices asks the .NET target tooling for project/item context.</param>
    public static async Task<int> Transpile(
        [Argument] string subject,
        string? to = null,
        string? output = null,
        bool stdout = false,
        bool force = false,
        string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        var result = await TranspilationOperation.Execute(subject, to, @namespace, cancellationToken);
        if (!result.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(result.Diagnostics);
            return 1;
        }

        var defaultPath = CommandInfrastructure.ConventionalMaterializationPath(
            result.VsirPath!,
            result.Target!);

        return await CommandInfrastructure.WriteResult(
            result.Source!,
            defaultPath,
            output,
            stdout,
            overwrite: force,
            cancellationToken);
    }

    /// <summary>Re-applies a deterministic VSIR change over a human-edited projection.</summary>
    /// <param name="subject">New VSIR symbol or path.</param>
    /// <param name="to">-to, Optional target language. Inferred when exactly one supported target is installed.</param>
    /// <param name="from">Previous VSIR symbol or path used to reconstruct the deterministic baseline.</param>
    /// <param name="source">Human-edited projection to preserve. By default the sibling materialization is used.</param>
    /// <param name="output">-o, Optional output path. By default the source materialization is updated.</param>
    /// <param name="stdout">Write the result to standard output instead of updating a file. Equivalent to -o -.</param>
    /// <param name="namespace">-n, Optional namespace override. By default VSlices asks the target tooling for project/item context.</param>
    public static async Task<int> Rebase(
        [Argument] string subject,
        string? to = null,
        string? from = null,
        string? source = null,
        string? output = null,
        bool stdout = false,
        string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            Console.Error.WriteLine(
                "CLI040: Rebase needs the previous VSIR baseline. Specify --from <previous-vsir>. Automatic provenance is currently orchestrated by 'vslices lower'.");
            return 2;
        }

        var result = await RebaseOperation.ExecuteFromVsir(
            subject,
            to,
            from,
            source,
            @namespace,
            cancellationToken);
        if (!result.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(result.Diagnostics);
            return 1;
        }

        return await CommandInfrastructure.WriteResult(
            result.Source!,
            result.SourcePath!,
            output,
            stdout,
            overwrite: true,
            cancellationToken);
    }

    /// <summary>Lowers a VSIR artifact using the least powerful currently available mechanism.</summary>
    /// <param name="subject">VSIR symbol or path.</param>
    /// <param name="to">-to, Optional target language. Inferred when exactly one supported target is installed.</param>
    /// <param name="from">Previous VSIR baseline override when automatic lineage cannot be established.</param>
    /// <param name="source">Existing human-edited materialization override.</param>
    /// <param name="output">-o, Optional output path.</param>
    /// <param name="stdout">Write the result to standard output instead of a file. Equivalent to -o -.</param>
    /// <param name="namespace">-n, Optional namespace override.</param>
    public static Task<int> Lower(
        [Argument] string subject,
        string? to = null,
        string? from = null,
        string? source = null,
        string? output = null,
        bool stdout = false,
        string? @namespace = null,
        CancellationToken cancellationToken = default) =>
        LoweringCoordinator.Execute(
            subject,
            to,
            from,
            source,
            output,
            stdout,
            @namespace,
            cancellationToken);
}
