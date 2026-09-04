using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace VSlices.Tooling;

internal static class SelfUpdater
{
    public static async Task<int> Update(
        string source,
        string channel,
        int? pullRequest,
        bool checkOnly,
        CancellationToken cancellationToken)
    {
        if (!TryResolveGitHubRepository(source, out var owner, out var repository))
        {
            Console.Error.WriteLine(
                $"UPD001: Update source '{source}' is not a supported GitHub repository URL.");
            return 2;
        }

        var normalizedChannel = channel.Trim().ToLowerInvariant();
        if (normalizedChannel is not ("stable" or "preview" or "build"))
        {
            Console.Error.WriteLine(
                $"UPD002: Update channel '{channel}' is not supported. Use 'stable', 'preview', or 'build'.");
            return 2;
        }

        if (normalizedChannel == "build" && pullRequest is null)
        {
            Console.Error.WriteLine(
                "UPD010: The build update channel requires a pull request. Configure 'updates.pull-request' or pass '--pull-request'.");
            return 2;
        }

        var rid = ResolveRuntimeIdentifier();
        if (rid is null)
        {
            Console.Error.WriteLine(
                $"UPD003: Self-update is not supported on {RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture}.");
            return 1;
        }

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("vslices-cli");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            if (normalizedChannel == "build")
            {
                var token = await ResolveGitHubToken(cancellationToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    Console.Error.WriteLine(
                        "UPD011: PR build artifacts require GitHub authentication. Set GH_TOKEN/GITHUB_TOKEN or authenticate with 'gh auth login'.");
                    return 1;
                }

                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return await UpdateFromBuild(
                    http,
                    owner!,
                    repository!,
                    pullRequest!.Value,
                    rid,
                    checkOnly,
                    cancellationToken);
            }

            return await UpdateFromRelease(
                http,
                owner!,
                repository!,
                source,
                normalizedChannel,
                rid,
                checkOnly,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"UPD009: Self-update failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> UpdateFromRelease(
        HttpClient http,
        string owner,
        string repository,
        string source,
        string channel,
        string rid,
        bool checkOnly,
        CancellationToken cancellationToken)
    {
        var release = await ResolveRelease(
            http,
            owner,
            repository,
            channel,
            cancellationToken);

        if (release is null)
        {
            Console.Error.WriteLine(
                $"UPD004: No {channel} VSlices CLI release is available from '{source}'.");
            return 1;
        }

        var currentVersion = CurrentVersion();
        var releaseVersion = NormalizeVersion(release.TagName);
        if (NormalizeVersion(currentVersion) == releaseVersion)
        {
            ShowResolvedUpdate(currentVersion, release.TagName, rid);
            TerminalOutput.BlankLine();
            TerminalOutput.Success("✓ VSlices is up to date");
            return 0;
        }

        var assetName = $"vslices-{rid}.zip";
        var checksumName = assetName + ".sha256";
        var asset = release.Assets.FirstOrDefault(x => x.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));
        var checksum = release.Assets.FirstOrDefault(x => x.Name.Equals(checksumName, StringComparison.OrdinalIgnoreCase));

        if (asset is null || checksum is null)
        {
            Console.Error.WriteLine(
                $"UPD005: Release '{release.TagName}' does not contain '{assetName}' and its SHA-256 checksum.");
            return 1;
        }

        ShowResolvedUpdate(currentVersion, release.TagName, rid);
        TerminalOutput.BlankLine();

        if (checkOnly)
        {
            TerminalOutput.Info($"→ {release.TagName} is available");
            return 0;
        }

        return await InstallArchivePair(
            http,
            asset.DownloadUrl,
            checksum.DownloadUrl,
            assetName,
            release.TagName,
            cancellationToken);
    }

