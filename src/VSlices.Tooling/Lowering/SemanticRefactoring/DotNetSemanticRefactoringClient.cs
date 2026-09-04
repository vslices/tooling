using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VSlices.Vsir;

namespace VSlices.Tooling;

internal static class DotNetSemanticRefactoringClient
{
    public static async Task<DotNetSemanticRefactoringPlan?> TryPlanNamespaceMove(
        TranspilationResult next,
        string humanPath,
        string humanSource,
        string candidateSource,
        CancellationToken cancellationToken)
    {
        if (!TryExtractFileScopedNamespace(humanSource, out var previousNamespace) ||
            !TryExtractFileScopedNamespace(candidateSource, out var nextNamespace) ||
            string.Equals(previousNamespace, nextNamespace, StringComparison.Ordinal))
        {
            return null;
        }

        if (next.TargetContext?.ProjectPath is null)
        {
            return DotNetSemanticRefactoringPlan.Failure([
                new(
                    "DOTNET020",
                    "The rebased materialization moves a C# namespace, but no related .csproj is available for semantic blast-radius analysis. No files were modified.")
            ]);
        }

        var helper = ResolveHelper();
        if (helper is null)
        {
            return DotNetSemanticRefactoringPlan.Failure([
                new(
                    "DOTNET021",
                    "The Roslyn semantic-refactoring helper is not installed beside the VSlices CLI. No files were modified.")
            ]);
        }

        var transactionRoot = Path.Combine(
            Path.GetTempPath(),
            "vslices-semantic-refactor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(transactionRoot);

        try
        {
            var candidatePath = Path.Combine(transactionRoot, "candidate.cs");
            var stagingPath = Path.Combine(transactionRoot, "staged");
            var manifestPath = Path.Combine(transactionRoot, "manifest.json");
            await File.WriteAllTextAsync(
                candidatePath,
                candidateSource,
                new UTF8Encoding(false),
                cancellationToken);

            var symbolName = Path.GetFileNameWithoutExtension(next.VsirPath!);
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(helper);
            Add("--project", next.TargetContext.ProjectPath);
            Add("--document", humanPath);
            Add("--candidate", candidatePath);
            Add("--symbol", symbolName);
            Add("--staging", stagingPath);
            Add("--manifest", manifestPath);

            var analysisStarted = Stopwatch.GetTimestamp();
            if (!Console.IsErrorRedirected)
                Console.Error.WriteLine("Analyzing semantic blast radius with Roslyn...");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Directory.Delete(transactionRoot, recursive: true);
                return DotNetSemanticRefactoringPlan.Failure([
                    new("DOTNET022", "Could not start the Roslyn semantic-refactoring helper.")
                ]);
            }

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var standardOutput = await stdout;
            var standardError = await stderr;

            if (!Console.IsErrorRedirected)
            {
                var elapsed = Stopwatch.GetElapsedTime(analysisStarted);
                Console.Error.WriteLine($"Roslyn semantic analysis finished in {elapsed.TotalSeconds:0.0}s.");
            }

            if (!File.Exists(manifestPath))
            {
                Directory.Delete(transactionRoot, recursive: true);
                var detail = string.Join(
                    " ",
                    new[] { standardError.Trim(), standardOutput.Trim() }
                        .Where(x => x.Length > 0));
                return DotNetSemanticRefactoringPlan.Failure([
                    new(
                        "DOTNET023",
                        "Roslyn semantic-refactoring helper did not produce a plan." +
                        (detail.Length == 0 ? string.Empty : " " + detail))
                ]);
            }

            var manifestText = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize(
                manifestText,
                SemanticRefactorJsonContext.Default.SemanticRefactorManifest);
            if (manifest is null)
            {
                Directory.Delete(transactionRoot, recursive: true);
                return DotNetSemanticRefactoringPlan.Failure([
                    new("DOTNET024", "Roslyn semantic-refactoring helper returned an invalid plan manifest.")
                ]);
            }

            if (!manifest.Success)
            {
                Directory.Delete(transactionRoot, recursive: true);
                return DotNetSemanticRefactoringPlan.Failure(
                    manifest.Diagnostics.Select(x => new VsirDiagnostic(x.Code, x.Message)));
            }

            return new(
                transactionRoot,
                manifest.NamespaceChanged,
                manifest.RequiresAuthorization,
                manifest.PreviousSymbol,
                manifest.NextSymbol,
                manifest.ReferenceCount,
                manifest.Files.Select(x => new DotNetSemanticRefactoringFile(
                    x.Path,
                    x.StagedPath,
                    x.OriginalSha256,
                    x.ReferenceCount)).ToArray(),
                []);

            void Add(string name, string value)
            {
                startInfo.ArgumentList.Add(name);
                startInfo.ArgumentList.Add(value);
            }
        }
        catch
        {
            if (Directory.Exists(transactionRoot))
                Directory.Delete(transactionRoot, recursive: true);
            throw;
        }
    }

