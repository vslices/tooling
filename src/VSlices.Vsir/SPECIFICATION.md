# VSIR file specification

Status: experimental, normative for the semantic-locality experiment.

This document specifies the intended structure and semantics of `.vsir` files. It is written so a human or AI agent can reconstruct the meaning of a VSIR artifact without requiring chat history, historical migration notes, or a particular target-language implementation.

The implementation in `VSlices.Vsir` may temporarily lag behind this specification while the experiment is being consolidated. When that happens, the discrepancy must remain explicit. Do not rewrite a semantically preferred artifact merely to satisfy an older parser unless compatibility is an explicit requirement.

## 1. Purpose

VSIR is a structured semantic representation of a software artifact.

A `.vsir` file is intended to preserve enough knowledge to answer, at minimum:

- what concept is represented;
- what semantic category it belongs to;
- what state it exposes;
- how that state is established or derived;
- how a valid value is represented externally;
- what additional semantic capabilities it requires;
- what equality, identity, catalog, or variant semantics apply;
- and which decisions remain target-specific lowering concerns.

A concrete implementation is a witness of the contract:

```text
ConcreteImplementation |= VSIR
```

The VSIR document is not required to resemble the shape of any particular implementation language.

## 2. Design rules

### 2.1 Semantic locality

Knowledge belonging to one declaration should normally be written beside that declaration.

Prefer:

```yaml
representation:
  Value:
    type: string
    mapping:
      stringify: state.Value
```

over a separate mapping block that must be correlated by field name.

Likewise, derived state remains inside `state`:

```yaml
state:
  Province:
    type: Province
    from:
      value: state.Commune.InProvince
```

### 2.2 Semantic structure before target syntax

Use semantic type structure:

```yaml
Address:
  optional: Location

Extensions:
  sequence: StreetExtension
```

Do not encode target-library syntax when the target library is not part of the semantic contract:

```yaml
Address: Option<Location>      # avoid
Extensions: Seq<StreetExtension> # avoid
```

### 2.3 No redundant knowledge

A fact that is implied by a stronger declaration should not be repeated.

In particular, `classification` implies base traits. `traits` exists only for additional semantic extensions.

### 2.4 Fail closed

Unknown or contradictory semantics must not silently disappear or be guessed.

A consumer, parser, validator, lowerer, or AI agent must stop when reconstructing a valid meaning would require inventing missing authority.

## 3. Minimum document identity

Every `.vsir` file must be self-identifying before any artifact-specific semantics are considered.

The minimum valid document header is:

```yaml
vsir: 0.1
kind: domain-type
name: StreetName
```

These three declarations answer different questions:

```text
vsir
  -> under which VSIR language rules must this document be interpreted?

kind
  -> what family of semantic artifact does this document describe?

name
  -> which concrete semantic artifact is being described?
```

A `.vsir` document must therefore be able to answer **how it must be interpreted, what category of artifact it represents, and which artifact it is** without depending on its file name, directory, associated source file, or chat history.

Each of these declarations is required and must appear exactly once.

### 3.1 `vsir`

```yaml
vsir: 0.1
```

`vsir` identifies the version of the VSIR specification used to interpret the document.

It is the version of the semantic language itself. It is not the version of the represented domain, project, product, application, target platform, source language, or generated materialization.

Conceptually:

```text
vsir
  -> selects the grammar and semantics used to read the document
```

A reader must not silently interpret a document according to the latest known version when `vsir` is absent. The version is part of the document's reconstructible meaning.

If a future VSIR version changes the meaning or structure of a declaration, a document that still declares an earlier version remains interpreted according to that earlier specification unless an explicit migration occurs.

### 3.2 `kind`

```yaml
kind: domain-type
```

`kind` identifies the semantic artifact family described by the document.

It determines which additional declarations are meaningful and valid for that artifact family.

Conceptually:

```text
kind
  -> selects the family of semantic rules that applies after the minimum header
```

For example, `kind: domain-type` makes declarations such as `classification`, `state`, `representation`, `construction`, `identity`, `variants`, and `equality` meaningful when allowed by the Domain Type specification.

A future kind must define its own valid structure rather than inheriting Domain Type semantics accidentally. Possible future kinds must be introduced from actual evidence rather than assumed in advance.

`kind` is distinct from `classification`:

```text
kind
  -> what kind of VSIR artifact is this?

classification
  -> what semantic class does that artifact belong to within its kind?
```

