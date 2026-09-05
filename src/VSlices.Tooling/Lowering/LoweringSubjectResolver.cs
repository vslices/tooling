using VSlices.Vsir;

namespace VSlices.Tooling;

internal enum LoweringSubjectKind
{
    VsirArtifact,
    DotNetProject
}

internal sealed record LoweringSubject(
    LoweringSubjectKind Kind,
    string Path);

internal static class LoweringSubjectResolver
{
    public static (LoweringSubject? Subject, VsirDiagnostic? Diagnostic) Resolve(
        string value,
        string cwd)
    {
        var explicitPath = Path.GetFullPath(value, cwd);
        if (File.Exists(explicitPath))
        {
            return Path.GetExtension(explicitPath).ToLowerInvariant() switch
            {
                ".vsir" => (new(LoweringSubjectKind.VsirArtifact, explicitPath), null),
                ".csproj" => (new(LoweringSubjectKind.DotNetProject, explicitPath), null),
                _ => (null, new("CLI003", $"Lowering subject '{value}' must resolve to a .vsir or .csproj file."))
            };
        }

        var requestedExtension = Path.GetExtension(value);
        if (requestedExtension.Equals(".vsir", StringComparison.OrdinalIgnoreCase))
            return ResolveVsir(value, cwd, explicitlyTyped: true);
        if (requestedExtension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            return ResolveProject(value, cwd, explicitlyTyped: true);

        var vsir = ResolveVsir(value, cwd, explicitlyTyped: false);
        var project = ResolveProject(value, cwd, explicitlyTyped: false);
        var hasVsir = vsir.Subject is not null;
        var hasProject = project.Subject is not null;

        if (hasVsir && hasProject)
        {
            return (null, new(
                "CLI004",
                $"Lowering subject '{value}' is ambiguous: both a VSIR artifact and a .NET project match. Specify '{value}.vsir' or '{value}.csproj'."));
        }

        if (hasVsir)
            return vsir;
        if (hasProject)
            return project;

        if (vsir.Diagnostic?.Code == "CLI002")
            return vsir;
        if (project.Diagnostic?.Code == "CLI005")
            return project;

        return (null, new("CLI001", $"Could not resolve lowering subject '{value}' as a VSIR artifact or .NET project."));
    }

    private static (LoweringSubject? Subject, VsirDiagnostic? Diagnostic) ResolveVsir(
        string value,
        string cwd,
        bool explicitlyTyped)
    {
        var symbol = explicitlyTyped
            ? Path.GetFileNameWithoutExtension(value)
            : Path.GetFileName(value);

        var directValue = explicitlyTyped ? value : value + ".vsir";
        var direct = Path.GetFullPath(directValue, cwd);
        if (File.Exists(direct))
            return (new(LoweringSubjectKind.VsirArtifact, direct), null);

        var searchRoot = VSlicesProjectContext.FindFrom(cwd)?.ProjectRoot ?? Path.GetFullPath(cwd);
        var policy = ArtifactDiscoveryPolicy.Load(searchRoot);
        var matches = EnumerateFiles(searchRoot, symbol + ".vsir", policy)
            .Take(3)
            .ToArray();

        return matches.Length switch
        {
            1 => (new(LoweringSubjectKind.VsirArtifact, matches[0]), null),
            0 => (null, null),
            _ => (null, new("CLI002", $"VSIR symbol '{symbol}' is ambiguous. Use a path to disambiguate."))
        };
    }

    private static (LoweringSubject? Subject, VsirDiagnostic? Diagnostic) ResolveProject(
        string value,
        string cwd,
        bool explicitlyTyped)
    {
        var symbol = explicitlyTyped
            ? Path.GetFileNameWithoutExtension(value)
            : Path.GetFileName(value);

        var directValue = explicitlyTyped ? value : value + ".csproj";
        var direct = Path.GetFullPath(directValue, cwd);
        if (File.Exists(direct))
            return (new(LoweringSubjectKind.DotNetProject, direct), null);

        var searchRoot = VSlicesProjectContext.FindFrom(cwd)?.ProjectRoot ?? Path.GetFullPath(cwd);
        var policy = ArtifactDiscoveryPolicy.Load(searchRoot);
        var matches = EnumerateFiles(searchRoot, symbol + ".csproj", policy)
            .Take(3)
            .ToArray();

        return matches.Length switch
        {
            1 => (new(LoweringSubjectKind.DotNetProject, matches[0]), null),
            0 => (null, null),
            _ => (null, new("CLI005", $".NET project symbol '{symbol}' is ambiguous. Use a path to disambiguate."))
        };
    }

    internal static IEnumerable<string> EnumerateProjectVsirFiles(string projectPath)
    {
        var root = Path.GetDirectoryName(projectPath)!;
        var policy = ArtifactDiscoveryPolicy.Load(root);
        return EnumerateFiles(root, "*.vsir", policy);
    }

    private static IEnumerable<string> EnumerateFiles(
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
