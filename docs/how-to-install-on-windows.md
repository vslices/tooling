# Install VSlices Tooling on Windows

This guide describes the supported Windows installation and update flows for the standalone VSlices Tooling CLI.

## Supported Windows architectures

The current Windows distribution supports:

```text
win-x64
win-arm64
```

The bootstrap detects the current operating-system architecture automatically and selects the matching published artifact.

## Requirements

For normal preview/stable installation:

- Windows x64 or Windows ARM64;
- PowerShell;
- internet access to GitHub.

Administrator privileges are not required.

For pull-request build installation, an authenticated GitHub session is also required because GitHub Actions artifact downloads require authentication. The updater can use, in order:

```text
GH_TOKEN
GITHUB_TOKEN
gh auth token
```

If GitHub CLI is already authenticated, no token needs to be copied into project configuration.

## Install the latest preview

Open PowerShell and run:

```powershell
irm https://raw.githubusercontent.com/vslices/tooling/main/install.ps1 | iex
```

The bootstrap:

1. resolves the newest preview release;
2. detects `win-x64` or `win-arm64`;
3. downloads the matching ZIP and SHA-256 file;
4. verifies the archive checksum;
5. installs `vslices.exe`;
6. adds the install directory to the current user PATH unless explicitly disabled.

The default install location is:

```text
%USERPROFILE%\.vslices\bin\vslices.exe
```

## Verify the installation

Run:

```powershell
vslices --help
```

A successful invocation confirms that the executable is available from the current shell environment.

If the command is not found immediately after installation, open a new terminal so Windows reloads the user PATH.

## Install a specific published version

The bootstrap supports a specific release version through the `Version` parameter.

For example:

```powershell
iex "& { $(irm https://raw.githubusercontent.com/vslices/tooling/main/install.ps1) } -Version 0.1.1-preview"
```

The `v` prefix is optional when resolving a published tag.

## Use the stable channel

The default bootstrap channel is `preview`.

To request only stable releases:

```powershell
iex "& { $(irm https://raw.githubusercontent.com/vslices/tooling/main/install.ps1) } -Channel stable"
```

## Use a custom install directory

For example:

```powershell
iex "& { $(irm https://raw.githubusercontent.com/vslices/tooling/main/install.ps1) } -InstallPath 'C:\Tools\VSlices'"
```

## Install without modifying PATH

Use `-SkipPath`:

```powershell
iex "& { $(irm https://raw.githubusercontent.com/vslices/tooling/main/install.ps1) } -SkipPath"
```

The executable is still installed, but the destination directory is not added to the user PATH.

## Update an installed CLI

The standalone native CLI can update itself:

```powershell
vslices update --self
```

To resolve the available update without replacing the executable:

```powershell
vslices update --self --check
```

The normal project configuration is release-oriented:

```yaml
updates:
  source: https://github.com/vslices/tooling
  channel: preview
```

Supported release channels are:

```text
preview
stable
```

`preview` may select prereleases. `stable` selects only non-prerelease GitHub Releases.

The intended operating model is config-first:

```text
.vslices/config.yaml
  = persistent update policy

vslices update --self
  = normal update action
```

Command-line update options remain available as explicit one-off overrides for diagnostics, CI, experiments, or recovery. They are not the recommended everyday workflow when the project already declares its update policy.

## Test the latest build from a pull request

Pull-request builds exist so CLI changes can be tested without creating a Git tag or GitHub Release.

A successful PR CI run publishes RID-specific development artifacts identified as:

```text
build<pr-number>.<run-number>
```

For example:

```text
build3.173
```

The user does not enter the run number manually.

To follow the newest successful build from a pull request, configure that development stream once in `.vslices/config.yaml`:

```yaml
updates:
  source: https://github.com/vslices/tooling
  channel: build
  pull-request: 3
```

Then the normal workflow is simply:

```powershell
vslices update --self
```

The updater:

1. reads the configured build channel and pull request;
2. finds the newest successful CI run associated with that pull request;
3. resolves its build identity automatically;
4. selects the artifact matching the current runtime, such as `win-x64` or `win-arm64`;
5. downloads the Actions artifact using the available GitHub authentication;
6. verifies the SHA-256 checksum contained in the build artifact;
7. replaces the standalone CLI after the current process exits.

A later push to the same pull request creates a newer build. Running `vslices update --self` again follows that newest successful build without editing either the run number or the command.

This is the preferred development loop:

```text
edit .vslices/config.yaml once
        ↓
channel: build
pull-request: <pr>
        ↓
vslices update --self
        ↓
new successful PR build appears
        ↓
vslices update --self
```

If a one-off override is genuinely needed, the CLI still supports explicit update arguments. Those flags should be treated as overrides of project policy rather than the normal way to follow a PR build.

## Authenticate for pull-request builds

The simplest development setup is GitHub CLI:

```powershell
gh auth login
```

After authentication, verify the session with:

```powershell
gh auth status
```

VSlices can then obtain a token from `gh auth token` when neither `GH_TOKEN` nor `GITHUB_TOKEN` is set.

Authentication is required only for GitHub Actions build artifacts. Published preview/stable releases continue to use public GitHub Release assets.

## Build vs published version

A PR build is not a product release version.

Examples:

```text
build3.173
build3.174
```

These identify development artifacts associated with a PR and CI run. They create neither Git tags nor GitHub Releases.

Published versions remain deliberate tag decisions, for example:

```text
v0.2.0-preview
v1.0.0
```

This keeps continuous build production separate from deliberate publication.

## Troubleshooting

### Unsupported Windows architecture

The supported Windows architectures are currently x64 and ARM64. Other architectures are rejected explicitly by the installer.

### `vslices` is not recognized after installation

Open a new terminal first so the updated user PATH is loaded.

The default executable path is:

```text
%USERPROFILE%\.vslices\bin\vslices.exe
```

You can invoke that path directly to distinguish a PATH problem from an installation problem.

### A pull-request build cannot be downloaded

Check that:

- the configured `pull-request` exists;
- the PR has at least one successful CI run that produced build artifacts;
- the requested runtime was produced by that run;
- GitHub authentication is available through `GH_TOKEN`, `GITHUB_TOKEN`, or `gh auth token`.

### `channel: build` is configured without `pull-request`

The build channel requires an explicit pull-request number. VSlices does not guess which development stream should be followed.

### Checksum verification fails

VSlices does not install an archive whose SHA-256 verification fails. Retry the operation; if the failure persists, treat the artifact as invalid rather than bypassing verification.

## Related documentation

- [`configuration.md`](configuration.md) describes the complete `.vslices/config.yaml` operating policy.
- [`releases/v0.1.1-preview.md`](releases/v0.1.1-preview.md) records the distribution and PR-build changes introduced while preparing `v0.1.1-preview`.
