# Semantic authoring affordances

Status: experimental interaction contract implemented by the current VSlices CLI authoring surface.

This document defines the interaction model between `new vsir`, `discovery vsir`, and `update vsir`. VSIR language semantics remain owned by [`vslices/intermediate-representation`](https://github.com/vslices/intermediate-representation). Target realization knowledge remains owned by [`vslices/ruleset`](https://github.com/vslices/ruleset). The migration/reconstruction traversal that uses these capabilities is described by [`vslices/planifications`](https://github.com/vslices/planifications).

## 1. Principle

Progressive VSIR authoring is navigable from the current artifact state without requiring a human or AI client to know the complete authoring grammar in advance.

```text
new
  -> establish an initial progressive state

discovery
  -> expose the semantic decisions currently available
  -> expose command templates for those decisions
  -> expose the admitted grammar for structured values

update
  -> execute one or more advertised semantic transitions atomically
  -> produce the next progressive state

discovery
  -> evaluate the new state again
```

Conceptually:

```text
ArtifactState
  -> Discovery
  -> SemanticAffordances[]
       -> ValueGrammarAffordances[]
  -> Update
  -> ArtifactState'
```

This is analogous to hypermedia-driven navigation: the current state advertises transitions the client may follow next, but the resource being navigated is a semantic state space rather than an HTTP resource graph.

> An authoring client should not need to infer a valid next operation when the current state can advertise it.

The same applies recursively to values:

> An authoring client should not need an embedded copy of a value grammar when discovery can advertise the forms admitted for that value.

This recursive property is **grammar-driven discovery**.

## 2. Semantic affordance

A discovery entry is an executable authoring affordance, not merely documentation.

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

Organizational metadata is deliberately outside canonical VSIR semantics. Search labels, indexes, provenance annotations or future workflow metadata must use a separate metadata surface rather than being persisted as undeclared root keys in `.vsir`.

## 3. Operation semantics

Operations describe semantic intent rather than incidental YAML storage mechanics.

### `set`

`set` means **establish this semantic assertion with the supplied value**.

For an ordinary semantic property or named map member, `set` creates the assertion when absent and replaces it when already present.

```text
state.Extensions absent
  + set state.Extensions={sequence: StreetExtension}
  -> establish

state.Extensions present
  + set state.Extensions={sequence: AnotherExtension}
  -> replace
```

Internal mutation code may realize the first case as a mapping insertion. That is not part of the public CLI semantics.

### `add`

`add` is reserved for collection semantics where union is meaningful independently of replacement.

Current semantic example:

```text
traits
```

`add` against an ordinary assertion fails closed and instructs the client to use `set`.

### `remove`

`remove` withdraws an assertion or collection member only when the current contract permits it. Required facts may therefore expose `set` without `remove`.

### Ordered structures

Ordered structures without stable member identity use whole-boundary `set`.

Current example:

```text
construction
  -> set complete ordered sequence
```

## 4. State-driven discovery

Discovery is computed from the current artifact, not from a static list of all legal VSIR properties.

Examples:

```text
classification: maintained
  -> equality required
  -> values required

traits contains transform
  -> input required while absent
  -> construction required while absent

representation.Value has from
  -> mapping unavailable

representation.Value has mapping
  -> from unavailable
```

Discovery may also project a candidate without persistence:

```text
vslices discovery vsir StreetName --set kind=domain-type
```

Projection uses the same public mutation/candidate-validation path as `update`, then discards the candidate.

## 5. Grammar-driven discovery

Structured semantic values are not exposed as opaque YAML blobs. Discovery advertises the grammar admitted for those values.

The distinction is:

```text
artifact affordance
  -> what semantic assertion can be established now

value grammar affordance
  -> which semantic forms can be composed to express its value
```

For a representation mapping, discovery currently advertises expression forms including:

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

The grammar is compositional. `select.source` is an `expression`, so it can itself be a `represent` expression:

```yaml
select:
  source:
    represent: state.Street
  field: Value
```

This distinction is semantic, not cosmetic:

```text
Select(Represent(state.Street), Value)
  !=
Select(state.Street, Value)
```

unless an explicit rule proves equivalence. Discovery therefore advertises valid composition but does not choose the domain decision on behalf of the author.

The current grammar-driven layer also advertises structured semantic field declarations and the construction forms exercised by the consumer corpus, including `ensure`, `resolve`, direct `apply`, mapped `apply`, and state `refine`.

Grammar-driven discovery is constrained semantic authority, not generic YAML-schema introspection.

## 6. Atomicity and fail-closed behavior

Every update remains one semantic transaction:

```text
read current artifact
  -> authorize transition
  -> parse value according to admitted value kind
  -> build candidate
  -> validate candidate
  -> serialize
  -> atomic replacement
```

If any step fails, the original artifact remains unchanged.

An advertised command is permission to attempt a transition, not permission to bypass validation.

## 7. Agent-facing traversal

A capable authoring agent should be able to begin with only:

```text
vslices new vsir <artifact>
vslices discovery vsir <artifact>
vslices update vsir <artifact> ...
```

and then traverse:

```text
create
  -> discover
  -> choose an advertised affordance
  -> inspect advertised value grammar
  -> compose one admitted value
  -> execute advertised command
  -> discover again
```

Tests claiming progressive CLI authorability should follow the same rule: they should not use a public mutation or structured value form that the preceding discovery state could not advertise.

## 8. Authoring parity

The current experiment introduced a stronger completeness criterion for Tooling.

A VSIR construction is fully supported when:

```text
discovery can explain how to express it
new/update can author it
validation can check it
lower can consume it
Ruleset can materialize it when target knowledge is required
```

This is **authoring parity**.

Conceptually:

```text
                 one VSIR language
                        |
             +----------+----------+
             |                     |
     new/discovery/update         lower
             |                     |
      can help express it      can consume it
```

The two sides are not mathematical inverses. They traverse the same semantic language from opposite directions:

```text
partial knowledge -> VSIR
VSIR -> target witness
```

`Location.vsir` is the current strongest witness for this parity because it exercises structured types, derived state, representation expressions, transform input, `resolve`, nested `apply`, mapped `apply`, and `refine`.

## 9. Evidence precedence

The Location lowering pass exposed one reusable semantic rule:

> Explicit authored semantic evidence outranks a convenience convention.

For example, when construction explicitly establishes `state.Street` through an `apply` binding followed by `refine`, that explicit relation takes precedence over a same-name `input.Street` convention.

Conventions may reduce authoring burden. They must not override stronger authored evidence.

## 10. Current implementation conformance

Implemented in the current Tooling branch:

```text
progressive new
state-driven discovery
non-persistent discovery projection
command templates emitted from discovered affordances
set = establish-or-replace for ordinary assertions
add restricted to collection-valued semantic surfaces
traits add-remove-set semantics
organizational metadata excluded from canonical VSIR
a local from/mapping mutual exclusion
grammar-driven discovery for structured field declarations
expression grammar discovery for representation mappings
construction grammar discovery
atomic update
fail-closed unsupported transitions
progressive Location reconstruction through discovered surfaces
canonical Location lowering
explicit preservation of represent/select composition
```

Remaining work should be treated as corpus-driven coverage expansion rather than a need for a new authoring protocol.

## 11. Cross-repository authority

Use these repositories together when reconstructing the design:

- [`vslices/intermediate-representation`](https://github.com/vslices/intermediate-representation) — language semantics and conformance.
- [`vslices/tooling`](https://github.com/vslices/tooling) — executable authoring, validation, discovery and lowering mechanisms.
- [`vslices/ruleset`](https://github.com/vslices/ruleset) — target-owned deterministic realization knowledge.
- [`vslices/planifications`](https://github.com/vslices/planifications) — progressive migration/reconstruction traversal and feedback loops.

Unknown semantics remain unknown. Missing authority remains a closed frontier rather than an invitation to invent syntax.
