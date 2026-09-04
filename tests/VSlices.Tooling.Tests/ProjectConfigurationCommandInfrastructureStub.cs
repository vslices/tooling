namespace VSlices.Tooling;

internal static class CommandInfrastructure
{
    public static async Task AtomicWrite(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, content, cancellationToken);
    }
}
