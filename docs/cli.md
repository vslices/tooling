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

All metadata and semantic knowledge after identity flows through:

```text
new -> discovery -> update -> discovery
```

## 3. `discovery vsir`

`discovery` reports always-available artifact metadata plus the state-dependent semantic frontier. An entry can include path, status, meaning, value kind, allowed values, operations, command templates, and value grammar.

`tags` is always present as optional `set/add/remove` searchable metadata.

Discovery can also project candidate mutations without persistence through the same mutation and validation path used by `update`.

## 4. Public mutation semantics

`update vsir` uses:

```text
set
add
remove
```

`set` establishes or replaces an assertion. `add/remove` are reserved for genuine collection membership such as `tags` and `traits`.

Repeated options are preserved as distinct mutations in one atomic invocation:

```text
vslices update vsir TicketId --set shape=product --set classification=identifier
vslices update vsir TicketCode --set state.Value=string --set representation.Value=string --add traits=transform --add traits=identifier
```

Any failure leaves the original artifact unchanged.

## 5. Tags versus traits

```text
tags
  -> free-form operational metadata
  -> indexing, grouping and search
  -> never activates semantic obligations
  -> never guides lowering

traits
  -> constrained semantic capabilities
  -> may activate validation and authoring obligations
  -> participates in semantic interpretation
```

The public `VsirParser` validates and removes tags before canonical semantic interpretation. Conformance, `transpile`, `lower`, and `rebase` enter through that same artifact parser.

## 6. Current Domain Type frontier

```text
kind: domain-type
shape: product
classification: value-object | identifier
traits: transform | identifier | refined
```

Classification and traits are independent semantic axes.

```text
TicketId
  classification: identifier
  traits: [transform]

TicketCode
  classification: value-object
  traits: [transform, identifier]
```

Both establish identifier capability. Therefore both require explicit equality and both lower to `Identifier<T, T.Repr>`.

Current obligations include:

```text
transform                  -> input
identifier classification -> equality
identifier trait          -> equality
refined trait             -> refined-from + refined scalar input constraints
```

Equality without either identifier classification or identifier trait fails closed.

`construction` is an optional ordered boundary. Product input may establish same-name state directly, as in `TicketId`.

## 7. Structured authoring

Named semantic members use `set`. Structured semantic field declarations are admitted through discovery-advertised value grammars rather than generic YAML editing.

Derived state uses `state.<property>.from`. Representation can use direct `.from` or semantic `.mapping`; the two are mutually exclusive for one field.

Representation expressions currently include `stringify`, `represent`, `select`, `map`, and `intrinsic`. Tooling preserves the authored expression tree rather than silently inserting transformations.

## 8. Transform construction grammar

When extra semantic work is needed, `construction` is authored through whole-boundary `set` and currently supports:

```text
normalize
ensure
resolve
apply
refine
```

`TicketCode` is the concrete normalize witness:

```text
vslices update vsir TicketCode --set "construction=[{normalize: {target: input.Value, intrinsic: trim}}, {ensure: {condition: {intrinsic: non-empty, args: {value: input.Value}}, failure: {message: 'Debes especificar el correlativo de la solicitud'}}}]"
```

The normalized semantic reference flows forward into later `ensure` and final state construction.

One semantic `apply` covers both direct and mapped/container shapes; target-specific `Apply` versus `ApplySeq` remains lowering/Ruleset knowledge.

## 9. Identifier and refined semantics

Identifier capability can be established through classification or trait. These declarations are structurally distinct but currently converge on the same discrete-space obligation and C# Framework contract.

Examples:

```text
vslices update vsir TicketId --set classification=identifier
vslices update vsir TicketCode --add traits=identifier
vslices update vsir TicketCode --set "equality={intrinsic: ordinal-equals, by: state.Value}"
```

`refined` remains trait-driven. `SrvIdentityId` combines `classification: identifier` with `traits: [transform, refined]`.

## 10. Gated semantic families

`sum`, `maintained`, `entity`, and `aggregate-root` remain gated until one canonical artifact crosses discovery, authoring, parsing, conformance, lowering, and required Ruleset realization.

The rules are:

```text
no parser/conformance/lowering evidence
  -> no public discovery affordance

admitted corpus form conflicts with implementation restriction
  -> repair the stale implementation layer
  -> do not rewrite the corpus to fit it
```

## 11. Search

Search filters use:

```text
<property>:<operator>:<value>
```

The motivating metadata case is:

```text
vslices search --filter tags:contains:ticket
```

Search is read-only and does not turn metadata into semantics.

## 12. Lowering lifecycle and parity

```text
partial knowledge
  -> new / discovery / update
  -> .vsir artifact
       tags metadata -----> search/index only
       semantic VSIR -----> VsirParser -----> transpile / lower / rebase
  -> target witness
```

A semantic construction has authoring parity when discovery can explain it, update can author it, validation can check it, lower can consume it, and Ruleset can materialize required target knowledge.

Current important witnesses:

```text
Location
  -> structured state/representation, resolve, apply, refine

TicketId
  -> identifier classification, equality, direct product input-to-state,
     zero explicit construction steps

TicketCode
  -> value-object classification + identifier trait,
     normalize trim, ensure, equality,
     normalized input carried into state construction

SrvIdentityId
  -> identifier classification + refined trait,
     scalar input, refined-from, semantic equality, stringify, refine
```

Unknown semantics remain unknown; Tooling does not infer target or domain authority from convenience conventions.
