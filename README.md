# VSlices Tooling

VSlices Tooling is the executable tooling surface of the VSlices suite.

Its purpose is to provide repeatable mechanisms around VSlices artifacts while keeping revisable semantic and lowering knowledge outside the executable whenever possible.

The CLI is named `vslices`.

## v0.1.0-preview scope

The intended first preview exposes:

```text
vslices init
vslices lower
vslices transpile
vslices rebase
vslices update --self
```

`interpretate` is deliberately not part of `v0.1.0-preview`. The concept is defined as interpretive lowering for cases where deterministic mechanisms are insufficient but enough semantic authority and contextual evidence exist. A first executable `vslices interpretate` surface is a candidate for `v0.2.0-preview`.

The complete release boundary and acceptance criteria are recorded in [`docs/releases/v0.1.0-preview.md`](docs/releases/v0.1.0-preview.md).

`v0.1.1-preview` is a distribution and release-pipeline patch that adds Windows ARM64 support, removes duplicated PR CI work, and introduces installable PR builds without expanding the lowering surface. See [`docs/releases/v0.1.1-preview.md`](docs/releases/v0.1.1-preview.md).

## Current responsibilities

The repository currently contains mechanisms for:

- structured VSlices document generation;
- parsing and conservatively validating the current experimental VSIR benchmark surface;
- deterministic VSIR-to-C# transpilation for supported structures;
- conservative semantic rebase over human-edited materializations;
- orchestration of the least-powerful available lowering mechanism through `lower`;
- shared project-aware VSIR artifact discovery;
- .NET target-context delegation and explicit namespace override;
- project-local configuration and ruleset initialization;
- Native AOT distribution;
- verified Windows bootstrap installation;
- verified standalone CLI self-update;
- installable pull-request builds through GitHub Actions artifacts.

## Authority boundaries

The core architectural separation is:

```text
.vsir
  = semantic source

.vsir.cs
  = editable materialization constrained by VSIR

project/.vslices/config.yaml
  = project operating policy

project/.vslices/.ignore
  = project-specific discovery exclusions

vslices/ruleset
  = official revisable lowering knowledge

project/.vslices/ruleset
  = local ruleset snapshot

vslices executable
  = mechanisms, orchestration, safety guarantees, and target adapters
```

Concrete target lowering mappings should not be embedded in the CLI when they can be expressed as external rules. A missing rule is not permission to guess or silently fall back.

A useful shorthand for the current design is:

> Lowering may complete implementation detail. Lowering must not complete missing semantics.

For an implementation `I` and VSIR document `V`, the intended relation is that `I` satisfies `V`; the transpiler constructs one valid witness rather than defining the only acceptable source form.

See [`docs/rulesets.md`](docs/rulesets.md), [`docs/configuration.md`](docs/configuration.md), and [`docs/context.vslices-tooling.md`](docs/context.vslices-tooling.md) for the focused contracts.

## Shared command conventions

Developer-experience shortcuts are implemented as shared command infrastructure so commands do not invent independent resolution behavior.

Current conventions include:

- VSIR symbols and paths share artifact resolution;
- recursive symbol discovery ignores `.git/`, `.vslices/`, `bin/`, and `obj/` as built-in exclusions;
- project-specific discovery exclusions live in `.vslices/.ignore`;
- explicit paths remain authoritative even for artifacts excluded from recursive discovery;
- `-to` may be omitted when a project default target is configured or exactly one supported target is installed;
- a C# materialization conventionally lives beside its VSIR as `Name.vsir.cs`;
- `-o <path>` overrides the conventional output path;
- `--stdout` is equivalent to `-o -`;
- file writes use temporary siblings and atomic replacement;
- expected failures use explicit diagnostics.

The current `.vslices/.ignore` contract intentionally supports a small subset: blank lines, `#` comments, directory paths, `*`, and `**`. Negation with `!` is not yet part of the contract.

## Lowering commands

### `transpile`

`transpile` creates a deterministic projection when the ruleset and target context are sufficient.

By default it writes the sibling materialization and refuses to overwrite an existing file unless `--force` is explicit. Existing `.vsir.cs` files are treated as human-editable materializations, not disposable generated output.

### `rebase`

`rebase` reconstructs the previous deterministic projection, compares it with the human-edited materialization, and applies a compatible deterministic VSIR change conservatively.