    private static async Task<int> UpdateFromBuild(
        HttpClient http,
        string owner,
        string repository,
        int pullRequest,
        string rid,
        bool checkOnly,
        CancellationToken cancellationToken)
    {
        var run = await ResolveLatestSuccessfulPullRequestRun(
            http,
            owner,
            repository,
            pullRequest,
            cancellationToken);

        if (run is null)
        {
            Console.Error.WriteLine(
                $"UPD012: No successful CI build was found for pull request #{pullRequest}.");
            return 1;
        }

        var buildIdentity = $"build{pullRequest}.{run.RunNumber}";
        var currentIdentity = CurrentBuildIdentity() ?? CurrentVersion();
        if (CurrentBuildIdentity() == buildIdentity)
        {
            ShowResolvedUpdate(currentIdentity, buildIdentity, rid);
            TerminalOutput.BlankLine();
            TerminalOutput.Success("✓ VSlices is up to date");
            return 0;
        }

        var artifactName = $"{buildIdentity}-{rid}";
        var artifact = await ResolveRunArtifact(
            http,
            owner,
            repository,
            run.Id,
            artifactName,
            cancellationToken);

        if (artifact is null)
        {
            Console.Error.WriteLine(
                $"UPD013: CI build '{buildIdentity}' does not contain artifact '{artifactName}'.");
            return 1;
        }

        ShowResolvedUpdate(currentIdentity, buildIdentity, rid);
        TerminalOutput.BlankLine();

        if (checkOnly)
        {
            TerminalOutput.Info($"→ {buildIdentity} is available");
            return 0;
        }

        var staging = Path.Combine(Path.GetTempPath(), "vslices-build-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var artifactArchive = Path.Combine(staging, artifactName + ".zip");
            await TerminalOutput.ProgressAsync(
                $"Downloading {buildIdentity}...",
                () => Download(http, artifact.DownloadUrl, artifactArchive, cancellationToken));

            var artifactContents = Path.Combine(staging, "artifact");
            ZipFile.ExtractToDirectory(artifactArchive, artifactContents);

            var assetName = $"vslices-{rid}.zip";
            var checksumName = assetName + ".sha256";
            var archivePath = Directory
                .EnumerateFiles(artifactContents, assetName, SearchOption.AllDirectories)
                .SingleOrDefault();
            var checksumPath = Directory
                .EnumerateFiles(artifactContents, checksumName, SearchOption.AllDirectories)
                .SingleOrDefault();

            if (archivePath is null || checksumPath is null)
            {
                Console.Error.WriteLine(
                    $"UPD014: Artifact '{artifactName}' must contain '{assetName}' and '{checksumName}'.");
                return 1;
            }

            var checksumValid = false;
            await TerminalOutput.ProgressAsync(
                "Verifying SHA-256 checksum...",
                async () => checksumValid = await VerifyChecksum(archivePath, checksumPath, cancellationToken));

            if (!checksumValid)
            {
                Console.Error.WriteLine("UPD007: Downloaded CLI archive failed SHA-256 verification.");
                return 1;
            }

            TerminalOutput.Success("✓ Checksum verified");

            return await InstallVerifiedArchive(
                archivePath,
                assetName,
                buildIdentity,
                cancellationToken);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static async Task<int> InstallArchivePair(
        HttpClient http,
        string archiveUrl,
        string checksumUrl,
        string assetName,
        string version,
        CancellationToken cancellationToken)
    {
        var staging = Path.Combine(Path.GetTempPath(), "vslices-self-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var archivePath = Path.Combine(staging, assetName);
            var checksumPath = Path.Combine(staging, assetName + ".sha256");

            await TerminalOutput.ProgressAsync(
                $"Downloading {version}...",
                async () =>
                {
                    await Download(http, archiveUrl, archivePath, cancellationToken);
                    await Download(http, checksumUrl, checksumPath, cancellationToken);
                });

            var checksumValid = false;
            await TerminalOutput.ProgressAsync(
                "Verifying SHA-256 checksum...",
                async () => checksumValid = await VerifyChecksum(archivePath, checksumPath, cancellationToken));

            if (!checksumValid)
            {
                Console.Error.WriteLine("UPD007: Downloaded CLI archive failed SHA-256 verification.");
                return 1;
            }

            TerminalOutput.Success("✓ Checksum verified");

            return await InstallVerifiedArchive(
                archivePath,
                assetName,
                version,
                cancellationToken);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static async Task<int> InstallVerifiedArchive(
        string archivePath,
        string assetName,
        string version,
        CancellationToken cancellationToken)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) ||
            !Path.GetFileNameWithoutExtension(executable).Equals("vslices", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                "UPD006: Self-update can only replace the standalone native 'vslices' executable. Use the installation mechanism that provided the current process instead.");
            return 1;
        }

        var extracted = Path.Combine(Path.GetDirectoryName(archivePath)!, "extracted-" + Guid.NewGuid().ToString("N"));
        ZipFile.ExtractToDirectory(archivePath, extracted);

        var binaryName = OperatingSystem.IsWindows() ? "vslices.exe" : "vslices";
        var replacements = Directory
            .EnumerateFiles(extracted, binaryName, SearchOption.AllDirectories)
            .Take(2)
            .ToArray();

        if (replacements.Length != 1)
        {
            Console.Error.WriteLine(
                $"UPD008: Release archive '{assetName}' must contain exactly one '{binaryName}'.");
            return 1;
        }

        var replacement = replacements[0];
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                replacement,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);
        }

