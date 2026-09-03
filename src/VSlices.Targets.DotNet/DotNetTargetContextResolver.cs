using System.Diagnostics;
using VSlices.Vsir;

namespace VSlices.Targets.DotNet;

public sealed record DotNetTargetContext(string? ProjectPath, string Namespace);

public static class DotNetTargetContextResolver
{
    public static async Task<(DotNetTargetContext? Context, VsirDiagnostic? Diagnostic)> Resolve(
        string vsirPath,
        string? namespaceOverride,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(namespaceOverride))
            return (new(null, namespaceOverride), null);

        var directory = Path.GetDirectoryName(vsirPath)!;
        var project = FindProject(directory);
        if (project is null)
        {
            return (null, new(
                "DOTNET001",
                $"Could not find a unique .csproj for '{vsirPath}'. Pass --namespace to override target context explicitly."));
        }

        var probeName = "__VSlicesNamespaceProbe_" + Guid.NewGuid().ToString("N");
        var probePath = Path.Combine(directory, probeName + ".cs");

        try
        {
            var result = await RunDotNet(
                directory,
                cancellationToken,
                "new",
                "class",
                "--name", probeName,
                "--output", directory,
                "--project", project,
                "--no-update-check");

            if (result.ExitCode != 0 || !File.Exists(probePath))
            {
                return (null, new(
                    "DOTNET002",
                    "dotnet new class could not resolve a C# item context for this VSIR. " +
                    "Pass --namespace to override it explicitly. " + result.StandardError.Trim()));
            }

            var source = await File.ReadAllTextAsync(probePath, cancellationToken);
            var namespaceLine = source
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .FirstOrDefault(x => x.StartsWith("namespace ", StringComparison.Ordinal));

            if (namespaceLine is null)
            {
                return (null, new(
                    "DOTNET003",
                    "The .NET item template produced no namespace that VSlices could reuse. Pass --namespace explicitly."));
            }

            var namespaceName = namespaceLine["namespace ".Length..]
                .Trim()
                .TrimEnd(';')
                .Trim();

            if (namespaceName.Length == 0)
            {
                return (null, new(
                    "DOTNET003",
                    "The .NET item template produced an empty namespace. Pass --namespace explicitly."));
            }

            return (new(project, namespaceName), null);
        }
        finally
        {
            if (File.Exists(probePath))
                File.Delete(probePath);
        }
    }

    private static string? FindProject(string startDirectory)
    {
        for (var current = new DirectoryInfo(startDirectory); current is not null; current = current.Parent)
        {
            var projects = current.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);
            if (projects.Length == 1)
                return projects[0].FullName;
            if (projects.Length > 1)
                return null;
        }

        return null;
    }

    private static async Task<ProcessResult> RunDotNet(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        if (process is null)
            return new(-1, string.Empty, "Could not start the dotnet CLI.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new(process.ExitCode, await stdout, await stderr);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
