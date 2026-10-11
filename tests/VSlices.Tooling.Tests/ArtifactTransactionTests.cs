namespace VSlices.Tooling.Tests;

public sealed class ArtifactTransactionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_second_commit_failure_restores_the_first_artifact_or_removes_its_new_file(bool existed)
    {
        using var project = new ToolingTestProject();
        var first = Path.Combine(project.Root, "first.md");
        var blocked = Path.Combine(project.Root, "blocked.md");
        var stagedFirst = Path.Combine(project.Root, "first.stage");
        var stagedSecond = Path.Combine(project.Root, "second.stage");
        var original = new byte[] { 239, 187, 191, 35, 32, 65, 13, 10 };
        if (existed) File.WriteAllBytes(first, original);
        File.WriteAllText(stagedFirst, "updated first");
        File.WriteAllText(stagedSecond, "new second");
        Directory.CreateDirectory(blocked);
        var result = await TransactionalFileWriter.Apply([
            new(first, stagedFirst, existed, TransactionalFileWriter.TrySha256(first)),
            new(blocked, stagedSecond, false, null)
        ], CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("rolled back", result.Error!, StringComparison.OrdinalIgnoreCase);
        if (existed) Assert.Equal(original, File.ReadAllBytes(first));
        else Assert.False(File.Exists(first));
        Assert.True(Directory.Exists(blocked));
        Assert.Empty(Directory.GetFiles(project.Root, "*.candidate", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(project.Root, "*.backup", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Stale_source_hash_prevents_both_writes()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "source.md");
        var target = Path.Combine(project.Root, "target.md");
        var stage = Path.Combine(project.Root, "staged.md");
        File.WriteAllText(source, "original");
        var previous = TransactionalFileWriter.TrySha256(source);
        File.WriteAllText(source, "concurrent edit");
        File.WriteAllText(stage, "prepared relation");
        var result = await TransactionalFileWriter.Apply([
            new(source, stage, true, previous), new(target, stage, false, null)
        ], CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal("concurrent edit", File.ReadAllText(source));
        Assert.False(File.Exists(target));
    }

    [Fact]
    public async Task Cancellation_before_preparation_preserves_the_existing_artifact()
    {
        using var project = new ToolingTestProject();
        var source = Path.Combine(project.Root, "source.md");
        var target = Path.Combine(project.Root, "target.md");
        var stage = Path.Combine(project.Root, "staged.md");
        File.WriteAllText(source, "original");
        File.WriteAllText(stage, "prepared relation");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await TransactionalFileWriter.Apply([
            new(source, stage, true, TransactionalFileWriter.TrySha256(source)), new(target, stage, false, null)
        ], cancellation.Token);
        Assert.False(result.Success);
        Assert.Equal("original", File.ReadAllText(source));
        Assert.False(File.Exists(target));
        Assert.Empty(Directory.GetFiles(project.Root, "*.candidate", SearchOption.AllDirectories));
    }
}
