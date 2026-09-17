using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record DocsStandardSnapshotPreparationResult(
    bool IsSuccess,
    string? Error)
{
    public static DocsStandardSnapshotPreparationResult Success() => new(true, null);
    public static DocsStandardSnapshotPreparationResult Failure(string error) => new(false, error);
}

internal static class DocsStandardSnapshotInstaller
{
    public static DocsStandardSnapshotPreparationResult Prepare(
        string materializedRoot,
        string preparedRoot)
    {
        var validation = DocsStandardCatalog.Load(materializedRoot);
        if (!validation.IsSuccess)
            return DocsStandardSnapshotPreparationResult.Failure(validation.Error!);

        if (Directory.Exists(preparedRoot))
            Directory.Delete(preparedRoot, recursive: true);
        Directory.CreateDirectory(preparedRoot);

        var manifestPath = Path.Combine(materializedRoot, "manifest.yaml");
        File.Copy(manifestPath, Path.Combine(preparedRoot, "manifest.yaml"), overwrite: true);

        foreach (var relativePath in ReadDocumentPaths(manifestPath))
        {
            var sourcePath = Path.GetFullPath(Path.Combine(materializedRoot, relativePath));
            var targetPath = Path.GetFullPath(Path.Combine(preparedRoot, relativePath));

            if (!IsContained(materializedRoot, sourcePath) || !IsContained(preparedRoot, targetPath))
            {
                return DocsStandardSnapshotPreparationResult.Failure(
                    $"DOCS027: Document definition path '{relativePath}' escapes the Docs Standard snapshot root.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        var preparedValidation = DocsStandardCatalog.Load(preparedRoot);
        if (!preparedValidation.IsSuccess)
            return DocsStandardSnapshotPreparationResult.Failure(preparedValidation.Error!);

        return DocsStandardSnapshotPreparationResult.Success();
    }

    public static void Replace(string vslicesRoot, string preparedRoot)
    {
        Directory.CreateDirectory(vslicesRoot);
        var target = Path.Combine(vslicesRoot, "docs-standard");
        var backup = Path.Combine(vslicesRoot, ".docs-standard-backup-" + Guid.NewGuid().ToString("N"));

        if (Directory.Exists(target))
            Directory.Move(target, backup);

        try
        {
            Directory.Move(preparedRoot, target);
            if (Directory.Exists(backup))
                Directory.Delete(backup, recursive: true);
        }
        catch
        {
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);
            if (Directory.Exists(backup))
                Directory.Move(backup, target);
            throw;
        }
        finally
        {
            if (Directory.Exists(backup) && Directory.Exists(target))
                Directory.Delete(backup, recursive: true);
        }
    }

    private static IReadOnlyList<string> ReadDocumentPaths(string manifestPath)
    {
        using var reader = File.OpenText(manifestPath);
        var yaml = new YamlStream();
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var documents = (YamlSequenceNode)root.Children[new YamlScalarNode("documents")];
        return documents.Children
            .Cast<YamlScalarNode>()
            .Select(node => node.Value!)
            .ToArray();
    }

    private static bool IsContained(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return !Path.IsPathRooted(relative) &&
               !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }
}
