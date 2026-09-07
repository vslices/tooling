# VSlices CLI specification

Status: experimental. This document describes the intended command semantics of the `vslices` CLI while the progressive migration workflow is being explored. Implementation may temporarily lag behind this specification; discrepancies must remain explicit.

## 1. Purpose

The CLI is an operational surface for creating, discovering, refining and materializing VSlices artifacts without requiring a human or AI agent to know the final structure in advance.

The primary authoring rule is:

> Declare only what is currently justified by evidence.

A command must not force an agent to invent semantic knowledge merely to satisfy a complete final form.

The CLI therefore favors progressive transitions over one-shot generation.

## 2. General command families

The general surface is organized around subjects:

```text
vslices init
vslices new <subject>
vslices discovery <subject>
vslices update <subject>
```

VSIR-specific transformation commands such as `lower`, `rebase` and `transpile` remain a separate dialect because they operate on the lifecycle or materialization of VSIR rather than expressing general artifact-management verbs.

### `init`

Initializes a VSlices project context.

### `new <subject>`

Creates a new instance of a recognized subject from the facts currently known.

Creation must be progressive. Optional flags may add knowledge, but the command must not require knowledge that has not yet been established unless that knowledge is necessary to identify the created subject.

Examples of intended subjects include:

```text
new vsir
new project
new document
new continuity-path
```

Not every subject is implemented yet.

### `discovery <subject>`

Inspects the immediately available decision frontier for the subject's current state without mutating it.

Discovery is intentionally local: it exposes only decisions directly available from the current state rather than dumping the entire theoretical grammar.

Flags supplied to `discovery` are projections or hypotheses. They do not mutate the artifact; they ask what would become available if those mutations were applied only to an in-memory candidate used by the query.

### `update <subject>`

Refines, synchronizes or changes an already-existing subject.

Implemented subjects include:

```text
update self
update ruleset
update vsir
```

The subject is always explicit. Flag-oriented subject selection such as `update --self` or `update --ruleset` is not part of the CLI contract.

## 3. Progressive `new vsir`

The minimum useful act during reconstruction is often naming a concept.

Therefore:

```text
vslices new vsir StreetName
```

is valid and may create:

```yaml
vsir: 0.1
name: StreetName
```

The absence of `kind` is not permission to infer one. It records that the concept has been named before its artifact family has been established.

When additional evidence already exists, creation may be more specific:

```text
vslices new vsir StreetName --kind domain-type
```

or, with further justified knowledge:

```text
vslices new vsir SrvIdentityId \
  --kind domain-type \
  --classification identifier \
  --shape product \
  --traits refined,transform
```

Kind-specific choices must not be accepted before the `kind` that authorizes them is known.

## 4. Tags as organizational knowledge

Tags support early migration and reconstruction work where concepts have been named but their final semantic organization is not yet known.

Example:

```text
vslices new vsir StreetName --tags addressing,street
```

may create:

```yaml
vsir: 0.1
name: StreetName
tags: ['addressing', 'street']
```

Tags are organizational and associative metadata. They are deliberately weaker than semantic declarations.

```text
tags
  -> provisional association / organization knowledge

kind / classification / traits
  -> semantic knowledge
```

A tag must not by itself imply a `kind`, `classification`, trait, section, lowering rule or target realization.

Tags are expected to evolve as more source evidence is inspected. That evolution belongs to the generic `update vsir` mutation model.

## 5. Generic semantic mutation model

`update vsir` uses one generic mutation model:

```text
UpdateOperation =
  target artifact
  + semantic path
  + mutation kind
  + optional value
```

The mutation kinds are:

```text
add
remove
set
```

The CLI uses `path=value` clauses:

```text
vslices update vsir StreetName --add tags=identity
vslices update vsir StreetName --remove tags=street
vslices update vsir StreetName --set classification=identifier
```

Multiple clauses of one mutation kind can be supplied in one option by separating clauses with `;`:

```text
vslices update vsir StreetName \
  --set "kind=domain-type;classification=value-object"
```

The complete invocation is one transaction.

For a set-valued path such as `tags`:

```text
add
  -> current union requested

remove
  -> current difference requested

set
  -> requested replaces current
```

Add and remove are idempotent when there is no semantic contradiction. Adding an existing value or removing an absent value does not fail.

There are no path-specific compatibility flags such as `--add-tags`, `--remove-tags` or `--set-tags`. `tags` is updated through the same generic semantic-path mechanism as every other writable declaration.

If the same value is requested for both addition and removal of the same path in one transaction, the command reports a contradiction and leaves the artifact unchanged.

`add`, `remove` and `set` are structural mechanisms, not semantic authority. A path, mutation kind and value are valid only when the active artifact contract authorizes that combination.

Therefore the CLI must not become a generic YAML editor.

```text
arbitrary path mutation
  -> not authorized merely because the path can be written

artifact specification / active contracts
  -> determine which paths exist
  -> determine which mutation kinds are valid
  -> determine which values are admissible
```

Deeply nested paths are permitted by the model only when they address a semantic declaration recognized by the active contract. The current authoring contract intentionally rejects deep paths whose contracts have not yet been specified. Depth itself is not authority.

## 6. Atomic update semantics

