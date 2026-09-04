using System.Security.Cryptography;

namespace VSlices.Tooling;

internal sealed record TransactionalFileChange(
    string Path,
    string StagedPath,
    bool ExpectedExists,
    string? ExpectedSha256);

internal static class TransactionalFileWriter
{
    public static async Task<(bool Success, string? Error)> Apply(
        IReadOnlyList<TransactionalFileChange> changes,
        CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            var exists = File.Exists(change.Path);
            if (exists != change.ExpectedExists)
            {
                return (false,
                    $"'{change.Path}' changed after the semantic refactoring plan was computed. No files were modified.");
            }

            if (exists && !string.Equals(
                    Sha256(change.Path),
                    change.ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return (false,
                    $"'{change.Path}' changed after the semantic refactoring plan was computed. No files were modified.");
            }
        }

        var transactionId = Guid.NewGuid().ToString("N");
        var prepared = new List<PreparedChange>();

        try
        {
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Path.GetDirectoryName(change.Path)!;
                Directory.CreateDirectory(directory);

                var candidate = Path.Combine(
                    directory,
                    $".{Path.GetFileName(change.Path)}.{transactionId}.candidate");
                var backup = Path.Combine(
                    directory,
                    $".{Path.GetFileName(change.Path)}.{transactionId}.backup");

                File.Copy(change.StagedPath, candidate, overwrite: true);
                if (change.ExpectedExists)
                    File.Copy(change.Path, backup, overwrite: true);

                prepared.Add(new(change, candidate, backup));
            }
        }
        catch (Exception ex)
        {
            CleanupBestEffort(prepared, includeBackups: true);
            return (false, $"Transactional semantic refactoring could not be prepared: {ex.Message}");
        }

        var committed = new List<PreparedChange>();
        try
        {
            foreach (var item in prepared)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(item.CandidatePath, item.Change.Path, overwrite: true);
                committed.Add(item);
            }
        }
        catch (Exception commitException)
        {
            var rollbackErrors = Rollback(committed);
            if (rollbackErrors.Count == 0)
            {
                CleanupBestEffort(prepared, includeBackups: true);
                return (false,
                    $"Transactional semantic refactoring failed and was rolled back: {commitException.Message}");
            }

            CleanupBestEffort(prepared, includeBackups: false);
            var retainedBackups = prepared
                .Where(x => File.Exists(x.BackupPath))
                .Select(x => x.BackupPath)
                .ToArray();
            var recovery = retainedBackups.Length == 0
                ? string.Empty
                : $" Recovery backups were retained at: {string.Join(", ", retainedBackups)}.";
            return (false,
                "Transactional semantic refactoring failed and rollback was incomplete. " +
                $"Commit error: {commitException.Message}. " +
                $"Rollback errors: {string.Join(" | ", rollbackErrors)}.{recovery}");
        }

        CleanupBestEffort(prepared, includeBackups: true);
        await Task.CompletedTask;
        return (true, null);
    }

    public static string? TrySha256(string path) =>
        File.Exists(path) ? Sha256(path) : null;

    private static IReadOnlyList<string> Rollback(IReadOnlyList<PreparedChange> committed)
    {
        var errors = new List<string>();
        foreach (var item in committed.Reverse())
        {
            try
            {
                if (item.Change.ExpectedExists)
                {
                    if (!File.Exists(item.BackupPath))
                    {
                        errors.Add($"Missing backup for '{item.Change.Path}'");
                        continue;
                    }

                    File.Move(item.BackupPath, item.Change.Path, overwrite: true);
                }
                else if (File.Exists(item.Change.Path))
                {
                    File.Delete(item.Change.Path);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Change.Path}: {ex.Message}");
            }
        }

        return errors;
    }

    private static void CleanupBestEffort(
        IEnumerable<PreparedChange> prepared,
        bool includeBackups)
    {
        foreach (var item in prepared)
        {
            TryDeleteFile(item.CandidatePath);
            if (includeBackups)
                TryDeleteFile(item.BackupPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Cleanup artifacts must not reinterpret an already committed or rolled-back transaction.
        }
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record PreparedChange(
        TransactionalFileChange Change,
        string CandidatePath,
        string BackupPath);
}