The previous VSIR baseline is still explicit through `--from`; automatic provenance reconstruction is outside `v0.1.0-preview`.

### `lower`

`lower` is the normal orchestration surface.

In the first preview:

```text
no materialization
  -> transpile

existing materialization + explicit previous baseline
  -> rebase

existing materialization + unknown ancestry
  -> stop
```

It must not invent ancestry merely to keep the command moving.

## Project initialization and configuration

`vslices init` establishes:

```text
.vslices/
  config.yaml
  .ignore
  ruleset/
```

The default configuration remains release-oriented:

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

Operational precedence is:

```text
explicit CLI argument
  > .vslices/config.yaml
  > built-in default
```

Configuration cannot redefine VSIR semantics or disable correctness guarantees such as missing-rule failure or atomic writes.

Only the selected target rules are materialized locally. Once initialized, lowering operates from project-local state without requiring network access.

## Windows installation

Install the latest preview with PowerShell:

```powershell
irm https://raw.githubusercontent.com/vslices/tooling/main/install.ps1 | iex
```

The bootstrap supports Windows x64 and ARM64, verifies the downloaded archive, and installs the standalone Native AOT executable without requiring administrator privileges.

For specific versions, custom install paths, PATH behavior, self-update, and installable pull-request builds, see [`docs/how-to-install-on-windows.md`](docs/how-to-install-on-windows.md).

## Distribution and self-update

Release automation targets the Native AOT RIDs:

```text
win-x64
win-arm64
linux-x64
```

Each published release artifact has a separate SHA-256 file:

```text
vslices-win-x64.zip
vslices-win-x64.zip.sha256

vslices-win-arm64.zip
vslices-win-arm64.zip.sha256

vslices-linux-x64.zip
vslices-linux-x64.zip.sha256
```

The self-update surface is:

```text
vslices update --self
vslices update --self --check
```

Release-oriented projects normally use `preview` or `stable`. Developers may instead follow the newest successful build of a pull request:

```yaml
updates:
  source: https://github.com/vslices/tooling
  channel: build
  pull-request: 2
```

PR builds are identified as:

```text
build<pr-number>.<run-number>
```

For example, `build2.154`. The run number is resolved automatically; it is not copied into configuration. Each successful PR CI run publishes RID-specific artifacts such as `build2.154-win-arm64`.

GitHub Actions artifact downloads require authentication. The updater uses `GH_TOKEN`, `GITHUB_TOKEN`, or an authenticated `gh` CLI session. Release and preview downloads remain based on public GitHub Releases.

The archive checksum is verified before replacement for both release and build channels. Windows uses a temporary helper after the running executable exits; Unix-like systems may replace the standalone executable directly.

CLI version and ruleset version remain independent. `vslices update --ruleset` is future scope.

## Validation strategy

The current benchmark begins with `StreetName.vsir` and expands only when concrete examples require additional semantic or lowering structures.

CI is expected to cover:

- Release build;
- automated lowering/rebase tests;
- explicit namespace override without a `.csproj`;
- real CLI `transpile`, `lower`, and `rebase` flows;
- recursive discovery ignores;
- .NET target-context delegation;
- PowerShell bootstrap syntax;
- Native AOT publication and execution for the host RID;
- Native AOT publication for both Windows x64 and Windows ARM64;
- RID-specific installable artifacts for successful PR runs;
- project initialization and configuration creation.

The same CI operation set runs for pull requests, scheduled nightly validation, `main`, manual dispatch, and `v*` tags. Release tags additionally trigger the release workflow that packages and publishes official assets.

Published releases provide the final end-to-end evidence for remote bootstrap installation and self-update against release assets. ARM64 execution is additionally validated on a real Windows ARM64 environment because the hosted Windows release runner is x64.

## Long-term dogfooding objective

Whenever VSlices claims it can represent a kind of software concept, VSlices Tooling itself should become a candidate for expressing instances of that concept through `.vsir` artifacts.

This does not imply generating every line of Tooling. The goal is semantic self-hosting where representable semantics are maintained through the same contracts and mechanisms offered to other projects.

## Status

VSIR lowering, rulesets, configuration, installation, rebase, and self-update remain preview-quality and experimental. The repository prefers small evidence-driven extensions over speculative generalization.
