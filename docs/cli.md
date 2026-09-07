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

For the current `domain-type` authoring contract, the accepted classifications are:

```text
value-object
entity
identifier
maintained
aggregate-root
```

Tags are different: they are organizational and associative metadata, so `--tags` does not require a kind or classification and does not imply either one.

No other VSIR semantic declaration is currently implemented through `new vsir`. In particular, `shape`, `traits`, state, representation, values, input and construction remain outside the current `new vsir` surface until their contracts are introduced deliberately.

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
values: <currently supported values, when the contract is enumerated>
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
  meaning: Declares the observable semantic properties that constitute a valid instance of the Domain Type. Child properties support add, remove and set; derived state may declare a direct state source through state.<property>.from.
  value kind: map<property, declaration>
  operations: add, remove, set

representation
  status: required
  meaning: Declares the observable form through which a valid Domain Type can be represented. Child properties support add, remove and set; a direct state source may be declared through representation.<property>.from when no semantic mapping is required.
  value kind: map<property, declaration>
  operations: add, remove, set

traits
  status: optional
  meaning: Declares additional semantic capabilities that are not already implied by the Domain Type classification.
  value kind: set<string>
  operations: add, remove, set
  values: transform
```

A maintained Domain Type activates one additional required surface:

```yaml
vsir: 0.1
kind: domain-type
name: IdentityType
classification: maintained
```

```text
state
  status: required
  value kind: map<property, declaration>
  operations: add, remove, set

representation
  status: required
  value kind: map<property, declaration>
  operations: add, remove, set

values
  status: required
  meaning: Declares the maintained members and the semantic state associated with each member.
  value kind: map<member, state>
  operations: add, remove, set

traits
  status: optional
  values: transform
```

`classification: maintained` semantically implies the `maintained` trait, so `maintained` is not offered as an explicit value under `traits`. Its effective contract is reflected instead by the required `values` frontier.

The `values` line on `traits` is authoritative for the explicit trait vocabulary currently supported by Tooling. At this stage the only explicitly authorable trait is:

```text
transform
  -> declares that the Domain Type can be established from an explicit input contract
  -> requires input
  -> requires construction
```

Classification-implied traits such as `maintained`, `identifier`, `entity`, or `aggregate-root` are not offered as explicit choices merely because they exist in the effective trait model. Additional explicit traits must be introduced deliberately as their contracts are specified.

Once `transform` is present, discovery follows the trait contract and exposes any missing obligations:

```text
input
  status: required
  meaning: Declares what enters the transform before Domain Type validity has been established.
  value kind: map<property, declaration>
  operations: not implemented

construction
  status: required
  meaning: Declares the semantic conditions and steps that establish a valid Domain Type from the transform input.
  value kind: sequence<step>
  operations: not implemented
```

These entries are obligations derived from the effective trait, not optional authoring suggestions. They disappear from the immediate frontier once the corresponding sections exist. Their mutation syntax remains intentionally unavailable until the input and construction authoring contracts are specified; discovery exposes the obligation without inventing an editing grammar.

`state`, `representation`, and `values` where applicable remain visible after their first content is established because discovery describes both obligations and currently available authoring surfaces. `required` describes the contract of the section; it does not mean the section is necessarily missing.

The current structured state/representation authoring subset operates on child properties rather than replacing a whole map:

```text
vslices update vsir StreetName --add state.Value=string
vslices update vsir StreetName --add representation.Value=string
vslices update vsir StreetName --set state.Value=Rut
vslices update vsir StreetName --remove state.Value
```

For direct property declarations, `add` requires that the property is absent, `set` requires that it already exists, and `remove` requires that it already exists. Attempting to remove the final property of a required `state` or `representation` map fails closed.

Maintained `values` uses the same add/remove/set distinction at the member boundary. A member declaration is supplied as a constrained inline YAML mapping whose only current top-level member field is `state`:

```text
vslices update vsir IdentityType \
  --add "values.Natural={state: {Name: Natural}}"

vslices update vsir IdentityType \
  --set "values.Juridical={state: {Name: Juridica}}"

vslices update vsir IdentityType --remove values.Natural
```

This materializes as:

```yaml
values:
  Natural:
    state:
      Name: Natural
  Juridical:
    state:
      Name: Juridica
```

The member name and member state remain distinct. Tooling does not infer `Name: Natural` merely from the member name `Natural`. A maintained member declaration therefore requires an explicit non-empty `state` mapping. Removing the final maintained member fails closed because `values` is required by the effective `maintained` trait.

A derived state property exposes its provenance through `from`:

```text
vslices update vsir Location --set state.Region.from=state.Commune.InProvince.InRegion
```

If `state.Region` was declared in scalar shorthand, Tooling preserves its type while expanding the declaration:

```yaml
state:
  Region:
    type: Region
    from: state.Commune.InProvince.InRegion
```

`state.<property>.from` accepts direct `state.*` references. Removing the relation collapses the declaration back to scalar shorthand when only `type` remains.

A representation property can now declare the direct state value that supplies it:

```text
vslices update vsir IdentityType --set representation.Value.from=state.Name
```

which expands a scalar declaration as needed:

```yaml
representation:
  Value:
    type: string
    from: state.Name
