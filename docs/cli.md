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
  -> establish version + semantic name only

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

`new vsir` always starts from zero semantic knowledge. Its only public input is the semantic name:

```text
vslices new vsir StreetName
```

It creates exactly:

```yaml
vsir: 0.1
name: StreetName
```

`new vsir` exposes no flags. It does not accept semantic shortcuts such as `--kind`, `--shape`, or `--classification`, and it does not expose output-routing, stdout, or force/overwrite flags.

All metadata and semantic knowledge after identity flows through the single public authoring protocol:

```text
new
  -> discovery
  -> update
  -> discovery
```

This keeps `new` from becoming a second authoring grammar or a bypass around state-driven discovery.

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

Repeated options are preserved as distinct mutations in one atomic invocation:

```text
vslices update vsir TicketId --set shape=product --set classification=identifier
vslices update vsir TicketCode --set state.Value=string --set representation.Value=string --add traits=transform --add traits=identifier
```

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
vslices update vsir TicketCode --add traits=identifier
```

Using `add` on ordinary semantic assertions fails closed.

### `remove`

`remove` withdraws an assertion or collection member only when the current contract permits it.

For set-valued surfaces it targets a member:

```text
vslices update vsir StreetName --remove "tags=ticket"
vslices update vsir SrvIdentityId --remove "traits=refined"
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

The public `VsirParser` validates `tags` as a sequence of non-empty unique strings and removes it before interpreting the canonical semantic document. Conformance assessment, `transpile`, `lower`, and `rebase` enter through that same artifact parser.

## 6. Current Domain Type frontier

For `kind: domain-type`, the current public end-to-end envelope is:

```text
shape: product
classification: value-object | identifier
traits: transform | identifier | refined
```

Classification and traits are independent semantic axes. `TicketId` demonstrates `classification: identifier` with no identifier trait. `TicketCode` demonstrates `classification: value-object` plus explicit `identifier` capability. Either form activates equality semantics and lowers to the Framework `Identifier<T, T.Repr>` contract.

The base structural decisions are:

```text
shape
state
representation
classification
traits
```

Current public shape:

```text
product
```

Current public classifications:

```text
value-object
identifier
```

Current explicit trait vocabulary:

```text
transform
identifier
refined
```

Obligations include:

```text
transform                         -> input
identifier classification        -> equality
identifier trait                 -> equality
refined trait                    -> refined-from + scalar refined input constraints
```

`construction` is available for ordered semantic steps but is not universally required. If product input already establishes same-name state coordinates deterministically, zero explicit construction steps is valid; `TicketId` is the current witness.

Historical experiments around `sum`, `maintained`, `entity`, and `aggregate-root` are deliberately gated. Tooling does not advertise them as current authoring capabilities until the same form has executable parser, conformance, and lowering evidence.

## 7. Named semantic members use `set`

Named members are assertions, not set-union operations.

Examples from the admitted surface:

