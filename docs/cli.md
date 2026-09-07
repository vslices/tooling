# VSlices CLI specification

Status: experimental. This document describes the intended command semantics of the `vslices` CLI while the progressive migration workflow is being explored. Implementation may temporarily lag behind the VSIR language specification; discrepancies must remain explicit.

## 1. Purpose

The CLI is an operational surface for creating, discovering, refining and materializing VSlices artifacts without requiring a human or AI agent to know the final structure in advance.

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

The currently implemented semantic authoring flags are only:

```text
--kind
--classification
```

For example:

```text
vslices new vsir StreetName --kind domain-type
```

or:

```text
vslices new vsir StreetName \
  --kind domain-type \
  --classification value-object
```

`--classification` requires `--kind` because classification validity is kind-specific.

No other VSIR semantic declaration is currently implemented through `new vsir`. In particular, `shape`, `traits`, tags, state, representation, input and construction are intentionally outside the current CLI authoring frontier until their contracts are specified and introduced deliberately.

## 4. `discovery vsir`

`discovery vsir` exposes only the immediate semantic frontier currently implemented by Tooling.

For a named artifact without `kind`:

```text
vslices discovery vsir StreetName

Immediate frontier:

  kind
    value kind: enum
    operations: set
    values: domain-type
```

After `kind: domain-type` is established, the frontier advances to `classification`:

```text
classification
  value kind: enum
  operations: set
  values: value-object, entity, identifier, maintained, aggregate-root
```

After classification is established, the current implementation reports no further semantic frontier.

This is an implementation boundary, not a claim that a classified VSIR artifact has no further semantic structure.

Discovery may project a `set` transition without mutating the artifact:

```text
vslices discovery vsir StreetName --set kind=domain-type
```

The projected candidate is validated in memory and discarded after discovery.

## 5. `update vsir`

The current progressive VSIR update surface supports only `set` mutations over the semantic paths already implemented:

```text
kind
classification
```

Examples:

```text
vslices update vsir StreetName --set kind=domain-type
```

```text
vslices update vsir StreetName \
  --set "kind=domain-type;classification=value-object"
```

A complete invocation is one semantic transaction:

```text
read current artifact
  -> apply requested set mutations to an in-memory candidate
  -> validate the candidate
  -> serialize candidate
  -> commit atomically
```

If validation or persistence fails, the original artifact remains unchanged.

Unsupported semantic paths fail closed. Tooling must not become a generic YAML editor merely because a path can be addressed syntactically.

## 6. Current authoring frontier

The implemented progressive authoring sequence is deliberately small:

```text
name
  -> kind
  -> classification
```

Concretely:

```text
new vsir
  -> may establish --kind
  -> may establish --classification when kind is known

discovery vsir
  -> exposes kind, then classification

update vsir
  -> can set kind and classification atomically
```

Nothing after `classification` should be treated as implemented CLI behavior yet.

The next authoring capability must be added only after its VSIR contract has been specified from evidence.

## 7. Agent-facing invariants

- creating or updating an artifact must not silently infer unsupported semantic knowledge;
- `classification` is not writable before a compatible `kind` is established in the resulting candidate;
- `discovery` must not mutate filesystem or artifact state;
- discovery projections use the same candidate validation as update;
- a complete `update` invocation is one semantic transaction;
- candidate validation occurs before persistence;
- failure leaves the original artifact unchanged;
- unsupported paths fail closed;
- current implementation limitations must not be mistaken for conceptual VSIR limits;
- VSIR language semantics remain owned by `vslices/intermediate-representation`.
