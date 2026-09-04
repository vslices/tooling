# VSlices project configuration

`.vslices/config.yaml` represents project-specific operating policy. It does not redefine VSIR semantics.

A normal initialized configuration is:

```yaml
version: 0.1

targets:
  default: csharp

ruleset:
  source: https://github.com/vslices/ruleset
  ref: main

lineage:
  bootstrap:
    convention: existing-materialization

updates:
  source: https://github.com/vslices/tooling
  channel: preview
```

Operational precedence is:

```text
explicit CLI argument
  > project configuration
  > executable default
```

## Project surface

```text
.vslices/config.yaml
  = operating policy

.vslices/.ignore
  = project-specific discovery exclusions

.vslices/ruleset/
  = local target-lowering snapshot

.vslices/lineage/
  = operational deterministic ancestry evidence
```

`VSlicesProjectContext` resolves these paths from the nearest `.vslices/config.yaml`. Other Tooling components reuse that context rather than independently inferring parent relationships.

## Target configuration

`targets.default` selects the normal target when `-to` is omitted. Explicit target arguments remain authoritative.

C# namespace derivation can exclude exact directory segments from the project-relative VSIR path:

```yaml
targets:
  default: csharp
  csharp:
    namespace:
      ignore-folders:
        - Tickets
```

The normal namespace derivation remains:

```text
evaluated RootNamespace
+ project-relative VSIR directory segments
```

Configured `ignore-folders` entries are removed only when an entire path segment matches exactly. They do not use globbing, prefixes, suffixes, or regular expressions.

Example:

```text
RootNamespace: Tickets.Domain
VSIR path:     Aggregates/Tickets/TicketCode.vsir
ignore:        Tickets
result:        Tickets.Domain.Aggregates
```

An explicit `--namespace` remains authoritative and bypasses derived namespace configuration.

## Ruleset provenance

`ruleset.source` records where the project-local snapshot is acquired from.

Supported source shapes currently include:

- local directory;
- direct HTTP(S) ZIP archive;
- supported GitHub repository URL.

For a GitHub repository source, `ruleset.ref` is a real Git reference candidate. Acquisition tries branch, tag and then direct archive/commit reference forms. This preserves support for experimental branch names while making `ref` honest enough to represent tags and commits too.

A local directory with `ruleset.ref` is rejected rather than silently treating the value as a branch. A generic direct ZIP URL likewise does not gain Git-ref semantics.

The official defaults are:

```text
source: https://github.com/vslices/ruleset
ref: main
```

## Lineage bootstrap

The first supported convention is:

```yaml
lineage:
  bootstrap:
    convention: existing-materialization
```

Its exact meaning is:

```text
existing conventional materialization
+ no lineage
+ configured bootstrap convention

  -> compute current deterministic projection
  -> record that deterministic projection in .vslices/lineage
  -> preserve the human materialization byte-for-byte
  -> succeed without immediate rebase
```

The human `.vsir.cs` is not declared to be a deterministic historical baseline. The configuration only authorizes lineage to begin at the current point.

On a later semantic change:

```text
stored previous deterministic
+ current human witness
+ next deterministic projection
-> rebase
```

The convention does not apply to an explicit `--source` override or a non-conventional materialization with no trusted ancestry.

## Lineage versioning

`.vslices/lineage/` is intended to be version-controlled by default.

Reason: another developer, machine or CI process should be able to reconstruct the same automatic three-way rebase from repository state. Lineage remains operational evidence, not semantic authority. Tooling does not currently infer missing ancestry from Git history, and no provenance graph is introduced.

## Ruleset updates

`vslices update --ruleset` is implemented.

The operation uses configured `ruleset.source` and `ruleset.ref`, materializes a candidate snapshot, copies only the selected target plus root files, validates the prepared snapshot with the real target loader and only then performs atomic replacement with backup/rollback.

For C#, validation includes `CSharpLoweringRuleSet.Load`, so missing declared files, duplicate rule nodes, unsupported rule mode/renderer and missing templates prevent replacement.

`vslices update --self` remains independent. Plain `vslices update` and combined `--self --ruleset` remain undefined while aggregate ordering and partial-failure semantics are still under study.

## CLI update policy

`updates.source`, `updates.channel` and optional `updates.pull-request` configure standalone CLI self-update. Supported channels remain `stable`, `preview`, and `build`; explicit self-update flags override project values for one invocation.

## Artifact discovery

Project-specific recursive-discovery exclusions live in `.vslices/.ignore`. Built-in exclusions remain `.git/`, `.vslices/`, `bin/`, and `obj/`.

Configuration cannot disable correctness/safety guarantees such as atomic writes, missing-rule failure, complete ruleset validation before swap, or trusted-ancestry requirements for rebase.