```text
vslices update vsir Location --set "state.Commune=Commune"
vslices update vsir Location --set "representation.Street=string"
vslices update vsir Location --set "input.CommuneId=CommuneId"
vslices update vsir SrvIdentityId --set "refined-from=Rut"
vslices update vsir TicketId --set "equality={intrinsic: ordinal-equals, by: state.Value}"
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

Derived state uses `state.<property>.from`. Representation may use either `representation.<property>.from` or `representation.<property>.mapping`; the two are mutually exclusive for the same field.

## 10. Grammar-driven representation mappings

`representation.<field>.mapping` accepts an expression grammar advertised by `discovery`.

Current forms exercised by the corpus include:

```text
stringify
represent
select
map
intrinsic
```

The grammar is compositional. `select(represent(state.Street), Value)` and `select(state.Street, Value)` remain distinct semantic trees. Tooling does not insert `represent` implicitly.

## 11. Transform, identifier and refined authoring

The current trait vocabulary is:

```text
transform
identifier
refined
```

### `transform`

`transform` activates the root `input` obligation. Root input may be scalar or product-shaped and authored progressively.

When product input already determines direct state coordinates, no explicit construction sequence is needed. `TicketId` demonstrates this form.

When extra semantic work is needed, `construction` is ordered and uses whole-boundary `set`. Current construction forms exercised by grammar-driven discovery include:

```text
normalize
ensure
resolve
apply
refine
```

`TicketCode` demonstrates normalization:

```text
vslices update vsir TicketCode --set "construction=[{normalize: {target: input.Value, intrinsic: trim}}, {ensure: {condition: {intrinsic: non-empty, args: {value: input.Value}}, failure: {message: 'Debes especificar el correlativo de la solicitud'}}}]"
```

One semantic `apply` covers both direct and mapped/container input shapes. Target-specific realization such as `Apply` versus `ApplySeq` belongs to lowering/Ruleset knowledge, not VSIR command vocabulary.

### `identifier`

Identifier capability has two currently evidenced forms:

```text
classification: identifier
```

and:

```text
classification: value-object
traits: [..., identifier]
```

The first is witnessed by `TicketId`; the second by `TicketCode`. They are not structurally synonymous declarations, but both establish identifier capability. Therefore both require an explicit `equality` boundary and both lower to `Identifier<T, T.Repr>`.

Examples:

```text
vslices update vsir TicketId --set classification=identifier
vslices update vsir TicketCode --add traits=identifier
vslices update vsir TicketCode --set "equality={intrinsic: ordinal-equals, by: state.Value}"
```

Equality without either identifier classification or identifier trait fails closed.

### `refined`

`refined` activates `refined-from` plus canonical refined construction constraints. A refined identifier such as `SrvIdentityId` combines `classification: identifier` with `traits: [transform, refined]`.

## 12. Gated semantic families

The CLI previously contained experimental authoring knowledge for semantic families that had not crossed the canonical consumer path. They are now gated rather than advertised:

```text
sum variants
maintained values
entity classification
aggregate-root classification
```

The rule is:

```text
no parser/conformance/lowering evidence
  -> no public discovery affordance
```

The complementary evidence rule is:

```text
admitted corpus form conflicts with an implementation restriction
  -> repair the stale implementation layer
  -> do not rewrite the corpus to fit the restriction
```

`TicketId` and `TicketCode` are concrete witnesses for this second rule.

## 13. `search`

`search` locates VSIR artifacts beneath the current directory using filters of the form `<property>:<operator>:<value>`. The motivating searchable metadata case is:

```text
vslices search --filter tags:contains:ticket
```

Search is read-only and respects artifact discovery exclusions.

## 14. Lowering lifecycle

Authoring commands and lowering commands operate on the same canonical VSIR semantic language from opposite directions:

```text
partial knowledge
  -> new / discovery / update
  -> .vsir artifact
       tags metadata -----> search/index only
       semantic VSIR -----> VsirParser -----> transpile / lower / rebase
  -> target witness
```

`TicketId` adds evidence for identifier classification, intrinsic ordinal equality, product transform input, and direct input-to-state construction with no explicit construction sequence.

`TicketCode` adds evidence for value-object classification plus identifier trait, normalize/trim, equality, and normalized input flowing into validation and state construction.

`SrvIdentityId` adds evidence for identifier classification combined with refined semantics.

## 15. Authoring parity

A VSIR semantic construction is considered fully supported by Tooling when:

```text
discovery can explain how to express it
new/update can author it
validation can check it
lower can consume it
Ruleset can materialize it when target realization is required
```

Because `new` contributes only version and name, semantic authoring parity is specifically exercised through `discovery/update` from that minimal starting point.

## 16. Agent-facing invariants

- `new vsir` has no flags and establishes only VSIR version plus semantic name;
- every metadata or semantic fact after identity is authored through `discovery` / `update`;
- `tags` is always an available metadata affordance once an artifact can be resolved;
- `tags` carries no semantic or lowering authority;
- `traits` is a distinct semantic capability surface;
- conformance, transpile, lower and rebase use the common metadata-aware artifact parser;
- `product` with `value-object | identifier` is the current public Domain Type envelope;
- `transform`, `identifier`, and `refined` are current explicit semantic traits;
- identifier capability may be established by identifier classification or identifier trait;
- identifier capability requires explicit equality semantics and maps to the Framework Identifier contract;
- construction grammar includes `normalize`, with TicketCode as the concrete witness;
- product transform input may establish matching state directly without an explicit `construction` sequence;
- `sum`, `maintained`, `entity`, and `aggregate-root` remain gated until corpus evidence crosses the complete pipeline;
- no command may silently invent unsupported semantics;
- repeated `--set`, `--add`, and `--remove` occurrences are preserved;
- ordinary named assertions use `set`; collection membership uses `add/remove`;
- structured values use discovery-advertised grammar;
- Ruleset owns target realization knowledge;
- unknown semantics remain unknown.
