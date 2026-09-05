using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using VSlices.Targets.DotNet;
using VSlices.Vsir;

namespace VSlices.Tooling;

internal static class DotNetTypeResolutionClient
{
    private static readonly HashSet<string> BuiltInTypes = new(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong",
        "nint", "nuint", "float", "double", "decimal", "char", "string", "object"
    };

    public static async Task<DotNetTypeResolutionResult> ResolveImports(
        DomainTypeVsir document,
        DotNetTargetContext targetContext,
        CancellationToken cancellationToken)
    {
        var typeNames = NominalTypeNames(document).ToArray();
        if (typeNames.Length == 0)
            return DotNetTypeResolutionResult.Success([]);

        if (targetContext.ProjectPath is null)
        {
            return DotNetTypeResolutionResult.Failure([
                new(
                    "DOTNET040",
                    "Nominal C# type resolution requires a related .csproj. The VSIR uses target-visible nominal types, but target project context is unavailable.")
            ]);
        }

        var helper = ResolveHelper();
        if (helper is null)
        {
            return DotNetTypeResolutionResult.Failure([
                new(
                    "DOTNET041",
                    "The Roslyn target-context helper is not installed beside the VSlices CLI, so nominal C# types cannot be resolved safely.")
            ]);
        }

        var transactionRoot = Path.Combine(
            Path.GetTempPath(),
            "vslices-type-resolution-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(transactionRoot);
        var manifestPath = Path.Combine(transactionRoot, "manifest.json");

        try
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(helper);
            startInfo.ArgumentList.Add("resolve-types");
            Add("--project", targetContext.ProjectPath);
            foreach (var typeName in typeNames)
                Add("--type", typeName);
            Add("--manifest", manifestPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return DotNetTypeResolutionResult.Failure([
                    new("DOTNET042", "Could not start the Roslyn nominal type-resolution helper.")
                ]);
            }

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var standardOutput = await stdout;
            var standardError = await stderr;

            if (!File.Exists(manifestPath))
            {
                var detail = string.Join(
                    " ",
                    new[] { standardError.Trim(), standardOutput.Trim() }
                        .Where(x => x.Length > 0));
                return DotNetTypeResolutionResult.Failure([
                    new(
                        "DOTNET043",
                        "Roslyn nominal type resolution did not produce a manifest." +
                        (detail.Length == 0 ? string.Empty : " " + detail))
                ]);
            }

            var manifestText = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize(
                manifestText,
                TypeResolutionJsonContext.Default.TypeResolutionClientManifest);
            if (manifest is null)
            {
                return DotNetTypeResolutionResult.Failure([
                    new("DOTNET044", "Roslyn nominal type resolution returned an invalid manifest.")
                ]);
            }

            if (!manifest.Success)
            {
                return DotNetTypeResolutionResult.Failure(
                    manifest.Diagnostics.Select(x => new VsirDiagnostic(x.Code, x.Message)));
            }

            var imports = manifest.Types
                .Select(x => x.Namespace)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x) &&
                    !string.Equals(x, targetContext.Namespace, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            return DotNetTypeResolutionResult.Success(imports);

            void Add(string name, string value)
            {
                startInfo.ArgumentList.Add(name);
                startInfo.ArgumentList.Add(value);
            }
        }
        finally
        {
            if (Directory.Exists(transactionRoot))
                Directory.Delete(transactionRoot, recursive: true);
        }
    }

    public static string ApplyImports(string source, IReadOnlyCollection<string> imports)
    {
        if (imports.Count == 0)
            return source;

        var header = string.Join(
            Environment.NewLine,
            imports.OrderBy(x => x, StringComparer.Ordinal).Select(x => $"using {x};"));
        return header + Environment.NewLine + Environment.NewLine + source;
    }

    private static IEnumerable<string> NominalTypeNames(DomainTypeVsir document)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        void Add(string? type)
        {
            if (string.IsNullOrWhiteSpace(type) ||
                BuiltInTypes.Contains(type) ||
                string.Equals(type, document.Name, StringComparison.Ordinal) ||
                !IsSimpleIdentifier(type))
            {
                return;
            }

            names.Add(type);
        }

        Add(document.RefinedFrom);
        Add(document.Equality?.Over);
        foreach (var field in document.State.Fields)
            Add(field.Type);
        foreach (var field in document.Representation.Fields)
            Add(field.Type);
        if (document.Construction.Input.IsScalar)
            Add(document.Construction.Input.ScalarType);
        else
            foreach (var field in document.Construction.Input.Fields)
                Add(field.Type);

        return names.OrderBy(x => x, StringComparer.Ordinal);
    }

    private static bool IsSimpleIdentifier(string value)
    {
        if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_'))
            return false;

        return value.Skip(1).All(x => char.IsLetterOrDigit(x) || x == '_');
    }

    private static string? ResolveHelper()
    {
        var configured = Environment.GetEnvironmentVariable("VSLICES_DOTNET_REFACTOR_HELPER");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return Path.GetFullPath(configured);

        var packaged = Path.Combine(
            AppContext.BaseDirectory,
            "refactor",
            "VSlices.Targets.DotNet.Refactor.dll");
        if (File.Exists(packaged))
            return packaged;

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (!File.Exists(Path.Combine(current.FullName, "tooling.slnx")))
                continue;

            var development = Path.Combine(
                current.FullName,
                "src",
                "VSlices.Targets.DotNet.Refactor",
                "bin",
                "Release",
                "net10.0",
                "VSlices.Targets.DotNet.Refactor.dll");
            return File.Exists(development) ? development : null;
        }

        return null;
    }
}

internal sealed record TypeResolutionClientManifest(
    bool Success,
    IReadOnlyList<TypeResolutionClientEntry> Types,
    IReadOnlyList<TypeResolutionClientDiagnostic> Diagnostics);

internal sealed record TypeResolutionClientEntry(string Name, string Namespace, string FullyQualifiedName);
internal sealed record TypeResolutionClientDiagnostic(string Code, string Message);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TypeResolutionClientManifest))]
internal partial class TypeResolutionJsonContext : JsonSerializerContext;

internal sealed record DotNetTypeResolutionResult(
    IReadOnlyList<string> Imports,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.Count == 0;

    public static DotNetTypeResolutionResult Success(IReadOnlyList<string> imports) =>
        new(imports, []);

    public static DotNetTypeResolutionResult Failure(IEnumerable<VsirDiagnostic> diagnostics) =>
        new([], diagnostics.ToArray());
}