Therefore this is not redundant:

```yaml
kind: domain-type
classification: identifier
```

The first declaration selects the Domain Type artifact family. The second classifies that Domain Type as an identifier.

### 3.3 `name`

```yaml
name: StreetName
```

`name` is the primary semantic identifier of the represented artifact inside the context in which the VSIR document is interpreted.

It names the concept, not a target-language declaration.

The semantic name may coincide with a realization:

```text
StreetName.vsir
StreetName.cs
class StreetName
```

but that coincidence is not what defines `name`.

Another target may realize the same semantic artifact using a different target-native identifier while the VSIR name remains unchanged.

The file name is therefore a discovery convention, not a substitute for `name`. A document named `StreetName.vsir` without an explicit `name: StreetName` is incomplete because it cannot identify itself independently of its storage path.

### 3.4 Minimum-header invariant

A conforming `.vsir` document must satisfy:

```text
exactly one vsir
exactly one kind
exactly one name
```

and those declarations must be sufficient to establish:

```text
language version
artifact family
artifact identity
```

before any kind-specific interpretation begins.

## 4. Domain Type document skeleton

For `kind: domain-type`, a document may continue with the following conceptual shape:

```yaml
vsir: 0.1
kind: domain-type
name: Example
classification: value-object
shape: product
traits: [some-extension]

state:
  ...

representation:
  ...

construction:
  ...

# optional semantic sections
identity:
  ...

variants:
  ...

values:
  ...

equality:
  ...
```

Not every section applies to every classification or shape.

### `classification`

Base semantic classification of the concept.

Current experimentally evidenced Domain Type classifications are:

```text
value-object
identifier
maintained
aggregate-root
```

Classification is stronger than a trait declaration: it identifies the base semantic nature of the Domain Type.

### `shape`

Structural form of the Domain Type.

Current evidenced values:

```text
product
sum
```

A product has one state shape. A sum has named variants that refine the common contract.

## 5. Classification and inferred traits

Traits are inferred from classification and then closed transitively through trait implications.

Current semantic implications are:

```text
classification maintained
  -> trait maintained

classification identifier
  -> trait identifier

classification aggregate-root
  -> trait aggregate-root

trait aggregate-root
  -> trait entity

classification value-object
  -> no base trait
```

Therefore:

```yaml
classification: identifier
traits: [refined, transform]
```

has the effective traits:

```text
identifier
refined
transform
```

And:

```yaml
classification: aggregate-root
```

has the effective traits:

```text
aggregate-root
entity
```

### Explicit `traits`

`traits` is reserved for additional semantic extensions not already implied by the classification or another declared trait.

Valid example:

```yaml
classification: value-object
traits: [transform]
```

Redundant example:

```yaml
classification: identifier
traits: [identifier]
```

The redundant form should be rejected rather than silently accepted.

The effective trait set is conceptually:

```text
effectiveTraits =
  closure(
    traitsImpliedBy(classification)
    union explicitTraits
  )
```

## 6. VSIR types

A type can be named or structurally constructed.

### Named type

```yaml
Value: string
Street: StreetName
Commune: Commune
```

### Unary structural type

```yaml
Address:
  optional: Location

Extensions:
  sequence: StreetExtension
```

A structural constructor names semantic structure, not a target implementation type.

Current evidenced constructors include:

```text
optional
sequence
```

New constructors should be introduced only from semantic need.

## 7. Field declarations

A field has a type and may carry context-specific semantic elaboration.

### Shorthand

When only the type must be stated:

```yaml
Value: string
```

or:

```yaml
Extensions:
  sequence: StreetExtension
```

### Expanded declaration

When additional semantics belong to the field, use `type`:

```yaml
Region:
  type: Region
  from:
    value: state.Commune.InProvince.InRegion
```

The presence of `type` distinguishes an expanded field declaration from a structural type mapping such as `optional` or `sequence`.

Different contexts may admit different semantic properties beside `type`. Do not generalize one context's property names to every field-like structure.

## 8. State

`state` declares observable semantic state.

### Direct state

```yaml
state:
  Commune: Commune
  Street: StreetName
```

Direct state has no independent derivation expression.

### Derived state

```yaml
state:
  Province:
    type: Province
    from:
      value: state.Commune.InProvince
```

Derived state is still state. It is not placed under a separate top-level `derived` taxonomy.

### State source invariant

A state field must have one semantic source.

