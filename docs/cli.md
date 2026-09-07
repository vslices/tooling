# VSlices CLI specification

Status: experimental. This document describes the intended command semantics of the `vslices` CLI while progressive VSIR authoring is being explored. Language-level VSIR semantics are owned by `vslices/intermediate-representation`; this document specifies CLI interaction semantics.

## 1. Purpose

The CLI supports progressive creation, discovery, refinement, location and materialization of VSlices artifacts without requiring the final structure to be known up front.

The primary authoring rule remains:

> Declare only what is currently justified by evidence.

The current VSIR authoring protocol is:

```text
vslices new vsir <artifact>
vslices discovery vsir <artifact>
vslices update vsir <artifact> ...
```

`new` establishes known facts, `discovery` exposes the immediate authorized frontier, and `update` applies an atomic semantic transition.

## 2. General command families

```text
vslices init
vslices new <subject>
vslices discovery <subject>
vslices search --filter <property>:<operator>:<value>
vslices update <subject>
```

VSIR lifecycle commands such as `lower`, `rebase` and `transpile` remain separate. Implemented update subjects also include:

```text
vslices update self
vslices update ruleset
```

## 3. Progressive `new vsir`

The minimum useful act remains naming a concept:

```text
vslices new vsir StreetName
```

which may create:

```yaml
vsir: 0.1
name: StreetName
```

The currently implemented authoring flags are:

```text
--kind
--shape
--classification
--tags
```

`--shape` and `--classification` require `--kind` because their validity is kind-specific.

For example:

```text
vslices new vsir StreetName \
  --kind domain-type \
  --shape product \
  --classification value-object \
  --tags addressing,street
```

may produce:

```yaml
vsir: 0.1
kind: domain-type
name: StreetName
tags: ['addressing', 'street']
shape: product
classification: value-object
```

Current Domain Type shapes are:

```text
product
sum
```

Current Domain Type classifications are:

```text
value-object
entity
identifier
maintained
aggregate-root
```

Tags remain organizational metadata. They do not require a kind, shape or classification and imply no semantic behavior.

## 4. Domain Type core frontier

Once `kind: domain-type` is established, discovery exposes the core Domain Type contract:

```text
shape
  status: required
  value kind: enum
  operations: set
  values: product, sum

state
  status: required
  operations: add, remove, set

representation
  status: required
  operations: add, remove, set

classification
  status: required
  value kind: enum
  operations: set
```

This reflects the current language rule:

```text
kind: domain-type
  -> requires shape
  -> requires state
  -> requires representation
  -> requires classification
```

Progressive authoring may temporarily leave one or more of these obligations unresolved. Discovery reports the obligation without inventing its value.

## 5. Shapes

### `shape: product`

A product-shaped Domain Type treats top-level `state` entries as simultaneous properties.

```yaml
shape: product
state:
  Street: StreetName
  Number: string
```

Product state authoring uses child property paths:

```text
vslices update vsir Location --add state.Street=StreetName
vslices update vsir Location --add state.Number=string
vslices update vsir Location --set state.Number=int
vslices update vsir Location --remove state.Number
```

A product state property may also declare direct semantic provenance:

```text
vslices update vsir Location \
  --set state.Region.from=state.Commune.InProvince.InRegion
```

which may expand shorthand into:

```yaml
state:
  Region:
    type: Region
    from: state.Commune.InProvince.InRegion
```

`state.<property>.from` accepts direct `state.*` references. Removing `from` preserves the field type and collapses back to shorthand when possible.

### `shape: sum`

A sum-shaped Domain Type is represented as shared state and representation plus a required set of mutually exclusive variants:

```text
shared product × (VariantA + VariantB + ...)
```

Discovery exposes:

```text
state
  status: required
  value kind: map<property, declaration>
  operations: add, remove, set
  meaning: shared state; may be empty

representation
  status: required
  value kind: map<property, declaration>
  operations: add, remove, set
  meaning: shared representation; may be empty

variants
  status: required
  value kind: map<variant, declaration>
  operations: add, remove, set
```

A sum without shared coordinates may establish the required empty maps explicitly:

```text
vslices update vsir Name --set "state={};representation={}"
```

Variants are authored at the complete variant boundary:

```text
vslices update vsir Name \
  --add "variants.CompanyName={traits: [transform], state: {Value: string}, representation: {Value: string}, input: {Value: string}, construction: [{refine: {state: {Value: input.Value}}}]}"
```

