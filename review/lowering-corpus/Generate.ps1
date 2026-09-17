[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Vslices,

    [string] $Output = (Join-Path $PSScriptRoot 'generated')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$vslicesPath = (Resolve-Path $Vslices).Path
$outputPath = [System.IO.Path]::GetFullPath($Output)
$rulesetRoot = Join-Path $root 'ruleset'
$corpusPath = Join-Path $root 'corpus.tsv'

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

$workspace = Join-Path $outputPath 'workspace'
$inputs = Join-Path $outputPath 'inputs'
$outputs = Join-Path $outputPath 'outputs'
$logs = Join-Path $outputPath 'logs'
$context = Join-Path $outputPath 'context'

foreach ($directory in @($workspace, $inputs, $outputs, $logs, $context)) {
    New-Item -ItemType Directory -Force $directory | Out-Null
}

Copy-Item (Join-Path $root 'ReviewCorpus.csproj') $workspace
Copy-Item (Join-Path $root 'NominalTypes.cs') $workspace
Copy-Item (Join-Path $root 'artifacts') (Join-Path $workspace 'artifacts') -Recurse

function Invoke-VSlices {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,
        [Parameter(Mandatory = $true)]
        [string] $StdoutPath,
        [Parameter(Mandatory = $true)]
        [string] $StderrPath
    )

    if ($vslicesPath.EndsWith('.dll', [System.StringComparison]::OrdinalIgnoreCase)) {
        & dotnet $vslicesPath @Arguments 1> $StdoutPath 2> $StderrPath
    }
    else {
        & $vslicesPath @Arguments 1> $StdoutPath 2> $StderrPath
    }

    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $stderr = if (Test-Path $StderrPath) { Get-Content $StderrPath -Raw } else { '' }
        $stdout = if (Test-Path $StdoutPath) { Get-Content $StdoutPath -Raw } else { '' }
        throw "VSlices exited with code $exitCode while running '$($Arguments -join ' ')'.`nSTDERR:`n$stderr`nSTDOUT:`n$stdout"
    }
}

$restoreStdout = Join-Path $logs 'restore.stdout.txt'
$restoreStderr = Join-Path $logs 'restore.stderr.txt'
& dotnet restore (Join-Path $workspace 'ReviewCorpus.csproj') --nologo 1> $restoreStdout 2> $restoreStderr
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed for the review target-context project.`n$(Get-Content $restoreStderr -Raw)"
}

$versionStdout = Join-Path $logs 'version.stdout.txt'
$versionStderr = Join-Path $logs 'version.stderr.txt'
Invoke-VSlices -Arguments @('--version') -StdoutPath $versionStdout -StderrPath $versionStderr
$toolingVersion = (Get-Content $versionStdout -Raw).Trim()

Push-Location $workspace
try {
    Invoke-VSlices `
        -Arguments @('init', '--from', $rulesetRoot, '--target', 'C#') `
        -StdoutPath (Join-Path $logs 'init.stdout.txt') `
        -StderrPath (Join-Path $logs 'init.stderr.txt')

    $entries = Import-Csv -Path $corpusPath -Delimiter "`t"
    foreach ($entry in $entries) {
        $source = Join-Path 'artifacts' $entry.file
        $materialization = Join-Path $outputs ($entry.file + '.cs')
        $stderr = Join-Path $logs ($entry.file + '.stderr.log')

        Write-Host "Lowering $($entry.file) -> $materialization"
        Invoke-VSlices `
            -Arguments @('lower', $source, '--namespace', $entry.namespace, '--stdout') `
            -StdoutPath $materialization `
            -StderrPath $stderr

        if (-not (Test-Path $materialization) -or (Get-Item $materialization).Length -eq 0) {
            throw "Lowering '$($entry.file)' succeeded without producing reviewable stdout materialization."
        }

        Copy-Item $source (Join-Path $inputs $entry.file)
    }
}
finally {
    Pop-Location
}

Copy-Item $corpusPath (Join-Path $outputPath 'corpus.tsv')
Copy-Item (Join-Path $root 'README.md') (Join-Path $outputPath 'CORPUS-README.md')
Copy-Item $rulesetRoot (Join-Path $outputPath 'ruleset') -Recurse
Copy-Item (Join-Path $root 'ReviewCorpus.csproj') $context
Copy-Item (Join-Path $root 'NominalTypes.cs') $context

$sourceHead = if (-not [string]::IsNullOrWhiteSpace($env:VSLICES_REVIEW_SOURCE_SHA)) {
    $env:VSLICES_REVIEW_SOURCE_SHA
} elseif (-not [string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
    $env:GITHUB_SHA
} else {
    'local-worktree'
}
$workflowCheckout = if (-not [string]::IsNullOrWhiteSpace($env:VSLICES_REVIEW_CHECKOUT_SHA)) {
    $env:VSLICES_REVIEW_CHECKOUT_SHA
} elseif (-not [string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
    $env:GITHUB_SHA
} else {
    'local-worktree'
}
$generatedAt = [DateTimeOffset]::UtcNow.ToString('O')
$entries = Import-Csv -Path $corpusPath -Delimiter "`t"

@"
# Lowering review evidence

- Tooling CLI version: $toolingVersion
- Tooling source HEAD: $sourceHead
- Workflow checkout SHA: $workflowCheckout
- Ruleset: vslices/ruleset@e2f5ea85283cc3a91e8c87107a06e0c23ccc1668
- Generated at UTC: $generatedAt
- Corpus entries: $($entries.Count)
- Command shape: vslices lower <artifact.vsir> --namespace <manifest namespace> --stdout

inputs/ and outputs/ are paired by filename. logs/ preserves target-context/type-resolution diagnostics emitted on stderr. context/ contains only the isolated .NET symbol context used for nominal type resolution.
"@ | Set-Content (Join-Path $outputPath 'MANIFEST.md') -Encoding utf8

$checksumRoots = @(
    (Join-Path $outputPath 'inputs'),
    (Join-Path $outputPath 'outputs'),
    (Join-Path $outputPath 'ruleset')
)

$checksumLines = foreach ($checksumRoot in $checksumRoots) {
    Get-ChildItem $checksumRoot -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($outputPath, $_.FullName).Replace('\', '/')
            $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relative"
        }
}
$checksumLines | Set-Content (Join-Path $outputPath 'checksums.sha256') -Encoding ascii

Remove-Item $workspace -Recurse -Force
Write-Host "Lowering review corpus generated at: $outputPath"
