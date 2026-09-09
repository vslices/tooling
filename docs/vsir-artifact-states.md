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

can remain progressively valid when discovery can safely expose the current public repair boundary:

```text
shape
  operations: set
  values: product
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

Canonical conformance is deliberately independent from the narrower public authoring surface:

```text
canonical conformance
  = VsirParser + canonical parser/validator support
  + active semantic validation environment

public authorability
  = transitions discovery/update currently know how to offer safely
```

This means a form can be:

```text
conforming
+ public semantic authoring gated
```

For example, current executable evidence establishes canonical parser/validation/lowering support for `sum` and `maintained` Domain Types, while public `new/discovery/update` authoring parity for those forms remains gated. Discovery must not misreport such an artifact as invalid merely because it does not advertise a transition that authors the form.

An artifact can also be:

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

Required authoring obligations are checked against their complete semantic paths while the artifact is incomplete. A required `state.Value` is not satisfied merely because a `state` mapping exists.

Whenever the canonical source is complete enough to conform, Tooling evaluates it through the public `VsirParser` boundary. That parser dispatches to the appropriate executable parser/validator for the canonical form and receives the active `VsirValidationContext` when project semantic extensions exist. Parser success establishes current executable conformance; parser diagnostics establish invalidity once the artifact is no longer merely missing an advertised required authoring decision.

`discovery` therefore loads `.vslices/extensions` from the owning project when available before it claims conformance. This does **not** make discovery a lowering command: target selection and target realization are still excluded from conformance.

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
    -> canonical semantic source is complete and valid in its semantic environment

public-authorable
    -> discovery/update have proven transitions for authoring that semantic form

lowerable(target, context)
    -> conforming source plus sufficient target knowledge/context can be materialized
```

These are related but non-equivalent predicates.

No lifecycle phase is stored in VSIR. Re-evaluating the current source and environment is the source of truth.

## Canonical surface

This experiment supports one canonical VSIR 0.1 surface. Pre-normalized experimental grammars are migration inputs, not compatibility targets. Fixtures and consumers should be migrated rather than routed through a second parser or lowerer.
