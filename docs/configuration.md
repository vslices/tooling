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

### `updates.channel`

Declares the preferred CLI update channel.

The initial channels anticipated by the tooling are:

```text
stable
preview
```

The project may normally use `preview` while VSIR lowering remains experimental. A future `vslices update --self --channel ...` argument may override the project preference for one invocation.

## Artifact discovery configuration

Project-specific discovery exclusions remain in:

```text
.vslices/.ignore
```

rather than being embedded as a list inside `config.yaml`.

The location is currently conventional. Future configuration may allow additional ignore sources if concrete use cases justify it.

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

The configuration model may later gain a materialization layout policy without changing the command model.

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
- validation of downloaded update artifacts before replacement.

A project must not be able to turn these guarantees off through configuration.

## Initialization

`vslices init` is responsible for establishing the project-local VSlices operating surface:

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
4. create `.ignore` if absent;
5. preserve project-owned configuration files during ruleset replacement unless the user explicitly requests a configuration reset in a future command surface.

`init --force` replaces the ruleset snapshot, not the project's unrelated local policy.

## Update direction

Project configuration is intended to support two distinct update paths:

```text
vslices update --self
  = update the CLI executable

vslices update --ruleset
  = update the project-local lowering knowledge
```

CLI version and ruleset version are deliberately independent.

The first self-update implementation should consume `updates.channel` and use the same configuration loading and override conventions as other commands rather than introducing update-specific settings machinery.
