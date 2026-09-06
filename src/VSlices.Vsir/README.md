# VSlices.Vsir

`VSlices.Vsir` owns the experimental semantic model, parsing and conservative validation of VSIR.

This README is the closest orientation surface for humans and AI agents changing VSIR semantics. It is intentionally kept beside the implementation so the semantic model can evolve with the parser, validator and tests instead of depending on consumer-project notes or chat history.

It is not an exhaustive schema reference. Read the current model, parser, validator and tests before changing behavior.

## Purpose

VSIR is a structured semantic representation for software artifacts.

Its job is to preserve enough meaning that another actor can reason about, validate and materialize an artifact without treating one target-language implementation as the semantic source.

A useful relation is:

```text
ConcreteImplementation |= VSIR
```

A deterministic materialization is one valid witness of the semantic contract. It is not necessarily the only valid source form.

VSIR is designed to be practical for AI-assisted editing while remaining readable and editable by humans.

That makes semantic locality important: information that belongs to one semantic declaration should normally be discoverable close to that declaration rather than requiring a reader to correlate distant YAML blocks.

## Authority boundaries

Keep these responsibilities distinct:

```text
consumer project
  = concrete domain/software evidence

.vsir
  = semantic source for the represented artifact

.vsir.cs
  = human-editable executable witness constrained by VSIR

VSlices.Vsir
  = semantic model, parsing and conservative validation

Ruleset
  = revisable target-lowering knowledge

Tooling lowering mechanisms
  = constrained execution of authorized lowering knowledge

target-native tooling
  = target facts already owned by the target ecosystem
```

Do not move knowledge between these boundaries merely because one layer is easier to change.

The core lowering rule remains:

> Lowering may complete implementation detail. Lowering must not complete missing semantics.

A missing deterministic rule is not permission to invent semantics or introduce interpretation.

## Current experimental posture

VSIR is still being discovered through real consumer artifacts.

The implementation may temporarily lag behind a normalization experiment while the consumer corpus is being made coherent. Conversely, old parser support is not proof that an older syntax remains the preferred semantic form.

When those differ:

1. establish the consumer semantics first;
2. preserve the experimental distinction explicitly;
3. collect repeated evidence across the corpus;
4. then update the model/parser/validator with the smallest coherent mechanism;
5. rerun lowering and observe the next boundary.

Do not add compatibility aliases solely to make an in-progress normalization parse unless preserving that historical syntax is an explicit requirement.

## Domain Type shape

Current Domain Type artifacts commonly identify:

```yaml
vsir: 0.1
kind: domain-type
name: Location
classification: value-object
shape: product
traits: [transform]
```

The current implementation also supports sum-shaped Domain Types through `variants` in consumer artifacts.

Classification, shape and traits are semantic declarations, not target-language inheritance instructions.

Physical directory placement or a C# base class does not decide semantic classification.

## Structural types

VSIR types should preserve semantic structure rather than leak a target realization into the document.

Prefer:

```yaml
Extensions:
  sequence: StreetExtension
```

over target-specific forms such as:

```yaml
Extensions: Seq<StreetExtension>
```

when the semantic fact is only that the field is a sequence.

The current implementation models this distinction through named and unary structural types. Additional constructors should be discovered from real semantic need rather than added as target-library aliases.

## Semantic locality

A recurring normalization pattern is:

```text
field
  -> exposed output shape
  -> optional semantic source
```

A declaration may remain shorthand when its source is deterministic and unambiguous:

```yaml
state:
  Street: StreetName

representation:
  Value: string
```

When the value must be derived explicitly, expand the same field locally:

```yaml
Field:
  output: Type
  from:
    ...
```

This pattern is currently evidenced for `state` and `representation`.

Do not generalize it automatically to every field-like structure in VSIR. `construction.input`, identity, Feature outputs, capabilities and future structures may have different semantics.

## State

`state` describes observable semantic state of a Domain Type.

Directly established state can use shorthand:

```yaml
state:
  Commune: Commune
  Street: StreetName
```

Derived observable state stays under `state` and declares its source locally:

```yaml
state:
  Province:
    output: Province
    from:
      value: state.Commune.InProvince

  Region:
    output: Region
    from:
      value: state.Commune.InProvince.InRegion
```

The normalization direction removes a separate top-level `derived` taxonomy when the declared values are still observable state of the same Domain Type.

### Construction consequence

A state field without `from` is expected to be established by construction when construction applies.

A state field with `from` already has a semantic source and should not also be independently established by construction.

This gives validation a useful invariant:

```text
direct state
  -> construction establishes it

derived state
  -> derivation establishes it
```

Two competing sources for the same state field should fail rather than be reconciled implicitly.

## Representation

`representation` describes how an already-valid Domain Type is semantically observed through its declared representation.

When same-name projection is deterministic, shorthand is preferred:

```yaml
state:
  Value: string

representation:
  Value: string
```

When projection is not implicit, keep the projection beside the representation field:

```yaml
representation:
  CommuneId:
    output: string
    from:
      select:
        source:
          represent: state.Commune
        field: Id
```

