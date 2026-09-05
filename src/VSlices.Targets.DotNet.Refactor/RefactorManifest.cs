using System.Text;
using System.Text.Json;

namespace VSlices.Targets.DotNet.Refactor;

internal sealed record RefactorFile(
    string Path,
    string StagedPath,
    string OriginalSha256,
    int ReferenceCount);

internal sealed record RefactorDiagnostic(string Code, string Message);

internal sealed record RefactorManifest(
    bool Success,
    bool NamespaceChanged,
    bool RequiresAuthorization,
    string? PreviousSymbol,
    string? NextSymbol,
    int ReferenceCount,
    IReadOnlyList<RefactorFile> Files,
    IReadOnlyList<RefactorDiagnostic> Diagnostics)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static RefactorManifest SuccessNoMove(string symbolName, string namespaceName)
    {
        var display = namespaceName.Length == 0 ? symbolName : namespaceName + "." + symbolName;
        return new(true, false, false, display, display, 0, [], []);
    }

    public static async Task WriteFailure(string path, string code, string message) =>
        await Write(path, new(false, false, false, null, null, 0, [], [new(code, message)]));

    public static async Task Write(string path, RefactorManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(manifest, JsonOptions),
            new UTF8Encoding(false));
    }
}
