param(
    [string]$Version,
    [ValidateSet('stable', 'preview')]
    [string]$Channel = 'preview',
    [string]$InstallPath = "$HOME\.vslices\bin",
    [switch]$SkipPath
)

$ErrorActionPreference = 'Stop'

$repository = 'vslices/tooling'

function Resolve-RuntimeIdentifier {
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture

    switch ($architecture) {
        ([System.Runtime.InteropServices.Architecture]::X64) { return 'win-x64' }
        ([System.Runtime.InteropServices.Architecture]::Arm64) { return 'win-arm64' }
        default { throw "This installer does not currently support Windows architecture '$architecture'." }
    }
}

function Get-Releases {
    $headers = @{
        Accept = 'application/vnd.github+json'
        'User-Agent' = 'vslices-install-script'
    }

    Invoke-RestMethod `
        -Uri "https://api.github.com/repos/$repository/releases?per_page=20" `
        -Headers $headers
}

function Resolve-Release {
    param([object[]]$Releases)

    if ($Version) {
        $normalized = $Version.TrimStart('v')
        return $Releases |
            Where-Object { -not $_.draft -and $_.tag_name.TrimStart('v') -eq $normalized } |
            Select-Object -First 1
    }

    return $Releases |
        Where-Object {
            -not $_.draft -and
            ($Channel -eq 'preview' -or -not $_.prerelease)
        } |
        Select-Object -First 1
}

function Add-ToUserPath {
    param([string]$Entry)

    $normalizedEntry = [System.IO.Path]::GetFullPath($Entry).TrimEnd('\')
    $current = [Environment]::GetEnvironmentVariable('Path', 'User')
    $entries = @($current -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    $alreadyPresent = $entries | Where-Object {
        try {
            [System.IO.Path]::GetFullPath($_).TrimEnd('\') -ieq $normalizedEntry
        }
        catch {
            $_.TrimEnd('\') -ieq $normalizedEntry
        }
    }

    if (-not $alreadyPresent) {
        $newPath = if ([string]::IsNullOrWhiteSpace($current)) {
            $normalizedEntry
        }
        else {
            "$current;$normalizedEntry"
        }

        [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
    }

    if (($env:Path -split ';') -notcontains $normalizedEntry) {
        $env:Path = "$env:Path;$normalizedEntry"
    }
}

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') {
    throw 'This installer currently supports Windows only.'
}

$rid = Resolve-RuntimeIdentifier
$assetName = "vslices-$rid.zip"
$checksumName = "$assetName.sha256"

Write-Host 'Installing VSlices Tooling...'
Write-Host "Runtime: $rid"
Write-Host "Channel: $Channel"
Write-Host "Install path: $InstallPath"

$releases = @(Get-Releases)
$release = Resolve-Release -Releases $releases
if (-not $release) {
    if ($Version) {
        throw "VSlices Tooling version '$Version' was not found."
    }

    throw "No '$Channel' VSlices Tooling release is currently available."
}

$asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
$checksumAsset = $release.assets | Where-Object { $_.name -eq $checksumName } | Select-Object -First 1
if (-not $asset -or -not $checksumAsset) {
    throw "Release '$($release.tag_name)' does not contain the expected $rid artifacts."
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("vslices-install-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temp | Out-Null

try {
    $archivePath = Join-Path $temp $assetName
    $checksumPath = Join-Path $temp $checksumName
    $extractPath = Join-Path $temp 'extracted'

    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archivePath
    Invoke-WebRequest -Uri $checksumAsset.browser_download_url -OutFile $checksumPath

    $expected = (Get-Content $checksumPath -Raw).Trim().Split([char[]]" `t`r`n", [System.StringSplitOptions]::RemoveEmptyEntries)[0]
    $actual = (Get-FileHash $archivePath -Algorithm SHA256).Hash
    if ($actual -ine $expected) {
        throw 'Downloaded VSlices Tooling archive failed SHA-256 verification.'
    }

    Expand-Archive -Path $archivePath -DestinationPath $extractPath -Force
    $sourceExecutable = Join-Path $extractPath 'vslices.exe'
    if (-not (Test-Path $sourceExecutable)) {
        throw "Release archive does not contain vslices.exe."
    }

    New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
    $destination = Join-Path $InstallPath 'vslices.exe'
    Copy-Item -Path $sourceExecutable -Destination $destination -Force

    if (-not $SkipPath) {
        Add-ToUserPath -Entry $InstallPath
    }

    Write-Host "Installed VSlices Tooling $($release.tag_name) ($rid) to '$destination'."
    if (-not $SkipPath) {
        Write-Host 'VSlices Tooling was added to your user PATH.'
    }
    Write-Host 'Run: vslices --help'
}
finally {
    if (Test-Path $temp) {
        Remove-Item -Recurse -Force $temp
    }
}