or:

```yaml
representation:
  Value:
    output: string
    from:
      intrinsic: concat-space
      values:
        - state.Name
        - state.Value
```

The normalization direction therefore absorbs historical top-level forms such as:

```yaml
representationMapping:
```

or:

```yaml
representation-mapping:
```

into the field that owns the projection.

The semantics of projection are not removed. Only the distant synchronization boundary is removed.

This improves discoverability and eliminates invariants such as separately checking that every mapping key corresponds to a representation field.

## Representation is pure

Representation operates over already-valid semantic state.

It may inspect state and nested Domain representations, but it must not:

- validate the Domain Type;
- mutate semantic state;
- persist data;
- observe application/runtime state;
- or encode an Application transition.

Projection expressions such as `value`, `represent`, `select`, `map`, `stringify` and admitted intrinsics are semantic operations. Their C#, LINQ, LanguageExt or other target syntax is a lowering concern.

## Construction

`construction` describes how valid semantic state is established from an input contract.

Current consumer evidence includes operations such as:

- `ensure` — prove admissibility;
- `resolve` — obtain an authoritative semantic value from another source;
- `apply` — establish a value through another semantic construction contract;
- `refine` — introduce a more precise value or establish final state.

The exact executable subset must always be read from the current parser, model and tests.

Do not create a new operation merely because a target implementation uses a different helper method.

Before adding an operation, ask whether the distinction is already carried by:

- input shape;
- output type;
- container shape;
- referenced semantic contract;
- or an existing generic operation.

For example, cardinality carried by structure should not automatically produce parallel verbs such as `apply` and `apply-seq`.

## Tooling and Ruleset

Preserve the working separation:

> Tooling owns the rule language. Rulesets own the rule vocabulary.

Tooling may constrain which expression, validation and execution mechanisms are safe.

It should not require a code change merely because a Ruleset introduces a new semantic name that already fits those mechanisms.

Built-ins bootstrap the rule language. They do not define its semantic ceiling.

Renderer or template existence never grants semantic authority by itself.

## Fail closed

VSIR validation is conservative.

Unknown keys or malformed structure in known semantic mappings must not silently disappear.

Variable-key semantic maps such as `state`, `representation` and `construction.input` are different: their user-defined field names are data, not a fixed keyword set.

Stop rather than guess when:

- semantics are missing;
- semantic ownership is unclear;
- a referenced type or contract cannot be resolved;
- multiple realizations exist and current semantics cannot distinguish them;
- required lowering knowledge is absent;
- a Ruleset requires an unsupported mechanism;
- target facts cannot be resolved authoritatively;
- or materialization would require inventing behavior.

## `.vsir` and `.vsir.cs`

Where both exist:

```text
Name.vsir
Name.vsir.cs
```

`.vsir` is the semantic source.

`.vsir.cs` is human-editable executable source under that semantic contract. It is not disposable generated output.

Compatible human choices may remain valid, which is why deterministic lineage and conservative rebase exist elsewhere in Tooling.

Compilation alone does not prove semantic conformance.

## AI editing procedure

When an AI modifies or analyzes VSIR:

1. Read the current branch/HEAD and repository instructions.
2. Read this README, then the current model/parser/validator/tests relevant to the case.
3. If work originates in a consumer, read the real `.vsir`, `.vsir.cs`, surrounding source/tests, local Ruleset/extensions and target context.
4. Establish semantics before normalizing syntax.
5. Prefer semantic locality when two structures describe one declaration.
6. Preserve shorthand where the source is deterministic and unambiguous.
7. Expand to `output` + `from` when provenance must be explicit.
8. Do not encode target-language expressions as semantic source.
9. Do not treat current implementation limitations as the conceptual ceiling.
10. Do not extend the global language from one isolated example when a narrower explanation is sufficient.
11. Leave uncertainty explicit when evidence is insufficient.
12. After a semantic mechanism changes, update tests and the nearest authoritative orientation in the same change.

## Evidence-driven evolution

Prefer this loop:

```text
real consumer artifact
  -> first unsupported semantic boundary
  -> identify the owner of that boundary
  -> smallest coherent semantic/mechanism change
  -> rerun
  -> observe the next boundary
```

The current semantic-locality experiment follows a slightly earlier phase:

```text
normalize a real corpus
  -> compare repeated structures
  -> distinguish semantic structure from historical syntax
  -> update VSlices.Vsir only after the pattern survives the corpus
  -> lower the normalized corpus
  -> continue development from the next real failure
```

This repository should eventually make scattered historical VSIR notes unnecessary. Prefer evolving this README, implementation and tests over accumulating new one-off semantic Markdown documents.

## Related orientation

For repository-wide ownership, command orchestration, Rulesets, project extensions, lineage and target adapters, read:

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../../docs/ai-development-orientation.md`](../../docs/ai-development-orientation.md)
- [`../../README.md`](../../README.md)

When those documents disagree with current code or tests, establish which artifact owns the changed behavior and make the discrepancy explicit rather than choosing silently.