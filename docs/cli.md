# VSlices CLI specification

Status: experimental interaction contract for the current progressive VSIR authoring and lowering experiment.

VSIR language semantics are owned by [`vslices/intermediate-representation`](https://github.com/vslices/intermediate-representation). This document describes CLI behavior. Target realization knowledge belongs to [`vslices/ruleset`](https://github.com/vslices/ruleset). Migration traversal belongs to [`vslices/planifications`](https://github.com/vslices/planifications).

## 1. Core authoring protocol

```text
vslices new vsir <artifact>
vslices discovery vsir <artifact>
vslices update vsir <artifact> ...
```

The governing authoring rule is:

> Declare only what is currently justified by evidence.

The interaction loop is:

```text
new
  -> establish progressive state

discovery
  -> expose current semantic affordances
  -> expose command templates
  -> expose structured value grammar when needed

update
  -> atomically apply advertised semantic decisions
  -> produce next state

discovery
  -> evaluate again
```

See [`semantic-authoring-affordances.md`](./semantic-authoring-affordances.md) for the complete interaction model.

## 2. `new vsir`

The minimum progressive artifact may begin with only identity known by the authoring process:

```text
vslices new vsir StreetName
```

which may create:

```yaml
vsir: 0.1
name: StreetName
```

Current convenience flags include:

```text
--kind
--shape
--classification
--tags
```

They establish facts only when supplied; they do not authorize Tooling to infer missing semantics.

## 3. `discovery vsir`

`discovery` is state-driven. It reports the immediate authorized frontier for the current artifact rather than dumping every syntactically possible VSIR property.

A discovery entry can include:

```text
path
status
meaning
value kind
allowed values
operations
command templates
value grammar
```

Example:

```text
state
  status: required
  operations: set
  command:
    vslices update vsir Location --set "state.<property>=<semantic-field-declaration>"
```

Discovery can project a candidate transition without persistence:

```text
vslices discovery vsir StreetName --set kind=domain-type
```

Projected discovery uses the same public mutation and validation path as `update`, then discards the candidate.

## 4. Public mutation semantics

`update vsir` uses three public operation names:

```text
set
add
remove
```

Their meaning is semantic rather than YAML-storage oriented.

### `set`

`set` establishes or replaces an ordinary semantic assertion.

```text
vslices update vsir Location --set "state.Street=StreetName"
vslices update vsir Location --set "state.Extensions={sequence: StreetExtension}"
```

A missing named member is established by `set`; clients do not switch to `add` because a YAML key is absent.

### `add`

`add` is reserved for genuine collection membership semantics.

Current examples:

```text
tags
traits
```

```text
vslices update vsir StreetName --add tags=addressing
vslices update vsir StreetName --add traits=transform
```

Using `add` on ordinary semantic assertions fails closed.

### `remove`

`remove` withdraws a semantic assertion or a collection member only when the current discovery contract permits it.

### Atomicity

One `update vsir` invocation is one semantic transaction:

```text
read
  -> authorize
  -> parse value
  -> construct candidate
  -> validate
  -> serialize
  -> atomic replace
```

Any failure leaves the original artifact unchanged.

## 5. Domain Type frontier

For `kind: domain-type`, the language requires the current structural/core decisions represented by the active VSIR specification, including:

```text
shape
state
representation
classification
```

Current shapes:

```text
product
sum
```

Current classifications exercised by Tooling authoring:

```text
value-object
entity
identifier
maintained
aggregate-root
```

Classification and explicit traits may activate additional obligations such as `equality`, `values`, `input`, and `construction`.

## 6. Named semantic members use `set`

Named members are assertions, not set-union operations.

Examples:

```text
vslices update vsir Location --set "state.Commune=Commune"
vslices update vsir Location --set "representation.Street=string"
vslices update vsir Location --set "input.CommuneId=CommuneId"
vslices update vsir Name --set "variants.CompanyName=<variant-declaration>"
vslices update vsir IdentityType --set "values.Natural={state: {Name: Natural}}"
```

Existing replaceable/removable members may expose `set` and/or `remove` from discovery.

## 7. Structured semantic field declarations

Tooling admits structured semantic field values without becoming a generic YAML editor.

Examples:

```text
state.Extensions={sequence: StreetExtension}
input.Ext={sequence: string}
representation.Ext={type: {sequence: string}}
```

Discovery advertises the corresponding grammar forms. `from` and `mapping` remain separate local semantic decisions and cannot be smuggled through a structured field declaration.

## 8. Local state and representation relations

Derived state uses:

```text
state.<property>.from
```

Example:

```text
vslices update vsir Location \
  --set "state.Region.from=state.Commune.InProvince.InRegion"
```

Representation may use either a direct source:

```text
representation.<property>.from
```

or a semantic mapping:

```text
representation.<property>.mapping
```

The two are mutually exclusive for the same representation field.

## 9. Grammar-driven representation mappings

`representation.<field>.mapping` accepts an expression grammar advertised by `discovery`.

Current forms exercised by the corpus include:

```text
stringify
  {stringify: <semantic-reference>}

represent
  {represent: <semantic-reference>}

select
  {select: {source: <expression>, field: <field>}}

map
  {map: {source: <expression>, bind: <name>, value: <expression>}}

intrinsic
  {intrinsic: <ruleset-intrinsic>, ...}
```

The grammar is compositional. In particular:

```text
select(represent(state.Street), Value)
```

and:

```text
select(state.Street, Value)
```

are distinct semantic trees. Tooling does not insert `represent` implicitly.

## 10. Transform authoring

The currently explicit root trait vocabulary exercised by this branch includes:

```text
transform
```

A transform activates `input` and `construction` obligations.

Root input may be scalar:

```text
vslices update vsir SrvIdentityId --set "input=Rut"
```

or product-shaped and authored progressively:

```text
vslices update vsir Location --set "input.CommuneId=CommuneId"
vslices update vsir Location --set "input.Ext={sequence: string}"
```

`construction` is ordered and currently uses whole-boundary `set` because construction steps do not yet expose stable public member identities.

Current construction forms exercised by grammar-driven discovery include:

```text
ensure
resolve
apply
refine
```

One semantic `apply` covers both direct and mapped/container input shapes. Target-specific realization such as `Apply` versus `ApplySeq` belongs to lowering/Ruleset knowledge, not VSIR command vocabulary.

## 11. Tags and traits

`tags` are organizational metadata and imply no semantic behavior.

`tags` and `traits` are current examples of genuine set-valued surfaces and may expose:

```text
add
remove
set
```

Their ordering has no semantic meaning.

## 12. Equality, maintained values and variants

Equality is established with `set` when required by the active classification contract.

Maintained members are named semantic assertions and therefore use `set` at `values.<member>`.

Sum variants are likewise named semantic assertions and use `set` at `variants.<variant>`.

Nested variant editing remains constrained by explicitly admitted contracts; addressability alone does not authorize arbitrary deep YAML mutation.

## 13. `search`

`search` locates VSIR artifacts beneath the current directory using filters such as:

```text
<property>:<operator>:<value>
```

Current operators include `contains` and `equals` for the supported root-property cases. Search is read-only and respects artifact discovery exclusions.

## 14. Lowering lifecycle

Authoring commands and lowering commands operate on the same VSIR semantic language from opposite directions:

```text
partial knowledge
  -> new / discovery / update
  -> VSIR
  -> lower
  -> target witness
```

`lower` must consume the normalized VSIR produced by authoring without requiring a legacy rewrite.

Current normalized lowering evidence from `Location` includes:

```text
structured semantic types
state.from
representation reference/stringify/represent/select/map/intrinsic
root input
ensure
resolve
apply direct
apply mapped/container
refine
```

## 15. Authoring parity

A VSIR construction is considered fully supported by Tooling when:

```text
discovery can explain how to express it
new/update can author it
validation can check it
lower can consume it
Ruleset can materialize it when target realization is required
```

`Location.vsir` is the strongest current witness for this parity.

## 16. Agent-facing invariants

- no command may silently invent unsupported semantics;
- `set` means establish-or-replace for ordinary assertions;
- `add` is reserved for genuine collection semantics;
- discovery is state-dependent and may be projected without persistence;
- discovery command templates and grammar forms are part of the authoring contract;
- grammar-driven discovery advertises valid forms but does not choose the domain decision;
- structured field authoring does not authorize arbitrary YAML;
- `from` and `mapping` remain mutually exclusive where specified;
- explicit authored construction evidence outranks same-name convenience conventions;
- update is atomic and fail-closed;
- lowering must preserve explicit semantic expression structure;
- current implementation limitations must not be mistaken for conceptual VSIR limits;
- VSIR language authority remains `vslices/intermediate-representation`.

## 17. Cross-repository map

- [`vslices/intermediate-representation`](https://github.com/vslices/intermediate-representation) — VSIR semantic language.
- [`vslices/tooling`](https://github.com/vslices/tooling) — CLI, parser, validator, discovery, mutation, lowering mechanisms.
- [`vslices/ruleset`](https://github.com/vslices/ruleset) — deterministic target realization knowledge.
- [`vslices/planifications`](https://github.com/vslices/planifications) — progressive source reconstruction process and feedback loops.