    private static bool TryExtractFileScopedNamespace(string source, out string namespaceName)
    {
        using var reader = new StringReader(source);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("namespace ", StringComparison.Ordinal) ||
                !trimmed.EndsWith(';'))
                continue;

            namespaceName = trimmed["namespace ".Length..^1].Trim();
            return namespaceName.Length > 0;
        }

        namespaceName = string.Empty;
        return false;
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

internal sealed record SemanticRefactorManifest(
    bool Success,
    bool NamespaceChanged,
    bool RequiresAuthorization,
    string? PreviousSymbol,
    string? NextSymbol,
    int ReferenceCount,
    IReadOnlyList<SemanticRefactorFileManifest> Files,
    IReadOnlyList<SemanticRefactorDiagnosticManifest> Diagnostics);

internal sealed record SemanticRefactorFileManifest(
    string Path,
    string StagedPath,
    string OriginalSha256,
    int ReferenceCount);

internal sealed record SemanticRefactorDiagnosticManifest(string Code, string Message);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SemanticRefactorManifest))]
internal partial class SemanticRefactorJsonContext : JsonSerializerContext;

internal sealed record DotNetSemanticRefactoringFile(
    string Path,
    string StagedPath,
    string OriginalSha256,
    int ReferenceCount);

internal sealed class DotNetSemanticRefactoringPlan : IDisposable
{
    public DotNetSemanticRefactoringPlan(
        string? transactionRoot,
        bool namespaceChanged,
        bool requiresAuthorization,
        string? previousSymbol,
        string? nextSymbol,
        int referenceCount,
        IReadOnlyList<DotNetSemanticRefactoringFile> files,
        IReadOnlyList<VsirDiagnostic> diagnostics)
    {
        TransactionRoot = transactionRoot;
        NamespaceChanged = namespaceChanged;
        RequiresAuthorization = requiresAuthorization;
        PreviousSymbol = previousSymbol;
        NextSymbol = nextSymbol;
        ReferenceCount = referenceCount;
        Files = files;
        Diagnostics = diagnostics;
    }

    public string? TransactionRoot { get; }
    public bool NamespaceChanged { get; }
    public bool RequiresAuthorization { get; }
    public string? PreviousSymbol { get; }
    public string? NextSymbol { get; }
    public int ReferenceCount { get; }
    public IReadOnlyList<DotNetSemanticRefactoringFile> Files { get; }
    public IReadOnlyList<VsirDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Diagnostics.Count == 0;

    public static DotNetSemanticRefactoringPlan Failure(IEnumerable<VsirDiagnostic> diagnostics) =>
        new(null, false, false, null, null, 0, [], diagnostics.ToArray());

    public void Dispose()
    {
        if (TransactionRoot is not null && Directory.Exists(TransactionRoot))
            Directory.Delete(TransactionRoot, recursive: true);
    }
}
