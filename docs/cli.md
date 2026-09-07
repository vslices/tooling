# VSlices CLI specification

Status: experimental. This document describes the intended command semantics of the `vslices` CLI while the progressive migration workflow is being explored. Implementation may temporarily lag behind the VSIR language specification; discrepancies must remain explicit.

## 1. Purpose

The CLI is an operational surface for creating, discovering, refining, locating and materializing VSlices artifacts without requiring a human or AI agent to know the final structure in advance.

The primary authoring rule is:

> Declare only what is currently justified by evidence.

The CLI therefore favors progressive transitions over one-shot generation.

Language-level VSIR semantics are owned by `vslices/intermediate-representation`. This document specifies CLI interaction semantics, not the VSIR language itself.

## 2. General command families

The general subject-oriented surface is:

```text
vslices init
vslices new <subject>
vslices discovery <subject>
vslices search --filter <property>:<operator>:<value>
vslices update <subject>
```

VSIR-specific transformation commands such as `lower`, `rebase` and `transpile` remain separate because they operate on VSIR lifecycle or materialization rather than general artifact-management verbs.

Implemented update subjects also include:

```text
vslices update self
vslices update ruleset
```

No flag-oriented compatibility aliases are retained while the CLI is experimental.

## 3. Progressive `new vsir`

The minimum useful act during reconstruction is naming a concept:

```text
vslices new vsir StreetName
```

which may create:

```yaml
vsir: 0.1
name: StreetName
```

The absence of `kind` or `classification` is not permission to infer them.

The currently implemented authoring flags are:

```text
--kind
--classification
--tags
```

For example:

```text
vslices new vsir StreetName --kind domain-type
```

or:

```text
vslices new vsir StreetName \
  --kind domain-type \
  --classification value-object \
  --tags addressing,street
```

which may produce:

```yaml
vsir: 0.1
kind: domain-type
name: StreetName
tags: ['addressing', 'street']
classification: value-object
```

`--classification` requires `--kind` because classification validity is kind-specific.

Tags are different: they are organizational and associative metadata, so `--tags` does not require a kind or classification and does not imply either one.

No other VSIR semantic declaration is currently implemented through `new vsir`. In particular, `shape`, `traits`, state, representation, input and construction remain outside the current CLI authoring frontier until their contracts are introduced deliberately.

## 4. Tags

Tags preserve provisional organizational knowledge without pretending that such knowledge is semantic classification.

```text
tags
  -> association / organization knowledge

kind / classification
  -> semantic knowledge
```

Tags do not imply kind, classification, traits, sections, lowering rules or target realizations.

They form a set of non-empty, single-line, unique strings. Their order is preserved when possible, but order has no semantic meaning.

Because tags may evolve independently from semantic classification, they remain available throughout progressive authoring rather than occupying one step in the `name -> kind -> classification` semantic sequence.

## 5. `search`

`search` locates VSIR artifacts beneath the current directory using an explicit property filter.

The filter grammar is:

```text
<property>:<operator>:<value>
```

For example:

```text
vslices search --filter tags:contains:identity-service
```

returns VSIR files whose root `tags` sequence contains the exact `identity-service` value.

The first implemented operators are:

```text
contains
  -> for a sequence, exact member containment
  -> for a scalar, ordinal substring containment

equals
  -> ordinal scalar equality
```

Examples:

```text
vslices search --filter tags:contains:identity-service
vslices search --filter classification:equals:value-object
vslices search --filter name:contains:Ticket
```

The first implementation filters root properties only. Unknown properties do not match. Unsupported operators fail closed rather than being guessed.

Search is read-only. It does not mutate VSIR artifacts, infer missing properties, or treat a textual match as semantic authority.

Search respects the existing artifact discovery policy, including built-in exclusions such as `.git`, `.vslices`, `bin` and `obj`, plus project `.vslices/.ignore` rules when available.

## 6. `discovery vsir`

`discovery vsir` exposes only the immediate semantic frontier currently implemented by Tooling, together with the always-available tags surface.

For a named artifact without `kind`:

```text
vslices discovery vsir StreetName

Immediate frontier:

  tags
    value kind: set<string>
    operations: add, remove, set

  kind
    value kind: enum
    operations: set
    values: domain-type
```

After `kind: domain-type` is established, `classification` becomes available while tags remain available:

```text
tags
  value kind: set<string>
  operations: add, remove, set

classification
  value kind: enum
  operations: set
  values: value-object, entity, identifier, maintained, aggregate-root
```

After classification is established, tags remain the only currently implemented authoring surface. This is an implementation boundary, not a claim that a classified VSIR artifact has no further semantic structure.

Discovery may project mutations without mutating the artifact:

```text
vslices discovery vsir StreetName --set kind=domain-type
vslices discovery vsir StreetName --add tags=addressing
```

The projected candidate is validated in memory and discarded after discovery.

## 7. `update vsir`

The current progressive VSIR update surface supports:

```text
kind
  -> set

classification
  -> set

tags
  -> add, remove, set
```

Examples:

```text
vslices update vsir StreetName --set kind=domain-type
```

```text
vslices update vsir StreetName \
  --set "kind=domain-type;classification=value-object"
```

```text
vslices update vsir StreetName --add tags=addressing,street
vslices update vsir StreetName --remove tags=street
vslices update vsir StreetName --set tags=identity,addressing
```

A complete invocation is one semantic/organizational transaction:

```text
read current artifact
  -> apply requested mutations to an in-memory candidate
  -> validate the candidate
  -> serialize candidate
  -> commit atomically
```

If validation or persistence fails, the original artifact remains unchanged.

Unsupported semantic paths or operations fail closed. Tooling must not become a generic YAML editor merely because a path can be addressed syntactically.

## 8. Current authoring frontier

The implemented semantic sequence remains deliberately small:

```text
name
  -> kind
  -> classification
```

Tags are orthogonal to that sequence:

```text
tags
  <-> may be added, removed or replaced at any current authoring stage
```

Concretely:

```text
new vsir
  -> may establish --tags
  -> may establish --kind
  -> may establish --classification when kind is known

discovery vsir
  -> always exposes tags
  -> exposes kind, then classification

search
  -> may filter current VSIR artifacts by an implemented root property filter

update vsir
  -> can add/remove/set tags
  -> can set kind and classification atomically
```

Nothing semantically after `classification` should be treated as implemented CLI behavior yet.

The next semantic authoring capability must be added only after its VSIR contract has been specified from evidence.

## 9. Agent-facing invariants

- creating or updating an artifact must not silently infer unsupported semantic knowledge;
- tags remain organizational metadata and must not imply semantic declarations;
- tags are non-empty, single-line and unique;
- `classification` is not writable before a compatible `kind` is established in the resulting candidate;
- `search` is read-only and explicit about its filter operator;
- unsupported search operators fail closed;
- `discovery` must not mutate filesystem or artifact state;
- discovery projections use the same candidate validation as update;
- a complete `update` invocation is one transaction;
- candidate validation occurs before persistence;
- failure leaves the original artifact unchanged;
- unsupported paths and operations fail closed;
- current implementation limitations must not be mistaken for conceptual VSIR limits;
- VSIR language semantics remain owned by `vslices/intermediate-representation`.
