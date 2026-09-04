namespace VSlices.Tooling.Tests;

public sealed class SemanticRefactoringSafetyTests
{
    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("n\n")]
    [InlineData("no\n")]
    [InlineData("anything\n")]
    public void Authorization_defaults_to_no(string inputText)
    {
        using var input = new StringReader(inputText);
        using var output = new StringWriter();

        var approved = SemanticRefactoringAuthorization.Confirm(input, output);

        Assert.False(approved);
        Assert.Contains("[y/N]", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("y\n")]
    [InlineData("Y\n")]
    [InlineData("yes\n")]
    [InlineData("YES\n")]
    public void Authorization_accepts_only_explicit_yes(string inputText)
    {
        using var input = new StringReader(inputText);
        using var output = new StringWriter();

        Assert.True(SemanticRefactoringAuthorization.Confirm(input, output));
    }

    [Fact]
    public async Task Transaction_aborts_before_writing_when_any_source_changed_after_planning()
    {
        using var directory = new TemporaryDirectory();
        var first = directory.Write("First.cs", "old-first");
        var second = directory.Write("Second.cs", "old-second");
        var stagedFirst = directory.Write("First.staged.cs", "new-first");
        var stagedSecond = directory.Write("Second.staged.cs", "new-second");

        var firstHash = TransactionalFileWriter.TrySha256(first);
        var secondHash = TransactionalFileWriter.TrySha256(second);
        File.WriteAllText(second, "changed-after-plan");

        var result = await TransactionalFileWriter.Apply([
            new(first, stagedFirst, true, firstHash),
            new(second, stagedSecond, true, secondHash)
        ], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("old-first", File.ReadAllText(first));
        Assert.Equal("changed-after-plan", File.ReadAllText(second));
    }

    [Fact]
    public async Task Transaction_commits_all_staged_files_together_when_preconditions_hold()
    {
        using var directory = new TemporaryDirectory();
        var human = directory.Write("TicketCode.cs", "old-human");
        var dependent = directory.Write("Consumer.cs", "old-dependent");
        var baseline = Path.Combine(directory.Path, "TicketCode.baseline");
        var stagedHuman = directory.Write("TicketCode.staged.cs", "new-human");
        var stagedDependent = directory.Write("Consumer.staged.cs", "new-dependent");
        var stagedBaseline = directory.Write("Baseline.staged", "new-baseline");

        var result = await TransactionalFileWriter.Apply([
            new(human, stagedHuman, true, TransactionalFileWriter.TrySha256(human)),
            new(dependent, stagedDependent, true, TransactionalFileWriter.TrySha256(dependent)),
            new(baseline, stagedBaseline, false, null)
        ], CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal("new-human", File.ReadAllText(human));
        Assert.Equal("new-dependent", File.ReadAllText(dependent));
        Assert.Equal("new-baseline", File.ReadAllText(baseline));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "vslices-semantic-refactor-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string name, string content)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
