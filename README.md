# VSlices Tooling

VSlices Tooling is the executable tooling surface of the VSlices suite.

Its purpose is to provide repeatable mechanisms around VSlices artifacts while keeping revisable semantic and lowering knowledge outside the executable whenever possible.

The CLI is named `vslices`.

## Start here

The current development line is `v0.2.0-preview`.

It advances on two complementary tracks:

```text
CLI experience
  -> identity, presentation, progress and operability

semantic capability
  -> broader real-world VSIR coverage
     -> classify the resulting lowering needs
     -> extend deterministic mechanisms where evidence supports them
     -> discover interpretive work only when a concrete case requires it
```

The currently implemented command surface remains:

```text
vslices init
vslices lower
vslices transpile
vslices rebase
vslices update --self
vslices --version
vslices -v
```

`interpretate` is a defined hypothesis for interpretive lowering, not a mandatory roadmap checkbox. A `vslices interpretate` command should emerge only if a real case remains genuinely underdetermined but sufficiently constrained after VSIR semantics, ruleset knowledge, project evidence and target-native authority have all been considered.

The current version direction is documented in [`docs/releases/v0.2.0-preview.md`](docs/releases/v0.2.0-preview.md).

## Orientation for AI-assisted development

A future development session should be able to reconstruct the accepted model from repository artifacts without depending on chat history.

Before changing semantic behavior, read [`docs/ai-development-orientation.md`](docs/ai-development-orientation.md). It defines the cross-repository reading order and the decision procedure for deciding whether evidence from a VSlices-enabled project belongs in:

```text
consumer project
vslices/tooling
vslices/ruleset
target-native tooling
```

When work begins from a consumer repository such as `atom-dev-serviu/access-management-product`, inspect its concrete `.vsir`, `.vsir.cs`, `.vslices/config.yaml`, local `.vslices/ruleset`, surrounding source/tests and target context before deciding that Tooling or the official ruleset must change.

The intended causal chain is:

```text
consumer evidence
  -> semantic requirement
  -> VSIR / ruleset / tooling change
  -> validation evidence
```

Do not use a missing rule as permission to guess and do not use remembered conversation state as authority when repository evidence can be inspected.

## Current responsibilities

The repository currently contains mechanisms for:

- structured VSlices document generation;
- parsing and conservatively validating the current experimental VSIR surface;
- deterministic VSIR-to-C# transpilation for supported structures;
- conservative semantic rebase over human-edited materializations;
- orchestration of the least-powerful available lowering mechanism through `lower`;
- shared project-aware VSIR artifact discovery;
- .NET target-context delegation and explicit namespace override;
- project-local configuration and ruleset initialization;
- a centralized static terminal presentation boundary for interactive CLI output;
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
  = human-editable materialization constrained by VSIR

project/.vslices/config.yaml
  = project operating policy

project/.vslices/.ignore
  = project-specific discovery exclusions

vslices/ruleset
  = official revisable lowering knowledge

project/.vslices/ruleset
  = local ruleset snapshot actually used by lowering

vslices executable
  = mechanisms, orchestration, safety guarantees, CLI behavior and target adapters

target-native tooling
  = target facts already owned by the target ecosystem
```

Concrete target lowering mappings should not be embedded in the CLI when they can be expressed as external rules. A missing rule is not permission to guess or silently fall back.

A useful shorthand for the current design is:

> Lowering may complete implementation detail. Lowering must not complete missing semantics.

For future interpretive work:

> Interpretation may resolve underdetermined materialization. Interpretation must not manufacture missing authority.

For an implementation `I` and VSIR document `V`, the intended relation is:

```text
I |= V
```

The transpiler constructs one valid witness rather than defining the only acceptable source form.

See [`docs/rulesets.md`](docs/rulesets.md), [`docs/configuration.md`](docs/configuration.md), [`docs/context.vslices-tooling.md`](docs/context.vslices-tooling.md), and [`docs/ai-development-orientation.md`](docs/ai-development-orientation.md) for focused contracts.

## Repository ownership

The current project split is:

```text
src/VSlices.Tooling
  = CLI execution adapter, orchestration-facing behavior and presentation

