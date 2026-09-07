# Semantic authoring affordances

Status: experimental design principle for the VSlices CLI authoring protocol.

This document defines the interaction model between `new vsir`, `discovery vsir`, and `update vsir`. It is a CLI interaction contract. VSIR language semantics remain owned by `vslices/intermediate-representation`.

## 1. Principle

Progressive VSIR authoring should be navigable from the current artifact state without requiring a human or AI client to know the complete authoring grammar in advance.

The protocol is:

```text
new
  -> establish an initial progressive state

discovery
  -> expose the semantic decisions currently available

update
  -> execute one or more advertised semantic transitions atomically
  -> produce the next valid progressive state

discovery
  -> evaluate the new state again
```

Conceptually:

```text
ArtifactState
  -> Discovery
  -> SemanticAffordances[]
  -> Update
  -> ArtifactState'
```

This is analogous to hypermedia-driven navigation: the current state advertises the transitions a client may follow next. The CLI applies the same idea to semantic authoring rather than HTTP resource navigation.

The governing rule is:

> An authoring client should not need to infer a valid next operation when the current state can advertise it.

## 2. Semantic affordance

A discovery entry is not only documentation. It is an executable authoring affordance.

An affordance should provide enough information for a client to understand:

```text
path
  -> which semantic decision is being addressed

status
  -> whether the decision is required or optional

meaning
  -> what semantic knowledge the decision establishes

value kind
  -> what shape of value is accepted

allowed values
  -> closed vocabulary when one is currently established

operations
  -> which transition operations are valid from the current state

command template
  -> how to invoke the transition through the CLI
```

Target discovery shape:

```text
state.Extensions
  status: optional
  meaning: establishes one state coordinate
  value kind: semantic-field-declaration
  operations: set
  command:
    vslices update vsir Location --set "state.Extensions=<semantic-field-declaration>"
```

For a set-valued surface, multiple operations may remain semantically meaningful:

```text
traits
  status: optional
  value kind: set<string>
  operations: add, remove, set
  commands:
    add:
      vslices update vsir Location --add "traits=<value>"
    remove:
      vslices update vsir Location --remove "traits=<value>"
    set:
      vslices update vsir Location --set "traits=<value[,value...]>"
```

Command templates are an intended part of the affordance contract. They are not yet emitted by the current implementation.

## 3. Operation semantics

Operations should describe semantic intent rather than incidental YAML existence.

### `set`

`set` means:

> Establish this semantic assertion with the supplied value.

For an ordinary semantic property or named map member, `set` should normally be valid whether that assertion is being established for the first time or replacing an existing value.

Desired behavior:

```text
state.Extensions absent
  + set state.Extensions={sequence: StreetExtension}
  -> establish the assertion

state.Extensions present
  + set state.Extensions={sequence: AnotherExtension}
  -> replace the assertion
```

A client should not need to select `add` merely because a YAML mapping key does not yet exist.

### `add`

`add` is reserved for collection semantics where union is meaningfully different from replacement.

Examples:

```text
tags
traits
```

### `remove`

`remove` withdraws a semantic assertion or collection member only when the current contract permits doing so.

Required facts may therefore expose `set` without `remove`.

### Ordered structures

Ordered structures without stable member identity should normally expose only whole-boundary `set`.

Current example:

```text
construction
  -> set complete ordered sequence
```

## 4. Discovery is state dependent

Discovery must be evaluated from the current artifact, not from a static list of all possible VSIR properties.

Examples:

```text
classification: maintained
  -> equality becomes required
  -> values becomes required
```

```text
traits contains transform
  -> input becomes required while absent
  -> construction becomes required while absent
```

```text
representation.Value has from
  -> mapping is not an available affordance
```

```text
representation.Value has mapping
  -> from is not an available affordance
```

After every successful update, a client should be able to call discovery again and receive the next authorized frontier.

## 5. Projection

`discovery` may project a candidate transition without persistence:

```text
vslices discovery vsir StreetName --set kind=domain-type
```

Projection must use the same mutation and candidate-validation path as `update` and then discard the candidate.

This lets a client ask:

> If I make this decision, what semantic affordances become available next?

without mutating the artifact.

## 6. Atomicity and fail-closed behavior

An advertised command is permission to attempt a transition, not permission to bypass validation.

Every update remains:

```text
read current artifact
  -> authorize requested transition
  -> parse supplied value according to its value kind
  -> build candidate
  -> validate candidate
  -> serialize
  -> atomic replacement
```

If any step fails, the original artifact remains unchanged.

Discovery must not advertise transitions that are known to be contradictory with the current state.

## 7. Agent-facing objective

A capable authoring agent should be able to begin with only this protocol knowledge:

```text
vslices new vsir <artifact>
vslices discovery vsir <artifact>
vslices update vsir <artifact> ...
```

It should not require an embedded copy of the complete VSIR authoring grammar.

The intended loop is:

```text
create
  -> discover
  -> choose an advertised affordance
  -> fill its value according to value kind / allowed values
  -> execute its advertised command form
  -> discover again
```

Tests claiming progressive CLI authorability should therefore exercise this loop. A test should not rely on a mutation that the preceding discovery state did not advertise.

## 8. Current implementation conformance

The current CLI already satisfies several parts of this model:

```text
implemented
  new establishes progressive artifacts
  discovery is computed from current artifact state
  update produces a new state atomically
  discovery projection shares the update mutation pipeline
  classification/trait obligations alter the frontier
  local from/mapping affordances depend on current field state
  unsupported transitions fail closed
  tests exist that traverse new -> discovery -> update -> discovery
```

There are two important remaining mismatches.

### 8.1 No command templates yet

Discovery currently reports:

```text
path
status
meaning
value kind
operations
allowed values
```

but does not emit the CLI command template that executes the affordance.

Target addition:

```text
command template(s)
```

The template should be derived from the same affordance contract rather than assembled independently in the presentation layer.

### 8.2 Map-member `add` versus `set` still reflects storage existence

Several current mutation boundaries distinguish:

```text
absent member -> add
existing member -> set
```

for ordinary state, representation, input, variants, and maintained members.

That behavior is operationally valid but does not yet match the desired semantic meaning of `set`.

The intended direction is:

```text
ordinary semantic assertion / named member
  -> set establishes or replaces
  -> remove when withdrawal is valid

set-valued collection
  -> add, remove, set

ordered structure without stable member identity
  -> set complete structure
```

This change should be made deliberately because it affects discovery output, mutation authorization, error codes, documentation, and existing tests.

## 9. Design constraint

The affordance model must not turn `discovery` into a generic YAML schema browser or `update` into a generic YAML editor.

An affordance exists only when VSlices Tooling has enough semantic authority to describe and validate the transition.

Unknown semantics remain unknown. Missing authority remains a closed frontier rather than an invitation to invent syntax.
