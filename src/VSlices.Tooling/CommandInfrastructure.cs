using VSlices.Vsir;

namespace VSlices.Tooling;

internal static class CommandInfrastructure
{
    public static (string? Path, VsirDiagnostic? Diagnostic) ResolveVsir(string value, string cwd)
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
        var policy = ArtifactDiscoveryPolicy.Load(cwd);
        var matches = EnumerateVsirFiles(cwd, symbol + ".vsir", policy)
            .Take(3)
            .ToArray();

        return matches.Length switch
        {
            1 => (matches[0], null),
            0 => (null, new("CLI001", $"Could not resolve VSIR symbol or path '{value}'.")),
            _ => (null, new("CLI002", $"VSIR symbol '{symbol}' is ambiguous. Use a path to disambiguate."))
        };
    }

    public static string ConventionalMaterializationPath(string vsirPath, string target) =>
        NormalizeTarget(target) switch
        {
            "csharp" => vsirPath + ".cs",
            var normalized => vsirPath + "." + normalized
        };

    public static (string? Target, VsirDiagnostic? Diagnostic) ResolveTarget(
        string? requested,
        string rulesetRoot)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return ValidateTarget(requested, rulesetRoot);

        var configuration = ProjectConfiguration.LoadFromRulesetRoot(rulesetRoot);
        if (!string.IsNullOrWhiteSpace(configuration?.DefaultTarget))
        {
            var configured = ValidateTarget(configuration.DefaultTarget, rulesetRoot);
            if (configured.Diagnostic is not null)
            {
                return (null, new(
                    "CLI023",
                    $"Configured default target '{configuration.DefaultTarget}' is not available in the project-local ruleset."));
            }

            return configured;
        }

        var installed = InstalledTargets(rulesetRoot);
        return installed.Count switch
        {
            1 => (installed[0], null),
            0 => (null, new("CLI021", "No supported target is installed in the project-local ruleset. Run 'vslices init' or specify -to after installing a target.")),
            _ => (null, new("CLI022", "More than one supported target is installed and no default target is configured. Specify -to explicitly or set targets.default in .vslices/config.yaml."))
        };
    }

    public static string DisplayTarget(string target) =>
        NormalizeTarget(target) == "csharp" ? "C#" : target;

    public static string NormalizeTarget(string target) =>
        target.Equals("C#", StringComparison.OrdinalIgnoreCase) ||
        target.Equals("csharp", StringComparison.OrdinalIgnoreCase)
            ? "csharp"
            : target.Trim().ToLowerInvariant();

    public static async Task<int> WriteResult(
        string content,
        string defaultPath,
        string? output,
        bool stdout,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        if (stdout && !string.IsNullOrWhiteSpace(output) && output != "-")
        {
            Console.Error.WriteLine("CLI030: --stdout cannot be combined with an explicit output path.");
            return 2;
        }

        if (stdout || output == "-")
        {
            Console.Write(content);
            return 0;
        }

        var resolved = string.IsNullOrWhiteSpace(output)
            ? defaultPath
            : Path.GetFullPath(output, Environment.CurrentDirectory);

        if (File.Exists(resolved) && !overwrite)
        {
            Console.Error.WriteLine(
                $"CLI031: Output '{resolved}' already exists. Use 'vslices lower', 'vslices rebase', or --force to replace it explicitly.");
            return 1;
        }

        await AtomicWrite(resolved, content, cancellationToken);
        Console.WriteLine($"Wrote '{resolved}'.");
        return 0;
    }

    public static async Task AtomicWrite(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(temporary, content, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public static void WriteDiagnostics(IEnumerable<VsirDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    }

    private static (string? Target, VsirDiagnostic? Diagnostic) ValidateTarget(
        string target,
        string rulesetRoot)
    {
        var normalized = NormalizeTarget(target);
        if (normalized != "csharp")
        {
            return (null, new(
                "CLI020",
                $"Target '{target}' is not supported. Current experimental target: C#."));
        }

        if (!Directory.Exists(Path.Combine(rulesetRoot, normalized)))
        {
            return (null, new(
                "CLI024",
                $"Target '{DisplayTarget(normalized)}' is supported by the CLI but is not installed in the project-local ruleset."));
        }

        return (normalized, null);
    }

    private static IReadOnlyList<string> InstalledTargets(string rulesetRoot)
    {
        var installed = new List<string>();
        if (Directory.Exists(Path.Combine(rulesetRoot, "csharp")))
            installed.Add("csharp");

        return installed;
    }

    private static IEnumerable<string> EnumerateVsirFiles(
        string root,
        string searchPattern,
        ArtifactDiscoveryPolicy policy)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var file in Directory.EnumerateFiles(current, searchPattern, SearchOption.TopDirectoryOnly))
            {
                if (!policy.IgnoreFile(file))
                    yield return file;
            }

            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (!policy.IgnoreDirectory(directory))
                    pending.Push(directory);
            }
        }
    }
}
