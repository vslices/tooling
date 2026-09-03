# VSlices project configuration

VSlices project configuration lives under `.vslices/` and represents project-specific operational policy.

The intended boundary is:

```text
.vslices/config.yaml
  = project and CLI operating preferences

.vslices/.ignore
  = project-specific artifact discovery exclusions

.vslices/ruleset/
  = revisable lowering knowledge

vslices executable
  = mechanisms, safety guarantees, orchestration, and target adapters
```

Configuration must not be used to redefine VSIR semantics or to weaken execution guarantees that the tooling relies on for correctness.

## Precedence

When an option can be supplied both explicitly and through project configuration, the precedence is:

```text
explicit CLI argument
  > .vslices/config.yaml
  > built-in default
```

This allows normal project workflows to stay concise while preserving explicit command-level overrides for CI, diagnostics, experiments, and one-off operations.

## Initial configuration surface

The initial project configuration is intentionally small:

```yaml
version: 0.1

targets:
  default: csharp

ruleset:
  source: https://github.com/vslices/ruleset
  ref: main

updates:
  source: https://github.com/vslices/tooling
  channel: preview
```

The fields are operational rather than semantic.

### `targets.default`

Declares the normal target used when a command does not receive `-to` explicitly.

Installing more than one target must not by itself make previously concise commands ambiguous when the project has declared a default.

An explicit `-to` always overrides the configured default.

### `ruleset.source`

Records the source from which the project-local ruleset was initialized.

For the official VSlices ruleset the canonical source is currently:

```text
https://github.com/vslices/ruleset
```

Custom ruleset sources may be recorded instead. Initialization from an explicit source should preserve that provenance in configuration so later ruleset update mechanisms do not need to guess where the rules came from.

### `ruleset.ref`

Declares the source revision or channel-like reference used for ruleset acquisition when the source supports refs.

The initial official default is `main` while the ruleset remains experimental.

### `updates.source`

Declares where the CLI looks for self-update releases or CI build artifacts. The initial supported source shape is a GitHub repository URL.

The official default is:

```text
https://github.com/vslices/tooling
```

An explicit `vslices update --self --source ...` invocation overrides this value for one run.

### `updates.channel`

Declares the preferred CLI update channel.

The supported channels are:

```text
stable
preview
build
```

`stable` accepts only non-prerelease GitHub releases. `preview` may accept prereleases as well as stable releases, preferring the newest release returned by the configured source.

`build` is a developer channel backed by GitHub Actions artifacts rather than Git tags or GitHub Releases. It resolves the newest successful CI run for the configured pull request and installs the artifact matching the current runtime identifier.

An explicit `vslices update --self --channel ...` invocation overrides this value for one run.

### `updates.pull-request`

Selects the pull request followed by the `build` channel.

Example:

```yaml
updates:
  source: https://github.com/vslices/tooling
  channel: build
  pull-request: 2
```

With this configuration, every successful CI run for PR #2 may publish build identities such as:

```text
build2.153
build2.154
build2.155
```

`vslices update --self` resolves the newest successful one automatically. The run number is therefore not stored in project configuration and does not require a manual edit after every push.

The build identity is deliberately independent of a future product version. A PR build does not claim that it will become `v0.1.1-preview`, `v0.1.2-preview`, or any other release. Published versions remain decisions represented by tags.

Build artifacts require GitHub authentication for download. The updater resolves credentials from `GH_TOKEN`, then `GITHUB_TOKEN`, then an authenticated GitHub CLI session via `gh auth token`. If none are available, the updater stops with an explicit diagnostic.

An explicit `vslices update --self --pull-request ...` invocation overrides this value for one run.

## Artifact discovery configuration

Project-specific discovery exclusions remain in:

```text
.vslices/.ignore
```

rather than being embedded as a list inside `config.yaml`.

The tooling always ignores these directories during recursive artifact discovery:

```text
.git/
.vslices/
bin/
obj/
```

These are safety/runtime exclusions and are not configurable.

The current `.vslices/.ignore` contract supports blank lines, `#` comments, directory paths, `*`, and `**`. Negation with `!` is not yet part of the contract.

Explicit paths remain authoritative: an ignored artifact is excluded from recursive discovery but may still be addressed directly by path.

## Materialization conventions

The current C# sibling convention remains:

```text
Name.vsir
Name.vsir.cs
```

This is intentionally not exposed as arbitrary configuration yet. The sibling relationship carries useful continuity between semantic source and editable materialization, and no concrete requirement currently justifies alternate layouts.

## What does not belong in `config.yaml`

Concrete lowering knowledge does not belong in project configuration. Examples include:

- intrinsic renderers;
- target syntax templates;
- construction mappings;
- semantic operation authority;
- lowering obligations and prohibitions.

Those belong in the ruleset.

Correctness and safety guarantees also remain executable behavior rather than preferences. Examples include:

- atomic file replacement;
- stopping when a required lowering rule is absent;
- refusing to invent semantic ancestry for rebase;
- built-in artifact discovery exclusions;
- SHA-256 validation of downloaded update artifacts before replacement.

A project must not be able to turn these guarantees off through configuration.

## Initialization

`vslices init` establishes the project-local VSlices operating surface:

```text
.vslices/
  config.yaml
  .ignore
  ruleset/
```

Initialization should:

1. resolve the selected ruleset source and target;
2. materialize only the selected target rules currently required by the project;
3. write `config.yaml` with the selected default target and ruleset provenance;
4. preserve existing CLI update source/channel preferences when replacing a ruleset;
5. create `.ignore` if absent.

`init --force` replaces the ruleset snapshot, not unrelated local project policy.

## Self update

The self-update surface is:

```text
vslices update --self
vslices update --self --check
```

For `stable` and `preview`, `--check` resolves the configured source, channel, current RID, and available GitHub Release without replacing the executable.

For `build`, `--check` resolves the latest successful CI run for the configured PR and reports its `build<pr>.<run>` identity without replacing the executable.

Published release assets use this contract:

```text
vslices-<rid>.zip
vslices-<rid>.zip.sha256
```

PR build artifacts use the same inner archive/checksum pair, wrapped in an Actions artifact named:

```text
build<pr>.<run>-<rid>
```

For example:

```text
build2.154-win-x64
build2.154-win-arm64
build2.154-linux-x64
```

The CI build embeds a SemVer-compatible assembly representation such as `0.0.0-build.2.154`, while the user-facing build identity remains `build2.154`.

The ZIP must contain exactly one platform executable named `vslices.exe` on Windows or `vslices` elsewhere. The checksum file contains the SHA-256 of the ZIP.

Self-update must verify the checksum before replacement. On Unix-like systems the executable can be replaced directly after verification. On Windows replacement is deferred to a temporary helper process after the running `vslices.exe` exits.

Self-update is only supported for the standalone native executable. Installations controlled by another package manager should be updated through that installation mechanism rather than silently replacing package-managed state.

## Update direction

CLI version and ruleset version are deliberately independent:

```text
vslices update --self
  = update the CLI executable

vslices update --ruleset
  = future update of project-local lowering knowledge
```

The current implementation only materializes the `--self` path. Ruleset update remains a separate subsequent capability.