Conceptually:

```text
direct state
  -> established by construction or another classification-specific mechanism

derived state
  -> established by from
```

A field must not be both independently constructed and derived.

## 9. Representation

`representation` declares how an already-valid Domain Type is observed through its representation contract.

Representation is pure with respect to Domain semantics: it does not validate, mutate, persist, query runtime state, or perform an Application transition.

### Implicit same-name representation

When the projection is deterministic and same-name:

```yaml
state:
  Value: string

representation:
  Value: string
```

No explicit mapping is required.

### Explicit mapping

When a projection is not implicit:

```yaml
representation:
  Value:
    type: string
    mapping:
      stringify: state.Value
```

Another example:

```yaml
representation:
  CommuneId:
    type: string
    mapping:
      select:
        source:
          represent: state.Commune
        field: Id
```

A sequence projection can remain local to the representation field:

```yaml
representation:
  Ext:
    type:
      sequence: string
    mapping:
      map:
        source: state.Extensions
        bind: extension
        value:
          select:
            source:
              represent: extension
            field: Value
```

Historical top-level `representationMapping` / `representation-mapping` is not part of the preferred normalized form.

## 10. Expression vocabulary

Expressions describe semantic relationships. They are not target-language source strings.

Current corpus evidence includes expression forms such as:

```yaml
value: state.Name
```

```yaml
stringify: state.Value
```

```yaml
represent: state.Commune
```

```yaml
select:
  source:
    represent: state.Commune
  field: Id
```

```yaml
map:
  source: state.Extensions
  bind: extension
  value:
    represent: extension
```

And admitted intrinsics where the semantic operation is itself named explicitly:

```yaml
intrinsic: concat-space
values:
  - state.Name
  - state.Value
```

The expression vocabulary must remain constrained and validated. A string that happens to be valid C#, JavaScript, SQL, or another target language is not automatically a VSIR expression.

## 11. Identity

A Domain Type with semantic identity may declare it locally:

```yaml
identity:
  type: SrvIdentityId
  from: state.Document
```

`identity.type` declares the semantic identifier type.

`identity.from` declares where the identity is obtained from the Domain Type's semantic state.

Identity semantics are distinct from representation and should not be encoded merely as a representation field.

## 12. Sum types and variants

A sum-shaped Domain Type uses named variants:

```yaml
shape: sum

variants:
  NaturalIdentity:
    traits: [transform]
    state:
      Name: FullName
    ...

  LegalIdentity:
    traits: [transform]
    state:
      Name: CompanyName
    ...
```

A variant may refine common state, construction, representation, and additional traits.

Common root declarations remain common and should not be duplicated into every variant merely for locality.

Variant traits follow the same rule as root traits: they describe additional semantics, not facts already implied by the parent classification.

## 13. Maintained Domain Types

A maintained Domain Type is a discrete semantic space whose allowed values are known by the Domain model.

```yaml
kind: domain-type
name: IdentityType
classification: maintained
shape: product

state:
  Name: string

representation:
  Value:
    type: string
    mapping:
      value: state.Name

values:
  Natural:
    state:
      Name: Natural
  Juridical:
    state:
      Name: Juridica
```

`values` names the maintained members and the state associated with each member.

The member name and its semantic state are distinct facts. They must not be collapsed merely because they are often equal.

A maintained Domain Type does not require public construction semantics when its universe is intentionally closed to the maintained values.

## 14. Construction

`construction` describes how valid direct semantic state is established from an input contract.

### Product input

```yaml
construction:
  input:
    Value: string
```

### Scalar input

```yaml
construction:
  input: Rut
```

### Steps

Current corpus evidence includes:

#### `ensure`

Proves admissibility:

```yaml
- ensure:
    condition:
      intrinsic: non-empty
      value: input.Value
    failure:
      message: Debes especificar un valor
```

#### `resolve`

Obtains an authoritative semantic value from an admitted in-Domain source:

```yaml
- resolve:
    source: Commune
    id: input.CommuneId
    as: commune
    failure:
      message: Debes especificar una comuna existente
```

`resolve` must not hide database, network, current-user, or other runtime/Application observations.

#### `apply`

Establishes a value through another semantic construction contract:

```yaml
- apply:
    over: StreetName
    input:
      Value: input.Street
    as: street
```

Mapped cardinality remains structural:

```yaml
- apply:
    over: StreetExtension
    input:
      source: input.Ext
      map:
        Value: item
    as: extensions
```

