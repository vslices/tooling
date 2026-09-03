# VSlices Tooling

VSlices Tooling is the executable tooling surface of the VSlices suite.

Its purpose is to provide repeatable mechanisms around VSlices artifacts while keeping revisable semantic and lowering knowledge outside the executable whenever possible.

The current CLI is named `vslices`.

## Current responsibilities

The repository currently contains tooling for:

- structured document generation for VSlices documentation;
- parsing and validating the experimental VSIR surface used by current benchmarks;
- deterministic VSIR-to-C# transpilation for supported structures;
- conservative semantic rebase experiments over human-edited materializations;
- orchestration of the least-powerful available lowering mechanism through `lower`;
- shared project-aware VSIR artifact discovery;
- .NET target-context discovery;
- project-local ruleset discovery and initialization;
- Native AOT self-update discovery and verified replacement.

The commands currently being explored include:

```text
vslices init
vslices lower
vslices transpile
vslices rebase
vslices update --self
```

`interpretate` and `update --ruleset` remain design directions rather than stable commands.

## Tooling vs. lowering knowledge

A central architectural boundary is:

```text
vslices executable
  = execution and orchestration mechanisms

project/.vslices/config.yaml
  = project operating preferences

vslices/ruleset
  = official, revisable lowering knowledge

project/.vslices/ruleset
  = local ruleset snapshot used by a project

.vsir
  = semantic source

.vsir.cs
  = editable materialization
```

Concrete target lowering mappings should not be embedded in the CLI when they can be expressed as external rules. The executable may know how to execute supported classes of rules, but the rules themselves belong in the ruleset.

A missing rule is not permission to guess. Unsupported lowering remains explicit.

Project configuration controls operating preferences such as the default target, ruleset provenance, CLI update source, and update channel. It must not redefine VSIR semantics or disable correctness guarantees. Explicit command arguments override project configuration, which overrides built-in defaults.

See `docs/configuration.md` for the project configuration contract and `docs/rulesets.md` for the current experimental ruleset contract.

## Shared command conventions

Developer-experience shortcuts are intentionally implemented as shared command infrastructure so future commands can reuse them rather than inventing command-specific behavior.

Current conventions include:

- VSIR symbols and paths share the same artifact resolution rules;
- recursive symbol discovery ignores `.git/`, `.vslices/`, `bin/`, and `obj/` as built-in exclusions;
- project-specific discovery exclusions are declared in `.vslices/.ignore`;
- `-to` may be omitted when a project default target is configured or when exactly one supported target is installed locally;
- a C# materialization conventionally lives beside its VSIR as `Name.vsir.cs`;
- `-o <path>` overrides the conventional output path;
- `--stdout` writes the result to standard output and is equivalent to `-o -`;
- file writes use a temporary sibling and atomic replacement;
- commands emit the same diagnostic representation for expected failures.

The current `.vslices/.ignore` contract intentionally supports a small familiar subset: blank lines, `#` comments, directory paths, `*`, and `**`. Negation with `!` is not yet part of the contract. Ignore patterns are project policy for VSlices artifact discovery in general rather than behavior owned by a particular command.

`transpile` writes a new sibling materialization by default and refuses to overwrite an existing file unless `--force` is explicit. This preserves the distinction between initial deterministic projection and an already human-editable materialization.

`rebase` infers the sibling materialization as its human source when `--source` is omitted and updates that materialization by default. The previous VSIR baseline still has to be supplied through `--from` until provenance is represented explicitly.

`lower` is the normal orchestration surface. In the current iteration it chooses deterministic transpilation when no materialization exists and rebase when an existing materialization and previous baseline are available. It must stop rather than inventing ancestry when a rebase baseline cannot be established.

## VSIR lowering model

VSIR does not define one privileged source-code rendering. It constrains the space of acceptable materializations.

For an implementation `I` and VSIR document `V`, the intended relation is:

```text
I satisfies V
```

A deterministic transpiler constructs one valid materialization when the lowering knowledge is complete enough. Human edits remain legitimate as long as the resulting source continues to satisfy the VSIR contract.

Semantic rebase is being explored for the case where VSIR evolves after a generated materialization has already been edited by a human.

## Project initialization

`vslices init` establishes the project-local VSlices operating surface:

```text
.vslices/
  config.yaml
  .ignore
  ruleset/
```

`config.yaml` records operational project choices such as the default target, ruleset source/ref, CLI update source, and update channel. `.ignore` contains project-specific discovery exclusions. `ruleset/` contains the selected local lowering knowledge.

The initial configuration shape is:

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

On an interactive terminal, initialization offers the official `vslices/ruleset` source and target selection. Custom local directories or HTTP(S) ZIP sources remain available through `--from`, and non-interactive use can select a target explicitly.

Only the selected target directory is materialized into the project-local ruleset. The current ZIP bootstrap still downloads the source archive before selecting files; remote per-target retrieval is a later optimization rather than a requirement for the current architecture.

`init --force` replaces the ruleset snapshot while preserving project-owned `.ignore` and CLI update preferences unless a future explicit reset surface says otherwise.

Once initialized, lowering operates from local state without requiring network access.

## Distribution and self update

The CLI is intended to remain lightweight and is configured for Native AOT publication under the executable name `vslices`.

Configuration and lowering knowledge remain external so changes to rules do not require republishing the executable. Native AOT is a deployment direction rather than a semantic constraint on the tooling design.

The current self-update surface is:

```text
vslices update --self
vslices update --self --check
```

`--self` resolves the update source and channel using the same precedence as the rest of the CLI:

```text
command argument
  > .vslices/config.yaml
  > built-in default
```

A supported release publishes one archive and checksum per RID:

```text
vslices-win-x64.zip
vslices-win-x64.zip.sha256

vslices-linux-x64.zip
vslices-linux-x64.zip.sha256
```

The archive checksum is verified before replacement. Windows uses a temporary helper after the running executable exits; Unix-like systems may replace the executable directly. Self-update is only intended for the standalone native executable.

Tag-triggered release automation currently publishes the proven `win-x64` and `linux-x64` Native AOT artifacts and injects the tag version into the binary so it can recognize when it is already on the selected release.

The update split remains deliberate:

```text
vslices update --self
  = executable update

vslices update --ruleset
  = future project lowering-knowledge update
```

CLI version and ruleset version are independent.

## Validation strategy

The current benchmarks begin with `StreetName.vsir` and progressively introduce new VSIR structures only when concrete examples require them.

Important properties to preserve include:

- deterministic output for the same VSIR, ruleset, and target context;
- no hidden fallback when a rule is absent;
- the ability to change lowering behavior through the external ruleset without recompiling the CLI;
- recursive artifact discovery that respects built-in and project-specific exclusions;
- project configuration whose command-line overrides have clear precedence;
- offline lowering after initialization;
- verified update downloads before executable replacement;
- technical validation through build/test and target-specific tooling;
- execution of the actual Native AOT binary in CI;
- semantic verification as a distinct concern from compilation.

## Long-term dogfooding objective

A long-term objective is incremental semantic self-hosting: whenever VSlices claims it can represent a kind of software concept, the tooling itself should become a candidate for expressing its own instances of that concept through `.vsir` artifacts.

This does not mean every line of VSlices Tooling must be generated. The stronger goal is that representable semantics inside the tooling are described and maintained using the same VSIR and lowering mechanisms provided to other projects.

The tooling can then serve simultaneously as a dogfooding target, conformance corpus, and source of evidence about gaps in VSIR.

## Status

VSIR lowering, ruleset support, project configuration, and self-update are experimental. The repository should prefer small, evidence-driven extensions over speculative generalization.
