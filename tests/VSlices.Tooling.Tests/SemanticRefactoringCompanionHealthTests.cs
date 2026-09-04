namespace VSlices.Tooling.Tests;

public sealed class SemanticRefactoringCompanionHealthTests
{
    [Fact]
    public void Standalone_cli_without_companion_requests_follow_up_update()
    {
        using var temp = TempDirectory.Create();

        var shouldNotify = SemanticRefactoringCompanionHealth.ShouldNotify(
            ["--version"],
            Path.Combine(temp.Path, OperatingSystem.IsWindows() ? "vslices.exe" : "vslices"),
            temp.Path);

        Assert.True(shouldNotify);
    }

    [Fact]
    public void Root_companion_without_msbuild_build_host_is_still_incomplete()
    {
        using var temp = TempDirectory.Create();
        var refactor = Path.Combine(temp.Path, "refactor");
        Directory.CreateDirectory(refactor);
        File.WriteAllText(
            Path.Combine(refactor, "VSlices.Targets.DotNet.Refactor.dll"),
            "test");

        var shouldNotify = SemanticRefactoringCompanionHealth.ShouldNotify(
            ["--version"],
            Path.Combine(temp.Path, OperatingSystem.IsWindows() ? "vslices.exe" : "vslices"),
            temp.Path);

        Assert.True(shouldNotify);
    }

    [Fact]
    public void Complete_companion_suppresses_notification()
    {
        using var temp = TempDirectory.Create();
        var refactor = Path.Combine(temp.Path, "refactor");
        var buildHost = Path.Combine(refactor, "BuildHost-netcore");
        Directory.CreateDirectory(buildHost);
        File.WriteAllText(
            Path.Combine(refactor, "VSlices.Targets.DotNet.Refactor.dll"),
            "test");
        File.WriteAllText(
            Path.Combine(buildHost, "Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll"),
            "test");

        var shouldNotify = SemanticRefactoringCompanionHealth.ShouldNotify(
            ["--version"],
            Path.Combine(temp.Path, OperatingSystem.IsWindows() ? "vslices.exe" : "vslices"),
            temp.Path);

        Assert.False(shouldNotify);
    }

    [Fact]
    public void Self_update_suppresses_notification_because_it_repairs_the_companion()
    {
        using var temp = TempDirectory.Create();

        var shouldNotify = SemanticRefactoringCompanionHealth.ShouldNotify(
            ["update", "--self"],
            Path.Combine(temp.Path, OperatingSystem.IsWindows() ? "vslices.exe" : "vslices"),
            temp.Path);

        Assert.False(shouldNotify);
    }

    [Fact]
    public void Dotnet_hosted_execution_does_not_report_installation_health()
    {
        using var temp = TempDirectory.Create();

        var shouldNotify = SemanticRefactoringCompanionHealth.ShouldNotify(
            ["--version"],
            Path.Combine(temp.Path, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"),
            temp.Path);

        Assert.False(shouldNotify);
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path) => Path = path;

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "vslices-companion-health-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
