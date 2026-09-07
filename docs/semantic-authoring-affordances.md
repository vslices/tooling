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

```text
state.Extensions absent
  + set state.Extensions={sequence: StreetExtension}
  -> establish

state.Extensions present
  + set state.Extensions={sequence: AnotherExtension}
  -> replace
```

For `tags`, `set` replaces the complete metadata set.

Internal mutation code may realize the first case as a mapping insertion. That is not part of the public CLI semantics.

### `add`

`add` is reserved for collection semantics where union is meaningful independently of replacement.

Current collection-valued public surfaces:

```text
tags    searchable metadata
traits  semantic capabilities
```

`add` against an ordinary semantic assertion fails closed and instructs the client to use `set`.

### `remove`

`remove` withdraws an assertion or collection member only when the current contract permits it. Required facts may therefore expose `set` without `remove`.

For set-valued surfaces such as `tags` and `traits`, removal targets a member:

```text
vslices update vsir StreetName --remove "tags=ticket"
```

### Ordered structures

Ordered structures without stable member identity use whole-boundary `set`.

Current example:

```text
construction
  -> set complete ordered sequence
```

## 4. State-driven discovery

Semantic discovery is computed from the current artifact, not from a static list of every historical or hypothetical VSIR property.

The current end-to-end Domain Type envelope is deliberately narrow:

```text
kind: domain-type
shape: product
classification: value-object
traits: transform | identifier | refined
```

`transform` is currently required by canonical Domain Type validation. `identifier` and `refined` are additional semantic capabilities expressed through `traits`, not alternate classifications.

Examples:

```text
traits does not contain transform
  -> traits is required
  -> transform is an admitted capability

traits contains transform
  -> input required while absent
  -> construction required while absent

traits contains identifier
  -> equality required

traits contains refined
  -> refined-from required
  -> scalar input required while absent

representation.Value has from
  -> mapping unavailable

representation.Value has mapping
  -> from unavailable
```

Historical experiments for `sum`, `maintained`, `entity`, and `aggregate-root` are not advertised by the public authoring frontier until parser, conformance and lowering evidence cross the same form end-to-end. They are research candidates, not compatibility promises.

`tags` is deliberately different: it is an always-available metadata affordance and does not depend on `kind`, `shape`, `classification`, or semantic completeness.

Discovery may also project a candidate without persistence:

```text
vslices discovery vsir StreetName --set kind=domain-type
vslices discovery vsir StreetName --add tags=ticket
```

Projection uses the same public mutation/candidate-validation paths as `update`, then discards the candidate.

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

An additional parity rule now applies to advertised semantics:

> If the canonical parser/validator/lowering path cannot consume a semantic form, discovery must not advertise that form as an available public transition.

That rule is why experimental `sum` and `maintained` authoring knowledge is gated instead of exposed prematurely.

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
  -> optionally classify/index with tags at any point
  -> choose an advertised semantic affordance
  -> inspect advertised value grammar
  -> compose one admitted value
  -> execute advertised command
  -> discover again
```

Tests claiming progressive CLI authorability should follow the same rule: they should not use a public semantic mutation or structured value form that the preceding discovery state could not advertise. Tags are the deliberate exception because the metadata affordance is invariant across semantic states.

## 8. Authoring parity

The current experiment introduced a stronger completeness criterion for Tooling.

A semantic VSIR construction is fully supported when:

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

Artifact acceptance is slightly broader because searchable metadata is orthogonal to semantic parity:

```text
.vsir artifact
  -> VsirParser
       tags -> validate + strip from semantic view
       semantic VSIR -> canonical parser/validator
  -> conformance / transpile / lower / rebase
```

Therefore a tagged artifact must be accepted by the same public parser used for conformance and target materialization, while `semantic effect(tags) = none`.

`Location.vsir` is the strongest structured-expression witness for semantic parity because it exercises structured types, derived state, representation expressions, transform input, `resolve`, nested `apply`, mapped `apply`, and `refine`.

`SrvIdentityId.vsir` is now an additional parity witness for scalar transform input plus `identifier` and `refined` traits, `refined-from`, equality over a semantic type, `stringify`, and final refinement.

## 9. Evidence precedence

The Location lowering pass exposed one reusable semantic rule:

> Explicit authored semantic evidence outranks a convenience convention.

For example, when construction explicitly establishes `state.Street` through an `apply` binding followed by `refine`, that explicit relation takes precedence over a same-name `input.Street` convention.

Conventions may reduce authoring burden. They must not override stronger authored evidence.

Search metadata is never evidence for this precedence relation.

## 10. Current implementation conformance

Implemented in the current Tooling branch:

```text
progressive new
always-available searchable tags metadata
state-driven semantic discovery
non-persistent discovery projection for tags and semantics
command templates emitted from discovered affordances
set = establish-or-replace for ordinary assertions
add restricted to collection-valued public surfaces
tags add-remove-set metadata semantics
traits add-remove-set semantic semantics
one public VsirParser boundary for metadata-aware conformance and target materialization
product + value-object public Domain Type envelope
transform / identifier / refined trait authoring
refined-from and equality obligations driven by traits
a local from/mapping mutual exclusion
grammar-driven discovery for structured field declarations
expression grammar discovery for representation mappings
construction grammar discovery
atomic update
fail-closed unsupported transitions
progressive Location reconstruction through discovered surfaces
progressive SrvIdentityId refined/identifier reconstruction
canonical C# lowering
explicit preservation of represent/select composition
sum / maintained / entity / aggregate-root authoring gated until end-to-end evidence exists
```

Remaining work should be treated as corpus-driven coverage expansion rather than a need for a new authoring protocol.

## 11. Cross-repository authority

Use these repositories together when reconstructing the design:

- [`vslices/intermediate-representation`](https://github.com/vslices/intermediate-representation) — language semantics and conformance.
- [`vslices/tooling`](https://github.com/vslices/tooling) — executable authoring, validation, discovery and lowering mechanisms.
- [`vslices/ruleset`](https://github.com/vslices/ruleset) — target-owned deterministic realization knowledge.
- [`vslices/planifications`](https://github.com/vslices/planifications) — progressive migration/reconstruction traversal and feedback loops.

Unknown semantics remain unknown. Missing authority remains a closed semantic frontier rather than an invitation to infer meaning from metadata.
