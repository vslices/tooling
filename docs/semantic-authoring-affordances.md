# Semantic authoring affordances

Status: experimental interaction contract implemented by the current VSlices CLI authoring surface.

This document defines the interaction model between `new vsir`, `discovery vsir`, and `update vsir`. VSIR language semantics remain owned by [`vslices/intermediate-representation`](https://github.com/vslices/intermediate-representation). Target realization knowledge remains owned by [`vslices/ruleset`](https://github.com/vslices/ruleset). The migration/reconstruction traversal that uses these capabilities is described by [`vslices/planifications`](https://github.com/vslices/planifications).

## 1. Principle

Progressive VSIR authoring is navigable from the current artifact state without requiring a human or AI client to know the complete authoring grammar in advance.

```text
new
  -> establish an initial progressive state

discovery
  -> expose always-available artifact metadata operations
  -> expose the semantic decisions currently available
  -> expose command templates for those decisions
  -> expose the admitted grammar for structured values

update
  -> execute one or more advertised metadata or semantic transitions atomically
  -> produce the next progressive state

discovery
  -> evaluate the new state again
```

Conceptually:

```text
ArtifactState
  -> MetadataAffordances[]
  -> Discovery
  -> SemanticAffordances[]
       -> ValueGrammarAffordances[]
  -> Update
  -> ArtifactState'
```

This is analogous to hypermedia-driven navigation: the current state advertises transitions the client may follow next, but the resource being navigated is a semantic state space plus orthogonal operational metadata.

> An authoring client should not need to infer a valid next operation when the current state can advertise it.

The same applies recursively to values:

> An authoring client should not need an embedded copy of a value grammar when discovery can advertise the forms admitted for that value.

This recursive property is **grammar-driven discovery**.

## 2. Affordances: metadata and semantics

Discovery exposes executable authoring affordances, not merely documentation.

An affordance carries:

```text
path
status
meaning
value kind
allowed values, when closed
operations
command templates
value grammar, when structured
```

There are two authority classes.

### Searchable artifact metadata

`tags` is always available as an optional set-valued metadata surface:

```text
tags
  status: optional
  value kind: set<string>
  operations: set, add, remove
```

Its purpose is operational discovery and grouping, including filters such as:

```text
vslices search --filter tags:contains:ticket
```

Tags carry no domain-semantic authority. They do not activate VSIR obligations, change validation meaning, or participate in lowering decisions. The public artifact parser validates the metadata shape and removes it before interpreting the canonical semantic document.

### Semantic affordances

Example:

```text
state
  status: required
  value kind: semantic-field-declaration
  operations: set
  command:
    vslices update vsir Location --set "state.<property>=<semantic-field-declaration>"
```

A local representation decision may advertise:

```text
representation.Street.mapping
  status: optional
  value kind: expression
  operations: set
  command:
    vslices update vsir Location --set "representation.Street.mapping=<expression>"
```

True set-valued semantic surfaces retain collection operations:

```text
traits
  operations: set, add, remove
```

The important distinction is:

```text
tags   -> how an artifact is indexed, grouped or found
traits -> semantic capabilities that affect interpretation
```

Do not use `traits` as a substitute for free-form search labels, and do not infer semantics from `tags`.

## 3. Operation semantics

Operations describe authoring intent rather than incidental YAML storage mechanics.

### `set`

`set` means **establish this assertion with the supplied value**.

For an ordinary semantic property or named map member, `set` creates the assertion when absent and replaces it when already present.

For `tags`, `set` replaces the complete metadata set.

### `add`

`add` is reserved for collection semantics where union is meaningful independently of replacement.

Current collection-valued public surfaces:

```text
tags    searchable metadata
traits  semantic capabilities
```

### `remove`

`remove` withdraws an assertion or collection member only when the current contract permits it. Required facts may therefore expose `set` without `remove`.

### Ordered structures

Ordered structures without stable member identity use whole-boundary `set`.

Current example:

```text
construction
  -> set complete ordered sequence
```

`construction` is not universally required. When product input already establishes direct state coordinates deterministically, zero explicit construction steps is a valid semantic form. `TicketId` is the concrete witness. If additional normalization, validation, resolution, application, or refinement is needed, `construction` carries those ordered steps.

## 4. State-driven discovery

Semantic discovery is computed from the current artifact, not from a static list of every historical or hypothetical VSIR property.

The current end-to-end Domain Type envelope is deliberately narrow and corpus-backed:

```text
kind: domain-type
shape: product
classification: value-object | identifier
traits: transform | identifier | refined
```

Classification and traits are independent semantic axes. `TicketId` witnesses `classification: identifier` without an identifier trait. `TicketCode` witnesses `classification: value-object` combined with the explicit `identifier` trait. Either form establishes identifier capability and therefore requires explicit equality semantics. Equality without identifier capability is rejected. `refined` remains an additional trait-driven capability.

Target contract lowering follows the same separation of semantic facts and the primitive Framework surface:

```text
kind: domain-type
  -> DomainType<T, T.Repr>

classification: identifier
        OR
traits contains identifier
  -> Identifier<T>

traits contains refined
  -> Refined<T, BASE>
```

Framework no longer exposes the representation-composite convenience traits such as `Identifier<T, T.Repr>` or `Refined<T, BASE, T.Repr>`. Canonical lowering therefore emits each justified primitive contract independently rather than reconstructing removed composites.

Examples:

```text
traits does not contain transform
  -> traits is required
  -> transform is an admitted capability

traits contains transform
  -> input required while absent
  -> construction available as an optional ordered boundary when direct input-to-state establishment is insufficient

classification is identifier
  -> equality required

traits contains identifier
  -> equality required

equality exists without either identifier form
  -> invalid

traits contains refined
  -> refined-from required
  -> scalar input required while absent
```

Historical experiments for `sum`, `maintained`, `entity`, and `aggregate-root` are not advertised by the public authoring frontier until parser, conformance and lowering evidence cross the same form end-to-end. They are research candidates, not compatibility promises.

## 5. Grammar-driven discovery

Structured semantic values are not exposed as opaque YAML blobs. Discovery advertises the grammar admitted for those values.

For a representation mapping, discovery currently advertises expression forms including `stringify`, `represent`, `select`, `map`, and `intrinsic`.

The grammar is compositional. `Select(Represent(state.Street), Value)` and `Select(state.Street, Value)` remain distinct unless explicit semantics establish equivalence.

The current construction grammar includes:

```text
normalize
ensure
resolve
apply
refine
```

`TicketCode` is the corpus witness for:

```yaml
- normalize:
    target: input.Value
    intrinsic: trim
```

Normalization updates the semantic reference carried forward, so later `ensure` and final state construction observe the normalized value rather than the original input.

## 6. Atomicity and fail-closed behavior

Every update remains one transaction:

```text
read current artifact
  -> authorize metadata/semantic transitions
  -> parse values according to admitted value kinds
  -> build candidate
  -> validate candidate
  -> serialize
  -> atomic replacement
```

If any step fails, the original artifact remains unchanged.

An advertised command is permission to attempt a transition, not permission to bypass validation.

An additional parity rule applies:

> If the canonical parser/validator/lowering path cannot consume a semantic form, discovery must not advertise that form as an available public transition.

The inverse matters too:

> If an admitted corpus artifact already carries a semantic form, an accidental restriction in one implementation layer must not be promoted into language authority. Repair the first stale layer instead of rewriting the corpus to match it.

`TicketId` exposed classification drift; `TicketCode` exposed the missing `normalize` affordance and the second, trait-based route to identifier capability. `SrvIdentityId` exposed that a Framework convenience composite must not become semantic authority for target contract selection.

## 7. Agent-facing traversal

A capable authoring agent should be able to begin with only:

```text
vslices new vsir <artifact>
vslices discovery vsir <artifact>
vslices update vsir <artifact> ...
```

and then traverse through advertised operations and grammars without embedding a second copy of the VSIR authoring language.

## 8. Authoring parity

A semantic VSIR construction is fully supported when:

```text
discovery can explain how to express it
new/update can author it
validation can check it
lower can consume it
Ruleset can materialize it when target knowledge is required
```

Artifact metadata has the related invariant:

```text
authorable metadata
  -> accepted by the common artifact parser
  -> semantic effect = none
```

Current parity witnesses:

```text
Location
  -> structured types, derived state, representation composition,
     resolve, apply direct/mapped, refine

TicketId
  -> identifier classification, equality, direct product input-to-state,
     zero explicit construction steps

TicketCode
  -> value-object classification + identifier trait,
     normalize trim, ensure, equality,
     normalized input flowing into state construction

SrvIdentityId
  -> identifier classification + refined trait,
     scalar input, refined-from, semantic equality, stringify, refine,
     primitive DomainType + Identifier + Refined target contracts
```

## 9. Evidence precedence

Two current precedence rules are explicit:

> Explicit authored semantic evidence outranks a convenience convention.

and:

> Concrete admitted corpus evidence outranks an accidental implementation restriction.

Search metadata is never semantic evidence for either rule.

## 10. Current implementation conformance

Implemented in the current Tooling branch:

```text
progressive new
always-available searchable tags metadata
state-driven semantic discovery
set/add/remove mutations with repeated CLI occurrences preserved
one public VsirParser artifact boundary
product + value-object/identifier public Domain Type envelope
transform / identifier / refined trait authoring
kind: domain-type -> DomainType<T,T.Repr> target contract
identifier classification OR identifier trait -> equality obligation
identifier classification OR identifier trait -> Identifier<T> target contract
refined trait -> Refined<T,BASE> target contract
equality without identifier capability -> fail closed
product transform input may establish same-name state directly
construction grammar includes normalize / ensure / resolve / apply / refine
normalize trim is corpus-backed by TicketCode
atomic update
canonical C# lowering
explicit semantic expression preservation
sum / maintained / entity / aggregate-root remain gated
```

Remaining work should be treated as corpus-driven coverage expansion rather than a need for a new authoring protocol.