        return await ReplaceExecutable(
            executable,
            replacement,
            version,
            cancellationToken);
    }

    private static void ShowResolvedUpdate(string current, string latest, string rid)
    {
        TerminalOutput.Detail("Current", current);
        TerminalOutput.Detail("Latest", latest);
        TerminalOutput.Detail("Runtime", rid);
    }

    private static async Task<GitHubRelease?> ResolveRelease(
        HttpClient http,
        string owner,
        string repository,
        string channel,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repository}/releases?per_page=20";
        await using var stream = await http.GetStreamAsync(url, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        foreach (var releaseElement in document.RootElement.EnumerateArray())
        {
            var draft = releaseElement.TryGetProperty("draft", out var draftValue) && draftValue.GetBoolean();
            var prerelease = releaseElement.TryGetProperty("prerelease", out var prereleaseValue) && prereleaseValue.GetBoolean();
            if (draft || (channel == "stable" && prerelease))
                continue;

            var tagName = releaseElement.TryGetProperty("tag_name", out var tagValue)
                ? tagValue.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(tagName))
                continue;

            var assets = new List<ReleaseAsset>();
            if (releaseElement.TryGetProperty("assets", out var assetsElement))
            {
                foreach (var assetElement in assetsElement.EnumerateArray())
                {
                    var name = assetElement.TryGetProperty("name", out var nameValue)
                        ? nameValue.GetString()
                        : null;
                    var downloadUrl = assetElement.TryGetProperty("browser_download_url", out var urlValue)
                        ? urlValue.GetString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(downloadUrl))
                        assets.Add(new ReleaseAsset(name, downloadUrl));
                }
            }

            return new GitHubRelease(tagName, assets);
        }

        return null;
    }

    private static async Task<WorkflowRun?> ResolveLatestSuccessfulPullRequestRun(
        HttpClient http,
        string owner,
        string repository,
        int pullRequest,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repository}/actions/workflows/ci.yml/runs?event=pull_request&status=success&per_page=100";
        await using var stream = await http.GetStreamAsync(url, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("workflow_runs", out var runs))
            return null;

        foreach (var run in runs.EnumerateArray())
        {
            if (!run.TryGetProperty("pull_requests", out var pullRequests))
                continue;

            var matches = pullRequests.EnumerateArray().Any(pr =>
                pr.TryGetProperty("number", out var number) && number.GetInt32() == pullRequest);
            if (!matches)
                continue;

            if (!run.TryGetProperty("id", out var id) ||
                !run.TryGetProperty("run_number", out var runNumber))
                continue;

            return new WorkflowRun(id.GetInt64(), runNumber.GetInt32());
        }

        return null;
    }

    private static async Task<WorkflowArtifact?> ResolveRunArtifact(
        HttpClient http,
        string owner,
        string repository,
        long runId,
        string artifactName,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{owner}/{repository}/actions/runs/{runId}/artifacts?per_page=100";
        await using var stream = await http.GetStreamAsync(url, cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("artifacts", out var artifacts))
            return null;

        foreach (var artifact in artifacts.EnumerateArray())
        {
            var expired = artifact.TryGetProperty("expired", out var expiredValue) && expiredValue.GetBoolean();
            var name = artifact.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
            var downloadUrl = artifact.TryGetProperty("archive_download_url", out var urlValue) ? urlValue.GetString() : null;
            if (!expired &&
                name?.Equals(artifactName, StringComparison.OrdinalIgnoreCase) == true &&
                !string.IsNullOrWhiteSpace(downloadUrl))
                return new WorkflowArtifact(name, downloadUrl);
        }

        return null;
    }

    private static async Task<string?> ResolveGitHubToken(CancellationToken cancellationToken)
    {
        var environmentToken = Environment.GetEnvironmentVariable("GH_TOKEN")
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(environmentToken))
            return environmentToken.Trim();

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = "auth token",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output.Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task Download(
        HttpClient http,
        string url,
        string path,
        CancellationToken cancellationToken)
    {
        await using var source = await http.GetStreamAsync(url, cancellationToken);
        await using var target = File.Create(path);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task<bool> VerifyChecksum(
        string archivePath,
        string checksumPath,
        CancellationToken cancellationToken)
    {
        var expectedText = await File.ReadAllTextAsync(checksumPath, cancellationToken);
        var expected = expectedText
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(expected))
            return false;

        await using var archive = File.OpenRead(archivePath);
        var actualBytes = await SHA256.HashDataAsync(archive, cancellationToken);
        var actual = Convert.ToHexString(actualBytes);
        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> ReplaceExecutable(
        string current,
        string replacement,
        string version,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.Move(replacement, current, overwrite: true);
            TerminalOutput.Success($"✓ Updated VSlices to {version}");
            return 0;
        }

        var currentDirectory = Path.GetDirectoryName(current)!;
        var pending = Path.Combine(currentDirectory, ".vslices.pending.exe");
        File.Copy(replacement, pending, overwrite: true);

        var script = Path.Combine(Path.GetTempPath(), "vslices-update-" + Guid.NewGuid().ToString("N") + ".ps1");
        var processId = Environment.ProcessId;
        var escapedCurrent = current.Replace("'", "''");
        var escapedPending = pending.Replace("'", "''");
        var escapedScript = script.Replace("'", "''");
        var scriptContent =
            $"while (Get-Process -Id {processId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 100 }}{Environment.NewLine}" +
            $"Move-Item -LiteralPath '{escapedPending}' -Destination '{escapedCurrent}' -Force{Environment.NewLine}" +
            $"Remove-Item -LiteralPath '{escapedScript}' -Force{Environment.NewLine}";

        await File.WriteAllTextAsync(script, scriptContent, cancellationToken);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        TerminalOutput.Success("✓ Update prepared");
        TerminalOutput.BlankLine();
        TerminalOutput.Muted($"{version} will replace the current executable when this process exits.");
        return 0;
    }

    private static string? ResolveRuntimeIdentifier()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        if (architecture is null)
            return null;

        if (OperatingSystem.IsWindows())
            return "win-" + architecture;
        if (OperatingSystem.IsLinux())
            return "linux-" + architecture;
        if (OperatingSystem.IsMacOS())
            return "osx-" + architecture;

        return null;
    }

    private static string CurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    private static string? CurrentBuildIdentity()
    {
        var version = CurrentVersion();
        var marker = "-build.";
        var markerIndex = version.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        var identifiers = version[(markerIndex + marker.Length)..]
            .Split('+')[0]
            .Split('.');
        return identifiers.Length >= 2 &&
               int.TryParse(identifiers[0], out var pullRequest) &&
               int.TryParse(identifiers[1], out var runNumber)
            ? $"build{pullRequest}.{runNumber}"
            : null;
    }

    private static string NormalizeVersion(string value) =>
        value.Trim().TrimStart('v', 'V').Split('+')[0];

    private static bool TryResolveGitHubRepository(string source, out string? owner, out string? repository)
    {
        owner = null;
        repository = null;

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
            return false;

        owner = segments[0];
        repository = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];
        return true;
    }

    private sealed record GitHubRelease(string TagName, IReadOnlyList<ReleaseAsset> Assets);
    private sealed record ReleaseAsset(string Name, string DownloadUrl);
    private sealed record WorkflowRun(long Id, int RunNumber);
    private sealed record WorkflowArtifact(string Name, string DownloadUrl);
}
