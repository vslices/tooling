using System.Text.Json;

namespace VSlices.Targets.DotNet.Refactor;

internal sealed record TypeResolutionManifest(
    bool Success,
    IReadOnlyList<TypeResolutionEntry> Types,
    IReadOnlyList<TypeResolutionDiagnostic> Diagnostics);

internal sealed record TypeResolutionEntry(string Name, string Namespace, string FullyQualifiedName);
internal sealed record TypeResolutionDiagnostic(string Code, string Message);

internal static class TypeResolutionManifestWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static Task WriteSuccess(
        string path,
        IReadOnlyList<TypeResolutionEntry> types,
        CancellationToken cancellationToken) =>
        Write(path, new(true, types, []), cancellationToken);

    public static Task WriteFailure(
        string path,
        string code,
        string message,
        CancellationToken cancellationToken) =>
        Write(
            path,
            new(false, [], [new TypeResolutionDiagnostic(code, message)]),
            cancellationToken);

    private static async Task Write(
        string path,
        TypeResolutionManifest manifest,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, Options, cancellationToken);
    }
}