A variant may declare local:

```text
state
representation
traits
input
construction
```

and may also be empty when variant identity alone is meaningful:

```text
vslices update vsir Status --add "variants.Pending={}"
```

The final variant cannot be removed. `variants` is rejected for `shape: product`.

### Shape mutation

Shape itself supports only `set`:

```text
vslices update vsir StreetName --set shape=product
vslices update vsir ContactMethod --set shape=sum
```

Unknown shapes fail closed.

## 6. Representation

Every Domain Type exposes `representation` as a required writable map.

For product-shaped Domain Types, direct child authoring is available:

```text
vslices update vsir StreetName --add representation.Value=string
```

A representation property can declare a direct state source:

```text
vslices update vsir IdentityType \
  --set representation.Value.from=state.Name
```

which may materialize as:

```yaml
representation:
  Value:
    type: string
    from: state.Name
```

`representation.<property>.from` is direct reuse of a state value and is mutually exclusive with `mapping`.

For `shape: sum`, shared representation is combined with the active variant's local representation. The effective representation must preserve the active variant.

## 7. Classification-specific obligations

Classification adds obligations on top of the Domain Type core contract.

### `classification: identifier`

```text
identifier
  -> equality required
```

Discovery exposes:

```text
equality
  status: required
  value kind: strategy
  operations: set
```

Example:

```text
vslices update vsir SrvIdentityId \
  --set "equality={over: Rut, by: state.Value}"
```

or:

```text
vslices update vsir SrvIdentityId \
  --set "equality={intrinsic: ordinal-equals, by: state.Value}"
```

### `classification: maintained`

```text
maintained
  -> equality required
  -> implied trait maintained
       -> values required
```

`values` supports add/remove/set at the member boundary:

```text
vslices update vsir IdentityType \
  --add "values.Natural={state: {Name: Natural}}"

vslices update vsir IdentityType \
  --set "values.Juridical={state: {Name: Juridica}}"

vslices update vsir IdentityType --remove values.Natural
```

A maintained member name does not imply its state. Add/set requires an explicit non-empty `state` mapping. Removing the final maintained member fails closed.

Equality uses the same `set` authoring surface as identifiers:

```text
vslices update vsir IdentityType \
  --set "equality={intrinsic: ordinal-equals, by: state.Name}"
```

`maintained` is classification-implied and is not offered as an explicit trait value.

## 8. Traits and transform authoring

The currently explicitly authorable trait vocabulary is:

```text
transform
```

Traits support:

```text
add
remove
set
```

Example:

```text
vslices update vsir StreetName --add traits=transform
```

Unknown explicit traits fail closed.

An effective root `transform` trait requires:

```text
input
construction
```

Discovery exposes both surfaces as required and writable:

```text
input
  status: required
  value kind: map<property, declaration> | scalar semantic type
  operations: add, remove, set

construction
  status: required
  value kind: sequence<step>
  operations: set
```

Structured input may be authored progressively at property boundaries:

```text
vslices update vsir StreetName --add input.Value=string
vslices update vsir StreetName --set input.Value=Rut
vslices update vsir StreetName --remove input.Value
```

The complete input contract may also be set, including scalar input:

```text
vslices update vsir SrvIdentityId --set input=Rut
vslices update vsir StreetName --set "input={Value: string}"
```

`construction` is an ordered sequence, so its current authoring boundary uses `set` over the complete sequence rather than pretending that ordered steps already have stable semantic identities.

For `StreetName`:

```text
vslices update vsir StreetName \
  --set "construction=[{ensure: {condition: {intrinsic: non-empty, args: {value: input.Value}}, failure: {message: Debes especificar una calle}}}, {ensure: {condition: {intrinsic: length-at-most, args: {value: input.Value, max: 30}}, failure: {message: Debe tener 30 caracteres o menos (Enviados {length})}}}, {refine: {state: {Value: input.Value}}}]"
```

The currently admitted construction step names are:

```text
ensure
resolve
apply
refine
```

Unknown step names fail closed. Input and construction authoring is rejected until the root `transform` trait has been established.

## 9. Tags

Tags are organizational and associative metadata:

```text
tags
  -> add, remove, set
```

Examples:

