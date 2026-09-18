# VSlices project configuration

`.vslices/config.yaml` represents project-specific operating policy. It does not redefine VSIR semantics.

A minimal initialized configuration is:

```yaml
version: 0.1

targets:
  default: csharp

lineage:
  bootstrap:
    convention: existing-materialization

updates:
  source: https://github.com/vslices/tooling
  channel: preview
```

`vslices init` creates only the minimum project surface required for later VSlices operations. It does not imply that Ruleset or Docs Standard are already installed.

External knowledge is installed independently:

```text
vslices update ruleset
vslices update docs-standard
vslices update template-standard
```

Initialization may compose those same operations as convenience through `--ruleset-origin`, `--docs-standard-origin`, `--template-standard-origin`, or `--default-origin`; it does not own a second installation mechanism.

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
  = optional local target-lowering snapshot

.vslices/docs-standard/
  = optional local normative documentation-vocabulary snapshot

.vslices/template-standard/
  = optional local normative materialization-template snapshot

.vslices/lineage/
  = operational deterministic ancestry evidence
```

`VSlicesProjectContext` resolves these paths from the nearest `.vslices/config.yaml`. Other Tooling components reuse that context rather than independently inferring parent relationships.

## Target configuration

`targets.default` selects the normal target when `-to` is omitted. Explicit target arguments remain authoritative.

C# namespace derivation can exclude directory segments from the project-relative VSIR path:

```yaml
targets:
  default: csharp
  csharp:
    namespace:
      ignore-folders:
        - "Aggregates/*"
        - "Aggregates/**/Entities"
        - "Aggregates/**/*"
        - Entities
```

The normal namespace derivation remains:

```text
evaluated RootNamespace
+ project-relative VSIR directory segments
```

`ignore-folders` rules are evaluated against the directory path relative to the related `.csproj`. The complete pattern provides context, but a successful match excludes only the final directory segment represented by that pattern; matching parent segments are not removed merely because they participated in the match.

Pattern semantics are:

```text
literal segment  exact ordinal segment match
*                any characters within exactly one directory segment
?                exactly one character within one directory segment
**               zero or more complete directory segments
```

A rule without a path separator remains a global segment convention. For example, `Entities` ignores any complete directory segment named `Entities`, while `*Internal` matches any single segment ending in `Internal`.

Path-aware examples:

```text
Aggregates/*
  -> ignores each direct child of Aggregates
  -> Aggregates/Tickets            => ignore Tickets
  -> Aggregates/Orders             => ignore Orders

Aggregates/Tickets
  -> ignores Tickets only in that exact path context

Aggregates/Tickets/Entities
  -> ignores Entities only below Aggregates/Tickets

Aggregates/**/Entities
  -> ignores an Entities folder at any depth below Aggregates

Aggregates/**/*
  -> ignores every descendant folder below Aggregates
  -> Aggregates itself remains part of the namespace
```

For example:

```text
RootNamespace: Tickets.Domain
VSIR path:     Aggregates/Tickets/Entities/History/TicketHistory.vsir
ignore:        Aggregates/**/*
result:        Tickets.Domain.Aggregates
```

The recursive `**` segment is contextual rather than the directory target itself. Consequently, recursive "ignore all descendants" uses `**/*`; a path rule ending directly in `**` is not used to remove a directory segment.

This lets physical organization be more expressive than target namespace organization without hardcoding aggregate names or folder conventions into Tooling.

An explicit `--namespace` remains authoritative and bypasses derived namespace configuration.

## Ruleset provenance

`ruleset.source` records where the project-local snapshot is acquired from.

Supported source shapes currently include:

- local directory;
- direct HTTP(S) ZIP archive;
- supported GitHub repository URL.

For a GitHub repository source, `ruleset.ref` is a real Git reference candidate. Acquisition tries branch, tag and then direct archive/commit reference forms. This preserves support for experimental branch names while making `ref` honest enough to represent tags and commits too.

A local directory with `ruleset.ref` is rejected rather than silently treating the value as a branch. A generic direct ZIP URL likewise does not gain Git-ref semantics.

If no Ruleset provenance has been configured yet, `vslices update ruleset` resolves the official origin:

```text
source: https://github.com/vslices/ruleset
ref: main
```

A successful first installation records that provenance in `.vslices/config.yaml`. Later plain updates reuse it.

## Docs Standard provenance

After a Docs Standard snapshot has been installed from an explicit source, Tooling records its provenance in project configuration:

```yaml
docs-standard:
  source: https://github.com/vslices/docs-standard
  ref: feat/document-authoring-preview
```

`docs-standard.source` and optional `docs-standard.ref` play the same role for documentary vocabulary that `ruleset.source` and `ruleset.ref` play for target-lowering knowledge.

## Template Standard provenance

After a Template Standard snapshot has been installed successfully, Tooling records its provenance in project configuration:

```yaml
template-standard:
  source: https://github.com/vslices/template-standard
  ref: feat/initial-markdown-template
```

`template-standard.source` and optional `template-standard.ref` identify the materialization vocabulary used by the project. The snapshot is installed at `.vslices/template-standard`.

Ruleset, Docs Standard, and Template Standard use the same update precedence:

```text
explicit --origin
  > compatibility --from / --ref
  > configured source / ref
  > official source / main
```

The preferred compact GitHub syntax is:

```text
owner/repository:ref
```

For example:

```text
vslices update ruleset --origin vslices/ruleset:main
vslices update docs-standard --origin vslices/docs-standard:feat/document-authoring-preview
vslices update template-standard --origin vslices/template-standard:feat/initial-markdown-template
```

Local directories and direct ZIP origins may be passed directly without a ref.

A successful first installation records the resolved source/ref only after the candidate snapshot has been materialized and validated. A failed candidate does not replace the known-good provenance.

Consequently, later invocations may use:

```text
vslices update ruleset
vslices update docs-standard
vslices update template-standard
```

to refresh from their recorded origins.

Older project configurations that predate this provenance field remain readable. Because an already-installed snapshot does not itself prove which source/ref produced it, such projects need one explicit successful update to establish provenance before argument-free updates can reuse it.

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

## External snapshot updates

`vslices update ruleset`, `vslices update docs-standard`, and `vslices update template-standard` are all first-install and refresh operations.

Each operation:

```text
resolve origin
-> materialize candidate
-> validate completely
-> atomically replace its project-local snapshot
-> persist successful provenance
```

Ruleset validation uses the real target loader. For C#, this includes `CSharpLoweringRuleSet.Load`, so missing declared files, duplicate rule nodes, unsupported rule mode/renderer and missing templates prevent replacement.

Docs Standard validation follows the manifest-reachable normative definitions and rejects malformed or incomplete candidates before replacement.

`vslices update self` remains independent.

## CLI update policy

`updates.source`, `updates.channel` and optional `updates.pull-request` configure standalone CLI self-update. Supported channels remain `stable`, `preview`, and `build`; explicit self-update flags override project values for one invocation.

## Artifact discovery

Project-specific recursive-discovery exclusions live in `.vslices/.ignore`. Built-in exclusions remain `.git/`, `.vslices/`, `bin/`, and `obj/`.

Configuration cannot disable correctness/safety guarantees such as atomic writes, missing-rule failure, complete ruleset validation before swap, or trusted-ancestry requirements for rebase.
