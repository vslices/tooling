# VSIR artifact states

Status: experimental terminology for the progressive authoring/lowering surface.

These states are **derived from evidence**. They are not persisted as `phase`, `status`, or another lifecycle field in `.vsir`.

## Progressive validity

A progressively valid artifact is admissible to the authoring protocol even when required semantic decisions remain unknown or an already-present assertion needs an explicitly advertised repair.

For example:

```yaml
vsir: 0.1
name: Rut
```

may be progressively valid while `kind` is still unknown. `discovery` can therefore expose `kind` as a required next decision without inventing it.

Likewise, an admitted but invalid assertion such as:

```yaml
kind: domain-type
shape: triangle
```

can remain progressively valid when discovery can safely expose the repair boundary:

```text
shape
  operations: set
  values: product, sum
```

while conformance is reported as invalid. Tooling must not continue past that invalid discriminator and advertise dependent semantic choices as if `triangle` had acquired meaning.

Progressive validity therefore answers:

> Does the artifact have enough trustworthy structure for Tooling to expose an authorized next transition or repair boundary?

It does **not** mean “the YAML parses”, and it does **not** claim that the artifact is already a conforming VSIR document.

An artifact without the progressive identity needed to navigate safely, such as one missing `name`, is rejected by `discovery` rather than being presented with a speculative frontier.

## VSIR conformance

A conforming artifact satisfies the canonical VSIR 0.1 language and semantic validation contract for the represented artifact.

Conformance answers:

> Is the semantic source complete and valid according to the current VSIR language authority and validation environment?

An artifact can therefore be:

```text
progressively valid
+ conformationally incomplete
```

when justified decisions are still missing, or:

```text
progressively valid
+ conformationally invalid
```

when a present assertion is invalid but discovery can expose an explicit repair without inventing dependent semantics.

Required obligations are checked against their complete semantic paths. A required `state.Value` is not satisfied merely because a `state` mapping exists.

When all required authoring decisions are present, Tooling parses the same canonical source through `VsirLanguageParser`; success establishes current executable conformance, while diagnostics establish invalidity.

## Lowerability

Lowerability is not a property of the `.vsir` document alone.

It additionally depends on the requested target and the active environment, including:

```text
conforming VSIR
+ target selection
+ installed Ruleset knowledge
+ project semantic extensions, when relevant
+ target/project context
+ available lowering mechanisms
```

Therefore `discovery vsir` deliberately reports lowerability as **not evaluated**. The `lower` path is the authority that establishes whether the current artifact can be materialized for the requested target/context.

A conforming artifact may still be non-lowerable because target knowledge is missing. That is a lowering boundary, not permission to weaken conformance or invent semantics.

## Relationship

```text
progressive-valid
    -> enough trustworthy structure to expose the next authorized transition or repair

conforming
    -> canonical semantic source is complete and valid

lowerable(target, context)
    -> conforming source plus sufficient target knowledge/context can be materialized
```

These are related but non-equivalent predicates.

No lifecycle phase is stored in VSIR. Re-evaluating the current source and environment is the source of truth.

## Canonical surface

This experiment supports one canonical VSIR 0.1 surface. Pre-normalized experimental grammars are migration inputs, not compatibility targets. Fixtures and consumers should be migrated rather than routed through a second parser or lowerer.