```

`representation.<property>.from` is for direct reuse of a state value only. It accepts `state.*` references and is mutually exclusive with a representation `mapping`. Semantic transformations such as stringify/select/map continue to belong to `mapping`; deeper mapping authoring is not implemented yet.

`traits` is currently writable after a Domain Type classification is known:

```text
vslices update vsir StreetName --add traits=transform
```

Unknown explicit trait values fail closed rather than being accepted as opaque strings.

Discovery may project supported mutations without mutating the artifact:

```text
vslices discovery vsir StreetName --set kind=domain-type
vslices discovery vsir StreetName --add tags=addressing
vslices discovery vsir StreetName --add traits=transform
vslices discovery vsir StreetName --add state.Value=string
vslices discovery vsir IdentityType --add "values.Natural={state: {Name: Natural}}"
vslices discovery vsir IdentityType --set representation.Value.from=state.Name
```

Projecting `--add traits=transform` therefore also projects the newly activated `input` and `construction` obligations in the returned frontier.

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
  -> current explicit values: transform

state.<property>
  -> add, remove, set

representation.<property>
  -> add, remove, set

values.<member>
  -> add, remove, set
  -> only when classification: maintained
  -> add/set value: {state: {<property>: <value>, ...}}

state.<property>.from
  -> add, remove, set
  -> value must be a state.* reference

representation.<property>.from
  -> add, remove, set
  -> value must be a state.* reference
  -> mutually exclusive with representation mapping
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

```text
vslices update vsir StreetName \
  --add "state.Value=string;representation.Value=string"
```

```text
vslices update vsir IdentityType \
  --add "values.Natural={state: {Name: Natural}}"
```

```text
vslices update vsir IdentityType \
  --set representation.Value.from=state.Name
```

For map-property or maintained-member removal the value is unnecessary, so the concise form is valid:

```text
vslices update vsir Location --remove state.Number
vslices update vsir Location --remove state.Region.from
vslices update vsir IdentityType --remove representation.Value.from
vslices update vsir IdentityType --remove values.Natural
```

Set-valued removal still names the values being removed:

```text
vslices update vsir StreetName --remove tags=street
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

Unsupported semantic paths, operations, or enumerated semantic values fail closed. Tooling must not become a generic YAML editor merely because a path can be addressed syntactically.

## 8. Current authoring frontier

The implemented semantic sequence now reaches maintained and trait-derived obligations:

```text
name
  -> kind
  -> classification
       -> value-object/entity/aggregate-root
            -> state / representation
       -> maintained
            -> state / representation
            -> values
  -> optional explicit traits
       -> transform
            -> required input
            -> required construction
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
  -> after value-object/entity/aggregate-root, exposes state and representation as required writable maps
  -> after maintained, exposes state, representation and values as required writable maps
  -> explains the direct state-source relation available under both state and representation
  -> after classification, exposes traits as optional and transform as the currently available explicit value
  -> when transform is effective, exposes missing input and construction as required obligations

search
  -> may filter current VSIR artifacts by an implemented root property filter

update vsir
  -> can add/remove/set tags and traits
  -> validates explicit traits against the currently supported vocabulary
  -> can set kind and classification atomically
  -> can add/remove/set direct state and representation properties
  -> can add/remove/set maintained members under values
  -> can establish/change/remove state.<property>.from
  -> can establish/change/remove representation.<property>.from
  -> rejects representation declarations that combine from and mapping
  -> does not yet author representation mapping, input or construction
```

Input/construction authoring, representation mapping authoring, deeper field declaration forms and additional explicit traits remain unavailable until their contracts are specified from evidence.

## 9. Agent-facing invariants

- creating or updating an artifact must not silently infer unsupported semantic knowledge;
- tags remain organizational metadata and must not imply semantic declarations;
- tags are non-empty, single-line and unique;
- `classification` is not writable before a compatible `kind` is established in the resulting candidate;
- `maintained` is an accepted Domain Type classification;
- `value-object`, `entity`, `maintained`, and `aggregate-root` expose `state` and `representation` as required writable maps;
- `classification: maintained` implies the `maintained` trait and exposes `values` as a required writable map;
- `maintained` is not offered as an explicit trait because it is classification-implied;
- maintained `values` supports add/remove/set at `values.<member>`;
- add/set of a maintained member requires an explicit non-empty `state` mapping;
- a maintained member name does not imply its state;
- removing the final maintained member fails closed;
- `values` is rejected for non-maintained classifications;
- discovery distinguishes required obligations from optional authoring surfaces;
- every discovered attribute carries a concise explanation of what it means;
- map-level operations apply to explicitly authorized child paths, not arbitrary YAML structure;
- adding an existing state/representation property fails rather than silently replacing it;
- setting a missing state/representation property fails rather than silently creating it;
- removing the final property of a required state/representation map fails closed;
- `state.<property>.from` declares direct semantic provenance from a `state.*` reference;
- `representation.<property>.from` declares a direct `state.*` source without transformation;
- representation `from` and `mapping` are mutually exclusive;
- removing a local `from` relation preserves the field type and collapses back to shorthand when possible;
- unsupported deeper state/representation paths remain fail-closed;
- `traits` is an optional Domain Type capability surface and supports add/remove/set after a Domain Type kind is established;
- discovery exposes the currently supported explicit trait vocabulary;
- the currently supported explicit trait vocabulary contains only `transform`;
- unknown explicit trait values fail closed;
- an effective `transform` trait requires both `input` and `construction`;
- discovery reports missing `input` and `construction` as required obligations while `transform` is effective;
- reporting those obligations does not authorize input/construction mutation before their editing contracts exist;
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
