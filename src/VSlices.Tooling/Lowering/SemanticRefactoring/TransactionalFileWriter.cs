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
            catch
            {
                foreach (var item in committed.AsEnumerable().Reverse())
                {
                    if (item.Change.ExpectedExists && File.Exists(item.BackupPath))
                        File.Move(item.BackupPath, item.Change.Path, overwrite: true);
                    else if (!item.Change.ExpectedExists && File.Exists(item.Change.Path))
                        File.Delete(item.Change.Path);
                }
                throw;
            }

            foreach (var item in prepared)
            {
                if (File.Exists(item.BackupPath))
                    File.Delete(item.BackupPath);
                if (File.Exists(item.CandidatePath))
                    File.Delete(item.CandidatePath);
            }

            await Task.CompletedTask;
            return (true, null);
        }
        catch (Exception ex)
        {
            foreach (var item in prepared)
            {
                if (File.Exists(item.CandidatePath))
                    File.Delete(item.CandidatePath);
                if (File.Exists(item.BackupPath))
                    File.Delete(item.BackupPath);
            }

            return (false, $"Transactional semantic refactoring failed: {ex.Message}");
        }
    }

    public static string? TrySha256(string path) =>
        File.Exists(path) ? Sha256(path) : null;

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record PreparedChange(
        TransactionalFileChange Change,
        string CandidatePath,
        string BackupPath);
}