src/VSlices.Vsir
  = experimental VSIR semantic model, parsing and conservative validation

src/VSlices.Vsir.CSharp
  = deterministic C# lowering and current deterministic rebase behavior

src/VSlices.Targets.DotNet
  = .NET target context and delegation to target-native tooling

src/VSlices.DocumentGeneration
  = structured document generation behavior

vslices/ruleset
  = official external lowering knowledge
```

Reusable behavior projects must not depend on the CLI project. Target-specific tooling may resolve target context, but it should not redefine VSIR semantics.

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

`transpile` creates a deterministic projection when the VSIR, ruleset and target context are sufficient.

By default it writes the sibling materialization and refuses to overwrite an existing file unless `--force` is explicit. Existing `.vsir.cs` files are human-editable materializations, not disposable generated output.

### `rebase`

`rebase` reconstructs the previous deterministic projection, compares it with the human-edited materialization, and applies a compatible deterministic VSIR change conservatively.

The previous VSIR baseline may be explicit through `--from`. Normal `lower` usage now records project-local deterministic baselines under `.vslices/lineage/` and reuses them when available; explicit `--from` remains the recovery path when trustworthy ancestry cannot otherwise be established.

### `lower`

`lower` is the normal orchestration surface and selects the least-powerful sufficient mechanism.

The current conservative behavior is:

```text
no materialization
  -> transpile
  -> record deterministic baseline

existing materialization + recorded deterministic baseline
  -> rebase automatically
  -> update deterministic baseline

existing materialization + no recorded baseline + exact current deterministic match
  -> establish lineage without rewriting the materialization

existing materialization + unknown ancestry
  -> stop
  -> allow explicit --from to establish the missing baseline
```

The lineage store is operational evidence for rebase. It is not semantic authority and must not be used to repair or reinterpret `.vsir` semantics.

It must not invent ancestry merely to keep the command moving.

If an interpretive mechanism is eventually adopted, `lower` should continue to choose the least interpretive mechanism that is actually authorized.

## Project initialization and configuration

`vslices init` establishes:

```text
.vslices/
  config.yaml
  .ignore
  ruleset/
```

Plain `vslices init` uses the official `vslices/ruleset` as the normal bootstrap source, while explicit local directories or HTTP(S) ZIP sources remain supported.

The default configuration is release-oriented:

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

Configuration expresses operating policy, not VSIR semantics. It cannot disable correctness guarantees such as missing-rule failure or atomic writes.

Only the selected target rules are materialized locally. Once initialized, lowering operates from project-local state without requiring network access.

## CLI identity and presentation

The CLI exposes equivalent version forms:

```text
vslices --version
vslices -v
```

Published versions expose the product version. Pull-request builds expose a human-facing build identity:

```text
build<pr-number>.<run-number>
```

The internal .NET assembly representation is an implementation detail and should not leak into normal CLI output.

Interactive `--version`, `init`, and `update --self` use the shared terminal presentation boundary and VSlices branding. Redirected/machine-consumable output remains plain and safe to script against.

Presentation may decorate output. It must not change command semantics.

The current styling layer is deliberately static. User-configurable themes are possible future work after the project has evidence for which presentation roles deserve to become stable configuration.

## Windows installation

Install the latest preview with PowerShell:

```powershell
irm https://raw.githubusercontent.com/vslices/tooling/main/install.ps1 | iex
```

The bootstrap supports Windows x64 and ARM64, verifies the downloaded archive, and installs the standalone Native AOT executable without requiring administrator privileges.

For specific versions, custom install paths, PATH behavior, self-update, and installable pull-request builds, see [`docs/how-to-install-on-windows.md`](docs/how-to-install-on-windows.md).

## Distribution and self-update

Release automation targets:

```text
win-x64
win-arm64
linux-x64
```

Each published release artifact has a separate SHA-256 file.

The self-update surface is:

```text
vslices update --self
vslices update --self --check
```

Normal usage keeps update policy in `.vslices/config.yaml` and invokes `vslices update --self` without repeating that policy on every command.

Developers may follow the newest successful build of a pull request:

```yaml
updates:
  source: https://github.com/vslices/tooling
  channel: build
  pull-request: 4
