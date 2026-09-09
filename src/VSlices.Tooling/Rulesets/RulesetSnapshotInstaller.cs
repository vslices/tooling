using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Tooling;

internal sealed record RulesetSnapshotPreparationResult(
    bool IsSuccess,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public static RulesetSnapshotPreparationResult Success() => new(true, []);
    public static RulesetSnapshotPreparationResult Failure(IEnumerable<VsirDiagnostic> diagnostics) =>
        new(false, diagnostics.ToArray());
}

internal static class RulesetSnapshotInstaller
{
    public static RulesetSnapshotPreparationResult Prepare(
        string materializedRoot,
        string target,
        string preparedRoot)
    {
        var manifestPath = Path.Combine(materializedRoot, "manifest.yaml");
        if (!File.Exists(manifestPath))
        {
            return RulesetSnapshotPreparationResult.Failure([new(
                "RSI001",
                $"Ruleset source '{materializedRoot}' does not contain manifest.yaml.")]);
        }

        var sourceTarget = Path.Combine(materializedRoot, target);
        if (!Directory.Exists(sourceTarget))
        {
            return RulesetSnapshotPreparationResult.Failure([new(
                "RSI002",
                $"Ruleset source does not contain target '{target}'.")]);
        }

        if (Directory.Exists(preparedRoot))
            Directory.Delete(preparedRoot, recursive: true);
        Directory.CreateDirectory(preparedRoot);

        CopyRootFiles(materializedRoot, preparedRoot);
        CopyDirectory(sourceTarget, Path.Combine(preparedRoot, target));

        if (target == "csharp")
        {
            var validation = CSharpLoweringRuleSet.Load(preparedRoot);
            if (!validation.IsSuccess)
                return RulesetSnapshotPreparationResult.Failure(validation.Diagnostics);
        }
        else
        {
            return RulesetSnapshotPreparationResult.Failure([new(
                "RSI003",
                $"Target '{target}' has no complete snapshot validator.")]);
        }

        return RulesetSnapshotPreparationResult.Success();
    }

    public static void Replace(string vslicesRoot, string preparedRoot)
    {
        Directory.CreateDirectory(vslicesRoot);
        var rulesetTarget = Path.Combine(vslicesRoot, "ruleset");
        var backup = Path.Combine(vslicesRoot, ".ruleset-backup-" + Guid.NewGuid().ToString("N"));

        if (Directory.Exists(rulesetTarget))
            Directory.Move(rulesetTarget, backup);

        try
        {
            Directory.Move(preparedRoot, rulesetTarget);
            if (Directory.Exists(backup))
                Directory.Delete(backup, recursive: true);
        }
        catch
        {
            if (Directory.Exists(rulesetTarget))
                Directory.Delete(rulesetTarget, recursive: true);
            if (Directory.Exists(backup))
                Directory.Move(backup, rulesetTarget);
            throw;
        }
        finally
        {
            if (Directory.Exists(backup) && Directory.Exists(rulesetTarget))
                Directory.Delete(backup, recursive: true);
        }
    }

    private static void CopyRootFiles(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);

        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
}
