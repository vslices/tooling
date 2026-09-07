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
vslices update <subject>
vslices discovery <subject>
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

### `update <subject>`

Refines, synchronizes or changes an already-existing subject.

Examples of intended subjects include:

```text
update self
update ruleset
update vsir
```

An update must make the intended mutation explicit. Ambiguous flags should be avoided when the same property can support additive, subtractive and replacement operations.

### `discovery <subject>`

Inspects the immediately available decision frontier for the subject's current state without mutating it.

Discovery is intentionally local: it should expose only the decisions that become directly available from the current state rather than dumping the entire theoretical grammar.

Flags supplied to `discovery` are projections or hypotheses. They do not mutate the artifact; they ask what would become available if those values were assumed for the purpose of the query.

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

Tags are expected to evolve as more source evidence is inspected. That evolution belongs to `update vsir` rather than to a separate generic `replace` command.

## 5. Updating tags

Tag mutation uses explicit set operations so an AI or human agent does not need to guess whether a flag means append or replacement.

### Add tags

```text
vslices update vsir StreetName --add-tags identity
```

Semantics:

```text
result = current tags union requested tags
```

Adding a tag that already exists is idempotent and should not fail.

### Remove tags

```text
vslices update vsir StreetName --remove-tags street
```

Semantics:

```text
result = current tags difference requested tags
```

Removing a tag that is not present is idempotent and should not fail.

### Replace the complete tag set

```text
vslices update vsir StreetName --set-tags identity,addressing
```

Semantics:

```text
result = requested tags
```

`--set-tags` is intentionally explicit. A generic `--tags` flag on `update vsir` must not ambiguously mean either append or replacement.

### Combined additive and subtractive update

When both operations are admitted in one invocation, their meaning must be deterministic and documented. The intended model is to apply removals and additions as one explicit set transition over the current artifact rather than as hidden sequential CLI side effects.

For example:

```text
vslices update vsir StreetName \
  --remove-tags street \
  --add-tags identity,location
```

represents one requested transition from the current tag set to the resulting tag set.

A future implementation must define conflict behavior explicitly if the same tag is present in both `--add-tags` and `--remove-tags`; it must not resolve that contradiction silently.

## 6. Generic semantic mutation model

The tag-specific flags above are the first concrete form of a more general update model.

The intended abstraction is:

```text
UpdateOperation =
  target artifact
  + semantic path
  + mutation kind
  + optional value
```

The generic mutation kinds are:

```text
add
remove
set
```

For example, a future generic surface may express operations equivalent to:

```text
vslices update vsir StreetName --add tags identity
vslices update vsir StreetName --remove tags street
vslices update vsir StreetName --set classification identifier
vslices update vsir Location --set state.Region.from.value state.Commune.InProvince.InRegion
```

The exact CLI syntax is intentionally not fixed yet. The semantic model is.

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

Deeply nested paths are permitted conceptually when they address a semantic declaration recognized by the active contract. Depth itself is not authority.

## 7. Atomic update semantics

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

The original artifact must remain unchanged if any part of that process fails.

This gives `update` two forms of atomicity:

```text
semantic atomicity
  -> the command represents one complete requested transition

filesystem atomicity
  -> the artifact is never persisted in a partially-written state
```

A conforming implementation should write through a temporary file or equivalent mechanism and atomically replace the original only after the complete candidate has been accepted.

### Candidate-state validation

Validation is performed against the resulting candidate artifact, not against partially persisted intermediate states.

For example:

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

The core guarantee is:

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

## 8. Why this is `update`, not `replace`

The artifact continues to represent the same concept while knowledge about it changes.

```text
new
  -> introduces the artifact

update
  -> refines knowledge about the existing artifact
```

A future `replace` command should be introduced only if evidence reveals an operation whose meaning is genuinely substitution of one whole subject/materialization by another and cannot be expressed coherently as an update.

Editing a set-valued property such as tags, changing a nested semantic path, or applying several coordinated mutations atomically is not sufficient reason to introduce a top-level `replace` verb.

## 9. Intended relationship with discovery

Discovery should eventually expose both available declarations and valid mutations over already-known declarations.

For an artifact containing:

```yaml
tags: ['addressing', 'street']
```

`discovery vsir` may expose an equivalent conceptual result:

```text
tags
  current:
    addressing
    street

available operations:
  add
  remove
  set
```

More generally, discovery should expose valid mutations over semantic paths rather than forcing the caller to guess them.

Conceptually:

```text
path: tags
value kind: set<string>
operations:
  add
  remove
  set
```

while another declaration may expose:

```text
path: classification
value kind: enum
operations:
  set
```

and a nested declaration may eventually expose:

```text
path: state.Region.from.value
value kind: expression
operations:
  set
```

Discovery does not choose the mutation. It exposes the valid action frontier so the caller can decide from external evidence and then apply that choice through `update`.

This supports the migration loop:

```text
inspect source
  -> name/tag concepts
  -> discover valid next actions
  -> update only justified knowledge
  -> inspect more source
  -> reorganize/refine
  -> eventually lower sufficiently defined concepts
```

## 10. Agent-facing invariants

For progressive authoring, implementations should preserve these invariants:

- creating or updating an artifact must not silently infer unsupported semantic knowledge;
- `discovery` must not mutate filesystem or artifact state;
- explicit mutation operations should describe their operation rather than rely on overloaded meaning;
- generic path mutation must remain constrained by the active artifact contract;
- nested paths do not become valid merely because they are addressable syntactically;
- additive and subtractive set updates should be idempotent where no semantic contradiction exists;
- contradictory requested mutations must be reported rather than silently ordered away;
- a complete `update` invocation is one semantic transaction;
- candidate validation occurs before persistence;
- failure leaves the original artifact unchanged;
- current implementation limitations must not be mistaken for the conceptual limits of the CLI specification;
- language-level VSIR semantics remain owned by `vslices/intermediate-representation`; this document specifies CLI interaction semantics, not the VSIR language itself.

## 11. Current implementation status

On the semantic-locality experiment, `new vsir` exists as the first progressive authoring operation, including optional `kind`, `classification`, `shape`, `traits` and `tags` support.

The `update vsir --add-tags`, `--remove-tags` and `--set-tags` forms described above are specified here before implementation so their semantics are established before command mechanics are added.

Generic semantic-path mutation, transactional multi-mutation updates and `discovery <subject>` are likewise specified before implementation. Their mechanisms must be derived from the active artifact specification rather than hardcoded as an unconstrained YAML editing surface.

Likewise, the subject-oriented `update self`, `update ruleset` and `discovery <subject>` surface describes the intended CLI direction; older flag-oriented update behavior may remain temporarily while migration to that surface is evaluated.