```text
vslices update vsir StreetName --add tags=addressing,street
vslices update vsir StreetName --remove tags=street
vslices update vsir StreetName --set tags=identity,addressing
```

Tags are non-empty, single-line and unique. Their order has no semantic meaning.

## 10. `search`

`search` locates VSIR artifacts beneath the current directory using:

```text
<property>:<operator>:<value>
```

Current operators:

```text
contains
  -> sequence: exact member containment
  -> scalar: ordinal substring containment

equals
  -> scalar: ordinal equality
```

Examples:

```text
vslices search --filter tags:contains:identity-service
vslices search --filter classification:equals:value-object
vslices search --filter shape:equals:sum
vslices search --filter name:contains:Ticket
```

The current implementation filters root properties only. Unsupported operators fail closed. Search is read-only and respects artifact discovery exclusions.

## 11. Discovery projections

Discovery may project supported mutations without persisting them:

```text
vslices discovery vsir StreetName --set kind=domain-type
vslices discovery vsir StreetName --set shape=product
vslices discovery vsir ContactMethod --set shape=sum
vslices discovery vsir StreetName --add tags=addressing
vslices discovery vsir StreetName --add traits=transform
vslices discovery vsir StreetName --add state.Value=string
vslices discovery vsir StreetName --add input.Value=string
vslices discovery vsir IdentityType --add "values.Natural={state: {Name: Natural}}"
```

Projection uses the same candidate mutation and validation path as `update`, then discards the candidate.

## 12. Atomic update semantics

One `update vsir` invocation is one transaction:

```text
read current artifact
  -> apply requested mutations to an in-memory candidate
  -> validate candidate
  -> serialize candidate
  -> commit atomically
```

If any mutation, authorization, validation, serialization or persistence step fails, the original artifact remains unchanged.

Unsupported semantic paths, operations or enumerated semantic values fail closed. `update` is not a generic YAML editor.

## 13. Current authoring frontier

```text
name
  -> kind
       -> domain-type
            -> shape
                 -> product
                 -> sum
                      -> shared state
                      -> shared representation
                      -> variants
            -> state
            -> representation
            -> classification
                 -> value-object
                 -> identifier
                      -> equality
                 -> maintained
                      -> equality
                      -> implied maintained
                           -> values
                 -> entity
                 -> aggregate-root
            -> optional explicit traits
                 -> transform
                      -> input
                      -> construction
```

Tags remain orthogonal and writable throughout progressive authoring.

Current deliberate limits include:

```text
structured semantic type authoring at ordinary state/representation fields
representation mapping authoring
nested sum-variant mutation beneath variants.<variant>
additional explicit traits such as refined
refined-from authoring
identity authoring
entity-specific state.Id validation
```

## 14. Agent-facing invariants

- creating or updating an artifact must not silently infer unsupported semantic knowledge;
- `kind: domain-type` activates required `shape`, `state`, `representation`, and `classification` frontiers;
- shape is explicit and supports only `product` and `sum` today;
- product state top-level entries are simultaneous properties;
- sum state and representation are shared products and may be empty;
- sum variants live under `variants` and are mutually exclusive;
- variant-local declarations may add state, representation, traits, input and construction;
- state and representation remain required for every Domain Type regardless of classification;
- `classification: identifier` requires `equality`;
- `classification: maintained` requires `equality` and implies `maintained`, which requires `values`;
- equality supports only `set`;
- maintained values support add/remove/set at `values.<member>`;
- `state.<property>.from` is product-state semantic provenance from a direct `state.*` reference;
- `representation.<property>.from` is a direct `state.*` source and is mutually exclusive with mapping;
- the currently supported explicit root trait vocabulary contains only `transform`;
- root transform input may be a scalar semantic type or product mapping;
- structured root input supports add/remove/set at `input.<property>`;
- construction is an ordered sequence and currently supports complete replacement through `set`;
- admitted construction step names are `ensure`, `resolve`, `apply`, and `refine`;
- input and construction authoring requires root trait `transform`;
- tags remain organizational metadata and imply no semantic declarations;
- discovery projections are non-persistent;
- candidate validation occurs before persistence;
- failure leaves the original artifact unchanged;
- unsupported paths and operations fail closed;
- current implementation limitations must not be mistaken for conceptual VSIR limits;
- VSIR language semantics remain owned by `vslices/intermediate-representation`.