#### `refine`

Introduces a more precise value or establishes final state from already-valid intermediate values:

```yaml
- refine:
    state:
      Commune: commune
      Street: street
      Extensions: extensions
```

A construction step must not be invented merely because a target implementation happens to use a different helper function.

## 15. Refined concepts

When a concept semantically refines another type, the relation may be declared explicitly:

```yaml
classification: identifier
traits: [refined, transform]
refined-from: Rut
```

`refined-from` identifies the semantic source type being refined.

The `refined` trait remains explicit because it is additional semantics, not implied by `classification: identifier`.

## 16. Equality

Equality may be declared when it is part of the semantic contract.

Intrinsic equality:

```yaml
equality:
  intrinsic: ordinal-equals
  by: state.Name
```

Equality delegated to another semantic type:

```yaml
equality:
  over: Rut
  by: state.Value
```

Exactly one equality strategy should own the comparison semantics for a declaration.

## 17. Reconstruction procedure

A reader should reconstruct a `.vsir` document in this order:

```text
1. version + kind
2. name
3. classification
4. inferred base traits
5. explicit traits
6. transitive trait closure
7. shape
8. common state
9. identity / maintained-value / variant semantics as applicable
10. construction and derivation sources
11. representation projections
12. equality
13. unresolved references or unsupported semantics
```

For each state or representation field:

```text
read field name
  -> determine shorthand vs expanded declaration
  -> reconstruct semantic type
  -> reconstruct local source/mapping when present
  -> verify exactly one source of meaning
```

A correct reconstruction must preserve distinctions even when a target language could collapse them.

Examples:

```text
Juridical member name != "Juridica" state value
aggregate-root classification != explicit aggregate-root trait
optional semantic type != Option<T> target realization
representation mapping != arbitrary target expression
```

## 18. Validation invariants

At minimum, a conforming validator should enforce the following semantic invariants as support is implemented:

- `vsir`, `kind`, and `name` are required exactly once;
- interpretation of kind-specific semantics begins only after the minimum header is valid;
- unknown fixed semantic keys fail closed;
- user-defined field names remain allowed only inside variable-key maps;
- explicit traits must not repeat inferred traits;
- duplicate explicit traits are invalid;
- trait implications are transitively closed;
- expanded fields require a `type`;
- direct and derived state cannot provide competing sources for one field;
- representation mappings belong to declared representation fields;
- implicit representation is permitted only when deterministic under the language rules;
- representation remains pure;
- a maintained value must satisfy the maintained Domain Type's state shape;
- an aggregate root must have coherent identity semantics;
- equality must have one authoritative strategy;
- unresolved referenced semantics must not be guessed.

## 19. Lowering boundary

VSIR specifies semantics. It does not own target syntax.

Keep the authority chain explicit:

```text
consumer evidence
  -> .vsir semantic source
  -> constrained Tooling mechanisms
  -> Ruleset target-lowering knowledge
  -> target-native facts
  -> .vsir.cs human-editable materialization
  -> compile/test/runtime evidence
```

Tooling owns the rule language. Rulesets own the rule vocabulary.

Lowering may complete implementation detail. Lowering must not complete missing semantics.

## 20. Human and AI editing guidance

A `.vsir` file should optimize for semantic discoverability rather than YAML cleverness.

When editing:

- preserve the minimum self-identifying header (`vsir`, `kind`, `name`);
- prefer shorthand when only a type must be stated;
- expand a field only when additional semantics are required;
- keep derivation under the state field it derives;
- keep representation mapping under the representation field it maps;
- use structural semantic types rather than target-library syntax;
- omit traits that are inferable from classification;
- retain traits that add semantics beyond classification;
- preserve uncertainty instead of inventing a missing contract;
- and do not add global vocabulary from one isolated implementation detail.

## 21. Evolution rule

This specification evolves from repeated evidence in real artifacts.

Use the following loop:

```text
real artifact
  -> reconstruct semantics
  -> normalize repeated structure
  -> identify irreducible semantic distinctions
  -> update specification
  -> update model/parser/validator/tests
  -> lower real artifacts
  -> observe next unsupported boundary
```

The specification should become sufficient for future humans and AI agents to reconstruct VSIR without relying on historical one-off `.md` documents. Historical notes may explain why a decision happened, but they must not be required to discover what a valid `.vsir` file means now.