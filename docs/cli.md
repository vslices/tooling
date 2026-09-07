# VSlices CLI specification

Status: experimental interaction contract for the current progressive VSIR authoring and lowering experiment.

VSIR language semantics are owned by [`vslices/intermediate-representation`](https://github.com/vslices/intermediate-representation). This document describes CLI behavior. Target realization knowledge belongs to [`vslices/ruleset`](https://github.com/vslices/ruleset). Migration traversal belongs to [`vslices/planifications`](https://github.com/vslices/planifications).

## 1. Core authoring protocol

```text
vslices new vsir <artifact>
vslices discovery vsir <artifact>
vslices update vsir <artifact> ...
```

The governing semantic authoring rule is:

> Declare only what is currently justified by evidence.

Searchable metadata is orthogonal to that semantic rule. `tags` may be authored at any point because tags do not assert domain meaning.

The interaction loop is:

```text
new
  -> establish progressive state

discovery
  -> expose always-available tags metadata operations
  -> expose current semantic affordances
  -> expose command templates
  -> expose structured value grammar when needed

update
  -> atomically apply advertised metadata and/or semantic decisions
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
```

They establish semantic facts only when supplied; they do not authorize Tooling to infer missing semantics.

Search metadata does not need to be known at creation time because `tags` is always exposed immediately by `discovery` and writable by `update`.

## 3. `discovery vsir`

`discovery` reports two things together:

1. always-available artifact metadata operations;
2. the state-dependent semantic frontier for the current artifact.

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

`tags` is always present:

```text
tags
  status: optional
  value kind: set<string>
  operations: set, add, remove
```

A semantic example:

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
vslices discovery vsir StreetName --add tags=ticket
```

Projected discovery uses the same public authoring paths as `update`, then discards the candidate.

## 4. Public mutation semantics

`update vsir` uses three public operation names:

```text
set
add
remove
```

Their meaning is authoring intent rather than YAML-storage mechanics.

### `set`

`set` establishes or replaces an assertion.

Semantic examples:

```text
vslices update vsir Location --set "state.Street=StreetName"
vslices update vsir Location --set "state.Extensions={sequence: StreetExtension}"
```

Metadata example:

```text
vslices update vsir StreetName --set "tags=ticket,serviu"
```

For `tags`, `set` replaces the complete metadata set.

### `add`

`add` is reserved for genuine collection membership.

Current public collection-valued surfaces:

```text
tags    searchable metadata
traits  semantic capabilities
```

Examples:

```text
vslices update vsir StreetName --add tags=ticket
vslices update vsir StreetName --add traits=transform
```

Using `add` on ordinary semantic assertions fails closed.

### `remove`

`remove` withdraws an assertion or collection member only when the current contract permits it.

For set-valued surfaces it targets a member:

```text
vslices update vsir StreetName --remove "tags=ticket"
vslices update vsir StreetName --remove "traits=transform"
```

### Atomicity

One `update vsir` invocation is one transaction:

```text
read
  -> authorize metadata and semantic transitions
  -> parse values
  -> construct candidate
  -> validate
  -> serialize
  -> atomic replace
```

Any failure leaves the original artifact unchanged.

## 5. Tags versus traits

`tags` and `traits` are both set-valued, but they have different authority.

```text
tags
  -> free-form operational metadata
  -> indexing, grouping and search
  -> never activates semantic obligations
  -> never guides lowering

traits
  -> semantic capability declarations
  -> constrained vocabulary
  -> may activate validation/authoring obligations
  -> participates in semantic interpretation
```

Do not move search labels into `traits`, and do not infer semantics from `tags`.

The semantic parser validates `tags` as a sequence of non-empty unique strings and removes it before interpreting the canonical semantic document.

## 6. Domain Type frontier

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

## 7. Named semantic members use `set`

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

## 8. Structured semantic field declarations

Tooling admits structured semantic field values without becoming a generic YAML editor.

Examples:

```text
state.Extensions={sequence: StreetExtension}
input.Ext={sequence: string}
representation.Ext={type: {sequence: string}}
```

Discovery advertises the corresponding grammar forms. `from` and `mapping` remain separate local semantic decisions and cannot be smuggled through a structured field declaration.

## 9. Local state and representation relations

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

## 10. Grammar-driven representation mappings

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

## 11. Transform authoring

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

## 12. Equality, maintained values and variants

Equality is established with `set` when required by the active classification contract.

Maintained members are named semantic assertions and therefore use `set` at `values.<member>`.

Sum variants are likewise named semantic assertions and use `set` at `variants.<variant>`.

Nested variant editing remains constrained by explicitly admitted contracts; addressability alone does not authorize arbitrary deep YAML mutation.

## 13. `search`

`search` locates VSIR artifacts beneath the current directory using filters of the form:

```text
<property>:<operator>:<value>
```

The motivating searchable metadata case is:

```text
vslices search --filter tags:contains:ticket
```

Current operators include `contains` and `equals` for the supported root-property cases. Search is read-only and respects artifact discovery exclusions.

Search may also inspect semantic root properties, but that does not make search metadata semantic or semantic fields free-form metadata.

## 14. Lowering lifecycle

Authoring commands and lowering commands operate on the same canonical VSIR semantic language from opposite directions:

```text
partial knowledge
  -> new / discovery / update
  -> artifact surface
       tags metadata -----> search/index only
       semantic VSIR -----> lower
  -> target witness
```

Before semantic parsing/lowering, valid `tags` metadata is removed. Lowering therefore cannot accidentally derive target behavior from search labels.

Current lowering evidence from `Location` includes:

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

A VSIR semantic construction is considered fully supported by Tooling when:

```text
discovery can explain how to express it
new/update can author it
validation can check it
lower can consume it
Ruleset can materialize it when target realization is required
```

`Location.vsir` is the strongest current witness for this semantic parity. Tags are intentionally outside the parity relation because they never enter semantic interpretation.

## 16. Agent-facing invariants

- `tags` is always an available metadata affordance once an artifact can be resolved;
- `tags` exists to support indexing/grouping/search, including `vslices search --filter tags:contains:<value>`;
- `tags` carries no semantic or lowering authority;
- `traits` remains a distinct semantic capability surface;
- no command may silently invent unsupported semantics;
- `set` means establish-or-replace for ordinary assertions;
- `add` is reserved for genuine collection membership (`tags` and `traits` currently);
- semantic discovery remains state-dependent and may be projected without persistence;
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
- [`vslices/tooling`](https://github.com/vslices/tooling) — CLI, parser, validator, discovery, mutation, search metadata and lowering mechanisms.
- [`vslices/ruleset`](https://github.com/vslices/ruleset) — deterministic target realization knowledge.
- [`vslices/planifications`](https://github.com/vslices/planifications) — progressive source reconstruction process and feedback loops.
