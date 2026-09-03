using ConsoleAppFramework;
using VSlices.Targets.DotNet;
using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Tooling;

internal static class VsirCommands
{
    /// <summary>Produces a deterministic projection from a VSIR artifact.</summary>
    /// <param name="subject">VSIR symbol or path. The .vsir extension is optional.</param>
    /// <param name="to">-to, Target language. Current experimental target: C#.</param>
    /// <param name="namespace">-n, Optional namespace override. By default VSlices asks the .NET target tooling for project/item context.</param>
    public static async Task<int> Transpile(
        [Argument] string subject,
        string to,
        string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsCSharp(to))
        {
            Console.Error.WriteLine($"Target '{to}' is not supported. Experimental target: C#.");
            return 2;
        }

        var lowered = await TranspileCore(subject, @namespace, cancellationToken);
        if (!lowered.IsSuccess)
        {
            WriteDiagnostics(lowered.Diagnostics);
            return 1;
        }

        Console.Write(lowered.Source);
        return 0;
    }

    /// <summary>Re-applies a deterministic VSIR change over a human-edited projection.</summary>
    /// <param name="subject">New VSIR symbol or path.</param>
    /// <param name="to">-to, Target language. Current experimental target: C#.</param>
    /// <param name="from">Previous VSIR symbol or path used to reconstruct the deterministic baseline.</param>
    /// <param name="source">Human-edited C# projection to preserve.</param>
    /// <param name="namespace">-n, Optional namespace override. By default VSlices asks the .NET target tooling for project/item context.</param>
    public static async Task<int> Rebase(
        [Argument] string subject,
        string to,
        string from,
        string source,
        string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsCSharp(to))
        {
            Console.Error.WriteLine($"Target '{to}' is not supported. Experimental target: C#.");
            return 2;
        }

        var previous = await TranspileCore(from, @namespace, cancellationToken);
        var next = await TranspileCore(subject, @namespace, cancellationToken);

        if (!previous.IsSuccess)
        {
            WriteDiagnostics(previous.Diagnostics);
            return 1;
        }

        if (!next.IsSuccess)
        {
            WriteDiagnostics(next.Diagnostics);
            return 1;
        }

        var resolvedHuman = Path.GetFullPath(source, Environment.CurrentDirectory);
        if (!File.Exists(resolvedHuman))
        {
            Console.Error.WriteLine($"CLI003: Could not resolve human C# projection '{source}'.");
            return 1;
        }

        var human = await File.ReadAllTextAsync(resolvedHuman, cancellationToken);
        var rebased = CSharpRebaser.Rebase(previous.Source!, human, next.Source!);
        if (!rebased.IsSuccess)
        {
            WriteDiagnostics(rebased.Diagnostics);
            return 1;
        }

        Console.Write(rebased.Source);
        return 0;
    }

    private static async Task<CSharpLoweringResult> TranspileCore(
        string input,
        string? namespaceOverride,
        CancellationToken cancellationToken)
    {
        var resolution = ResolveVsir(input, Environment.CurrentDirectory);
        if (resolution.Diagnostic is not null)
            return new(null, [resolution.Diagnostic]);

        var targetContext = await DotNetTargetContextResolver.Resolve(
            resolution.Path!,
            namespaceOverride,
            cancellationToken);

        if (targetContext.Diagnostic is not null)
            return new(null, [targetContext.Diagnostic]);

        var text = await File.ReadAllTextAsync(resolution.Path!, cancellationToken);
        var parsed = VsirParser.Parse(text);
        if (!parsed.IsSuccess)
            return new(null, parsed.Diagnostics);

        return CSharpLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext(targetContext.Context!.Namespace));
    }

    private static (string? Path, VsirDiagnostic? Diagnostic) ResolveVsir(string value, string cwd)
    {
        var direct = Path.GetFullPath(value, cwd);
        if (File.Exists(direct))
            return (direct, null);

        if (!Path.HasExtension(value))
        {
            var withExtension = Path.GetFullPath(value + ".vsir", cwd);
            if (File.Exists(withExtension))
                return (withExtension, null);
        }

        var symbol = Path.GetFileNameWithoutExtension(value);
        var matches = Directory
            .EnumerateFiles(cwd, symbol + ".vsir", SearchOption.AllDirectories)
            .Where(path => !HasBuildDirectory(path))
            .Take(3)
            .ToArray();

        return matches.Length switch
        {
            1 => (matches[0], null),
            0 => (null, new("CLI001", $"Could not resolve VSIR symbol or path '{value}'.")),
            _ => (null, new("CLI002", $"VSIR symbol '{symbol}' is ambiguous. Use a path to disambiguate."))
        };
    }

    private static bool HasBuildDirectory(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(x => x is "bin" or "obj");
    }

    private static bool IsCSharp(string target) =>
        target.Equals("C#", StringComparison.OrdinalIgnoreCase) ||
        target.Equals("csharp", StringComparison.OrdinalIgnoreCase);

    private static void WriteDiagnostics(IEnumerable<VsirDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    }
}