```

The updater resolves the newest successful run automatically. CLI flags for channel or pull request are overrides for diagnostics, CI, experiments or recovery rather than the preferred daily workflow.

GitHub Actions build artifact downloads use `GH_TOKEN`, `GITHUB_TOKEN`, or an authenticated `gh` CLI session. Release and preview downloads remain based on public GitHub Releases.

The archive checksum is verified before replacement. Windows uses a temporary helper after the running executable exits; Unix-like systems may replace the standalone executable directly.

CLI version and ruleset version remain independent. `vslices update --ruleset` is future scope.

## Evidence-driven VSIR development

The initial benchmark was `StreetName.vsir`, but `v0.2.0-preview` deliberately expands through additional real examples rather than treating that benchmark as the model's boundary.

For each new `.vsir`, ask:

```text
Can the semantics be represented faithfully?
Can the current ruleset lower them deterministically?
Can target tooling authoritatively resolve remaining target detail?
Can rebase preserve compatible human choices?
What, if anything, remains underdetermined?
```

Classify discovered gaps before changing repositories:

```text
semantic representation gap
validation/parsing gap
ruleset knowledge gap
target-context gap
lowering mechanism gap
rebase/provenance gap
presentation-only gap
no gap
```

The Ticket Support `TicketId.vsir` case is the current concrete semantic-boundary example. It declares `traits: [identifier, transform]` and `equality: { intrinsic: ordinal-equals, by: state.Value }`. Tooling now represents and validates those semantics rather than silently discarding or rejecting them as structurally unknown. C# lowering still rejects them explicitly until a deterministic identifier/equality lowering mechanism is justified. This preserves the distinction between semantic validity and target lowering knowledge.

This classification is especially important when analyzing a consumer repository and coordinating changes between that repository, `vslices/tooling`, and `vslices/ruleset`.

## Validation strategy

CI is expected to cover:

- Release build and automated tests;
- deterministic lowering/rebase behavior;
- real CLI `transpile`, `lower`, `rebase`, `init`, version and update flows where practical;
- explicit namespace override without a `.csproj`;
- recursive discovery ignores;
- .NET target-context delegation;
- PowerShell bootstrap syntax;
- Native AOT publication and execution for the host RID;
- Native AOT publication for Windows x64 and Windows ARM64;
- RID-specific installable artifacts for successful PR runs;
- project initialization and configuration creation;
- machine-safe redirected output for CLI surfaces that also have interactive presentation.

The same CI operation set runs for pull requests, scheduled nightly validation, `main`, manual dispatch, and `v*` tags. Release tags additionally trigger the release workflow that packages and publishes official assets.

Published releases provide final end-to-end evidence for remote bootstrap installation and self-update against release assets. ARM64 execution may additionally require a real Windows ARM64 environment because the hosted Windows release runner is x64.

## Long-term dogfooding objective

Whenever VSlices claims it can represent a kind of software concept, VSlices Tooling itself should become a candidate for expressing instances of that concept through `.vsir` artifacts.

This does not imply generating every line of Tooling. The goal is semantic self-hosting where representable semantics are maintained through the same contracts and mechanisms offered to other projects.

## Status

VSIR lowering, rulesets, configuration, installation, rebase, presentation and self-update remain preview-quality and experimental.

The repository prefers small evidence-driven extensions over speculative generalization. Material decisions should be made reconstructible in repository artifacts so future human or AI-assisted sessions can continue from current evidence instead of recreating the design from conversation history.