`update` is a semantic transition over an artifact, not a sequence of partial file edits.

A single invocation may contain one or more requested mutations. The complete invocation is treated as one transaction over the artifact.

Conceptually:

```text
read current artifact
  -> parse current state
  -> resolve and authorize every requested mutation
  -> apply mutations to an in-memory candidate
  -> validate the candidate result
  -> serialize candidate
  -> commit the resulting artifact atomically
```

The original artifact remains unchanged if any part of that process fails.

This gives `update` two forms of atomicity:

```text
semantic atomicity
  -> the command represents one complete requested transition

filesystem atomicity
  -> the artifact is never persisted in a partially-written state
```

The implementation writes through the existing atomic file-write mechanism and replaces the original only after the complete candidate has been accepted.

### Candidate-state validation

Validation is performed against the resulting candidate artifact, not against partially persisted intermediate states.

```text
current artifact A
requested mutations m1, m2, m3

A
  -> apply m1 in memory
  -> apply m2 in memory
  -> apply m3 in memory
  -> candidate B
  -> validate B
```

Only `B` is eligible for persistence.

If `m3` makes the candidate invalid, none of `m1`, `m2` or `m3` is persisted.

This allows a transaction to introduce multiple mutually-dependent declarations together without requiring each intermediate in-memory state to be independently persistable.

### Failure invariant

> `update` applies one or more semantic mutations as a single atomic transition over an artifact. Either the resulting artifact is accepted and committed completely, or the original artifact remains unchanged.

A mutation failure, authorization failure, validation failure, serialization failure or persistence failure must not leave a partially-updated artifact behind.

### Why this matters for AI agents

An AI agent should not need to rewrite an entire artifact merely to change one semantic fact.

Instead of:

```text
read whole file
  -> reconstruct whole file with one change
  -> overwrite whole file
```

the preferred interaction is:

```text
identify one justified semantic change
  -> express path + mutation
  -> let Tooling preserve and validate the rest of the artifact
```

This reduces accidental loss, formatting drift, stale knowledge and unrelated edits while keeping the agent's requested authority narrowly scoped.

## 7. Why this is `update`, not `replace`

The artifact continues to represent the same concept while knowledge about it changes.

```text
new
  -> introduces the artifact

update
  -> refines knowledge about the existing artifact
```

A future `replace` command should be introduced only if evidence reveals an operation whose meaning is genuinely substitution of one whole subject/materialization by another and cannot be expressed coherently as an update.

Editing a set-valued property such as tags, changing a nested semantic path, or applying several coordinated mutations atomically is not sufficient reason to introduce a top-level `replace` verb.

## 8. `discovery vsir`

`discovery vsir` exposes valid mutations over the immediate semantic frontier of the current artifact.

For a named artifact with no `kind`, the current contract exposes organizational tagging plus the next semantic decision:

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

After `kind: domain-type` is known, the immediate semantic frontier advances to `classification`. It does not eagerly expose later choices such as `shape` or `traits` until classification is established.

Discovery can project mutations without persisting them:

```text
vslices discovery vsir StreetName --set kind=domain-type
```

The command builds the projected candidate in memory, validates it using the same mutation mechanism as `update`, and reports the frontier that would follow. The source artifact is not modified.

This yields the authoring protocol:

```text
new
  -> establish known facts

discovery
  -> inspect immediate authorized frontier

update
  -> atomically advance the artifact
```

## 9. Agent-facing invariants

For progressive authoring, implementations preserve these invariants:

- creating or updating an artifact must not silently infer unsupported semantic knowledge;
- `discovery` must not mutate filesystem or artifact state;
- discovery projections use the same mutation authorization and candidate validation as update;
- explicit mutation operations describe their operation rather than rely on overloaded meaning;
- generic path mutation remains constrained by the active artifact contract;
- nested paths do not become valid merely because they are addressable syntactically;
- additive and subtractive set updates are idempotent where no semantic contradiction exists;
- contradictory requested mutations are reported rather than silently ordered away;
- a complete `update` invocation is one semantic transaction;
- candidate validation occurs before persistence;
- failure leaves the original artifact unchanged;
- current implementation limitations must not be mistaken for the conceptual limits of the CLI specification;
- language-level VSIR semantics remain owned by `vslices/intermediate-representation`; this document specifies CLI interaction semantics, not the VSIR language itself.

## 10. Current implementation status

On the semantic-locality experiment, the progressive VSIR authoring loop is implemented for the currently specified frontier:

```text
vslices new vsir
vslices discovery vsir
vslices update vsir
```

`new vsir` supports optional `kind`, `classification`, `shape`, `traits` and `tags` when those facts are already justified.

`update vsir` supports only the contract-constrained generic `add`, `remove` and `set` operations. The complete requested mutation set is applied to an in-memory candidate and committed atomically only after candidate validation succeeds.

`discovery vsir` reports the immediate frontier and supports non-persisted generic mutation projections.

The subject-oriented lifecycle forms are:

```text
vslices update self
vslices update ruleset
```

No compatibility aliases are retained while this CLI is still experimental and has a single active user.

Deep semantic paths remain intentionally unavailable until their owning VSIR contracts are specified. The mechanism must grow from specification evidence rather than becoming an unconstrained YAML editing surface.
