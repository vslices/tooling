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

No other VSIR semantic declaration is currently implemented through `new vsir`. In particular, `shape`, `traits`, state, representation, input and construction remain outside the current `new vsir` surface until their contracts are introduced deliberately.

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

`discovery vsir` exposes the immediate semantic frontier currently implemented by Tooling, together with always-available organizational surfaces.

Each discovered attribute explains both its role and its current obligation status:

```text
status: required | optional
meaning: <semantic explanation>
value kind: <expected structure>
operations: <currently supported mutations>
```

A required attribute is part of the contract activated by the artifact's current semantic declarations. An optional attribute is available but is not required merely by the current state.

For a named artifact without `kind`:

```text
vslices discovery vsir StreetName

Immediate frontier:

  tags
    status: optional
    meaning: Organizational labels used to associate and search artifacts. Tags do not imply semantic behavior.
    value kind: set<string>
    operations: add, remove, set

  kind
    status: required
    meaning: Selects the VSIR artifact family and determines which declarations are meaningful for the artifact.
    value kind: enum
    operations: set
    values: domain-type
```

After `kind: domain-type` is established, `classification` becomes available while tags remain available.

For a classified value object such as:

```yaml
vsir: 0.1
kind: domain-type
name: StreetName
tags: ['addressing', 'street']
classification: value-object
```

classification obligations become visible:

```text
state
  status: required
  meaning: Declares the observable semantic properties that constitute a valid instance of the Domain Type.
  value kind: mapping
  operations: not implemented

representation
  status: required
  meaning: Declares the observable form through which a valid Domain Type can be represented without changing its semantic validity.
  value kind: mapping
  operations: not implemented

traits
  status: optional
  meaning: Declares additional semantic capabilities that are not already implied by the Domain Type classification.
  value kind: set<string>
  operations: add, remove, set
```

`state` and `representation` are reported as required for `value-object`, `entity`, and `aggregate-root` because those obligations are established by the VSIR specification. Their structured mutation syntax is not yet implemented, so discovery makes the requirement visible without inventing an editing mechanism.

`traits` is currently writable after a Domain Type classification is known:

```text
vslices update vsir StreetName --add traits=transform
```

Discovery may project supported mutations without mutating the artifact:

```text
vslices discovery vsir StreetName --set kind=domain-type
vslices discovery vsir StreetName --add tags=addressing
vslices discovery vsir StreetName --add traits=transform
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

traits
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

```text
vslices update vsir StreetName --add traits=transform
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

The implemented semantic sequence now reaches the first classification obligations:

```text
name
  -> kind
  -> classification
  -> required state / representation where implied
  -> optional traits
```

Tags remain orthogonal to that sequence:

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
  -> after value-object/entity/aggregate-root, exposes missing state and representation as required
  -> after classification, exposes traits as optional

search
  -> may filter current VSIR artifacts by an implemented root property filter

update vsir
  -> can add/remove/set tags and traits
  -> can set kind and classification atomically
  -> does not yet author structured state or representation mappings
```

The next structured authoring capability must be added only after its mutation contract has been specified from evidence.

## 9. Agent-facing invariants

- creating or updating an artifact must not silently infer unsupported semantic knowledge;
- tags remain organizational metadata and must not imply semantic declarations;
- tags are non-empty, single-line and unique;
- `classification` is not writable before a compatible `kind` is established in the resulting candidate;
- `value-object`, `entity`, and `aggregate-root` discovery exposes missing `state` and `representation` as required obligations;
- discovery distinguishes required obligations from optional authoring surfaces;
- every discovered attribute carries a concise explanation of what it means;
- reporting a required attribute does not authorize mutation when its editing contract is not yet implemented;
- `traits` is an optional Domain Type capability surface and supports add/remove/set after a Domain Type kind is established;
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
