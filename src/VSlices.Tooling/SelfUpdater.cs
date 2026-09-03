using System.Diagnostics;
using System.IO.Compression;
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
        if (normalizedChannel is not ("stable" or "preview"))
        {
            Console.Error.WriteLine(
                $"UPD002: Update channel '{channel}' is not supported. Use 'stable' or 'preview'.");
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

            var release = await ResolveRelease(
                http,
                owner!,
                repository!,
                normalizedChannel,
                cancellationToken);

            if (release is null)
            {
                Console.Error.WriteLine(
                    $"UPD004: No {normalizedChannel} VSlices CLI release is available from '{source}'.");
                return 1;
            }

            var currentVersion = CurrentVersion();
            var releaseVersion = NormalizeVersion(release.TagName);
            if (NormalizeVersion(currentVersion) == releaseVersion)
            {
                Console.WriteLine($"VSlices CLI is already up to date ({release.TagName}).");
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

            Console.WriteLine(
                $"VSlices CLI update available: {currentVersion} -> {release.TagName} ({normalizedChannel}, {rid}).");

            if (checkOnly)
                return 0;

            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable) ||
                !Path.GetFileNameWithoutExtension(executable).Equals("vslices", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    "UPD006: Self-update can only replace the standalone native 'vslices' executable. Use the installation mechanism that provided the current process instead.");
                return 1;
            }

            var staging = Path.Combine(Path.GetTempPath(), "vslices-self-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            try
            {
                var archivePath = Path.Combine(staging, assetName);
                var checksumPath = Path.Combine(staging, checksumName);
                await Download(http, asset.DownloadUrl, archivePath, cancellationToken);
                await Download(http, checksum.DownloadUrl, checksumPath, cancellationToken);

                if (!await VerifyChecksum(archivePath, checksumPath, cancellationToken))
                {
                    Console.Error.WriteLine("UPD007: Downloaded CLI archive failed SHA-256 verification.");
                    return 1;
                }

                var extracted = Path.Combine(staging, "extracted");
                ZipFile.ExtractToDirectory(archivePath, extracted);

                var binaryName = OperatingSystem.IsWindows() ? "vslices.exe" : "vslices";
                var replacement = Directory
                    .EnumerateFiles(extracted, binaryName, SearchOption.AllDirectories)
                    .SingleOrDefault();

                if (replacement is null)
                {
                    Console.Error.WriteLine(
                        $"UPD008: Release archive '{assetName}' does not contain exactly one '{binaryName}'.");
                    return 1;
                }

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
                    release.TagName,
                    cancellationToken);
            }
            finally
            {
                if (!OperatingSystem.IsWindows() && Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"UPD009: Self-update failed: {ex.Message}");
            return 1;
        }
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
        var releases = await JsonSerializer.DeserializeAsync<GitHubReleaseDto[]>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        var candidate = releases?
            .Where(x => !x.Draft)
            .Where(x => channel == "preview" || !x.Prerelease)
            .FirstOrDefault();

        return candidate is null
            ? null
            : new GitHubRelease(
                candidate.TagName ?? "unknown",
                candidate.Assets?
                    .Where(x => x.Name is not null && x.BrowserDownloadUrl is not null)
                    .Select(x => new ReleaseAsset(x.Name!, x.BrowserDownloadUrl!))
                    .ToArray() ?? []);
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
            Console.WriteLine($"Updated VSlices CLI to {version}.");
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

        await File.WriteAllTextAsync(
            script,
            $"""
            while (Get-Process -Id {processId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 100 }}
            Move-Item -LiteralPath '{escapedPending}' -Destination '{escapedCurrent}' -Force
            Remove-Item -LiteralPath '{escapedScript}' -Force
            """,
            cancellationToken);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Console.WriteLine($"VSlices CLI {version} is ready and will replace the current executable after this process exits.");
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

    private sealed class GitHubReleaseDto
    {
        public string? TagName { get; set; }
        public bool Draft { get; set; }
        public bool Prerelease { get; set; }
        public GitHubAssetDto[]? Assets { get; set; }
    }

    private sealed class GitHubAssetDto
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
    }
}
