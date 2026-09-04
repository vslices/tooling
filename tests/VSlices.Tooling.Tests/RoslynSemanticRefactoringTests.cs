using System.Diagnostics;
using System.Text.Json;

namespace VSlices.Tooling.Tests;

public sealed class RoslynSemanticRefactoringTests
{
    [Fact]
    public async Task Namespace_move_stages_only_references_that_stop_resolving()
    {
        using var fixture = new RoslynFixture();
        fixture.WriteProject();
        fixture.Write(
            "Demo/TicketCode.cs",
            """
            namespace Demo.Domain;

            public sealed class TicketCode
            {
                public TicketCode? Self { get; }
                public sealed class Input;
            }
            """);
        fixture.Write(
            "Demo/Consumer.cs",
            """
            using static Demo.Domain.TicketCode;

            namespace Demo.Consumer;

            public sealed class Consumer
            {
                private readonly Demo.Domain.TicketCode _code = new();
                private readonly Input _input = new();
            }
            """);
        fixture.Write(
            "TicketCode.candidate.cs",
            """
            namespace Demo.Domain.Aggregates;

            public sealed class TicketCode
            {
                public TicketCode? Self { get; }
                public sealed class Input;
            }
            """);

        Assert.Equal(0, (await fixture.Run("restore", fixture.ProjectPath)).ExitCode);

        var helper = fixture.HelperPath;
        var result = await fixture.Run(
            helper,
            "--project", fixture.ProjectPath,
            "--document", fixture.PathOf("Demo/TicketCode.cs"),
            "--candidate", fixture.PathOf("TicketCode.candidate.cs"),
            "--symbol", "TicketCode",
            "--staging", fixture.PathOf("staged"),
            "--manifest", fixture.PathOf("manifest.json"));

        Assert.Equal(0, result.ExitCode);
        using var manifest = JsonDocument.Parse(File.ReadAllText(fixture.PathOf("manifest.json")));
        var root = manifest.RootElement;
        Assert.True(root.GetProperty("Success").GetBoolean());
        Assert.True(root.GetProperty("RequiresAuthorization").GetBoolean());
        Assert.Equal(2, root.GetProperty("ReferenceCount").GetInt32());

        var files = root.GetProperty("Files").EnumerateArray().ToArray();
        Assert.Equal(2, files.Length);

        var stagedTicketCode = files.Single(x =>
            x.GetProperty("Path").GetString()!.EndsWith("TicketCode.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, stagedTicketCode.GetProperty("ReferenceCount").GetInt32());
        var stagedTicketCodeText = File.ReadAllText(stagedTicketCode.GetProperty("StagedPath").GetString()!);
        Assert.Contains("namespace Demo.Domain.Aggregates;", stagedTicketCodeText, StringComparison.Ordinal);
        Assert.Contains("TicketCode? Self", stagedTicketCodeText, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Demo.Domain.Aggregates.TicketCode? Self", stagedTicketCodeText, StringComparison.Ordinal);

        var stagedConsumer = files.Single(x =>
            x.GetProperty("Path").GetString()!.EndsWith("Consumer.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, stagedConsumer.GetProperty("ReferenceCount").GetInt32());
        var stagedConsumerText = File.ReadAllText(stagedConsumer.GetProperty("StagedPath").GetString()!);
        Assert.Contains("global::Demo.Domain.Aggregates.TicketCode", stagedConsumerText, StringComparison.Ordinal);

        Assert.Contains("namespace Demo.Domain;", File.ReadAllText(fixture.PathOf("Demo/TicketCode.cs")), StringComparison.Ordinal);
        Assert.Contains("Demo.Domain.TicketCode", File.ReadAllText(fixture.PathOf("Demo/Consumer.cs")), StringComparison.Ordinal);
    }

    private sealed class RoslynFixture : IDisposable
    {
        private readonly string _repositoryRoot;

        public RoslynFixture()
        {
            Root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "vslices-roslyn-refactor-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            _repositoryRoot = FindRepositoryRoot();
        }

        public string Root { get; }
        public string ProjectPath => PathOf("Demo/Demo.csproj");
        public string HelperPath => System.IO.Path.Combine(
            _repositoryRoot,
            "src",
            "VSlices.Targets.DotNet.Refactor",
            "bin",
            "Release",
            "net10.0",
            "VSlices.Targets.DotNet.Refactor.dll");

        public void WriteProject()
        {
            Write(
                "Demo/Demo.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """);
            Write(
                "SemanticRefactor.slnx",
                """
                <Solution>
                  <Project Path="Demo/Demo.csproj" />
                </Solution>
                """);
        }

        public string Write(string relativePath, string content)
        {
            var path = PathOf(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public string PathOf(string relativePath) =>
            System.IO.Path.Combine(Root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

        public async Task<ProcessResult> Run(params string[] arguments)
        {
            Assert.True(File.Exists(HelperPath), $"Expected built helper at '{HelperPath}'.");

            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet process.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new(process.ExitCode, await stdout, await stderr);
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(System.IO.Path.Combine(current.FullName, "tooling.slnx")))
                    return current.FullName;
                current = current.Parent;
            }

            throw new InvalidOperationException("Could not locate tooling.slnx from test output.");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
