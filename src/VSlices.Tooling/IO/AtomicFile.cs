namespace VSlices.Tooling;

/// <summary>
/// Shared filesystem primitive for replace-in-place text writes. This is not a
/// command concern: project configuration, authoring and command output may all
/// rely on the same atomic temporary-file replacement guarantee.
/// </summary>
internal static class AtomicFile
{
    public static async Task WriteTextAsync(
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
}
