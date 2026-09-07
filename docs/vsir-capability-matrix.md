# VSIR capability evidence matrix

Status: experimental review aid for the current Ticket Support / Identities reconstruction.

This matrix is not a permanent product contract. It records where each semantic form is currently supported across the toolchain so an observed gap can be assigned to the first responsible layer instead of being worked around elsewhere.

The columns mean:

```text
discover
  -> discovery can advertise the semantic decision and its value grammar

author
  -> new/update can establish the semantic form without manual YAML editing

parse
  -> the canonical VSIR 0.1 parser preserves the form

conform
  -> semantic validation can establish that the complete artifact satisfies the current contract

lower
  -> lowering can consume the preserved form without inventing semantics

ruleset
  -> deterministic target realization exists when the form requires target knowledge
```

Legend:

```text
yes      executable evidence exists in this branch
partial  some admitted forms are evidenced but corpus parity is incomplete
gated    experimental knowledge exists, but the public authoring surface does not advertise it
n/a      the layer is not required for the semantic form
open     evidence has not crossed the layer yet
```

The current public Domain Type envelope is deliberately narrower than every historical experiment and follows the concrete corpus:

```text
kind: domain-type
shape: product
classification: value-object | identifier
traits: transform | identifier | refined
```

Classification and traits are independent semantic axes. `TicketId` directly witnesses identifier classification. `TicketCode` witnesses value-object classification plus an explicit `identifier` capability trait. Either form establishes identifier semantics and therefore requires equality. `refined` is another semantic capability expressed through traits. `transform` is currently required for a conforming Domain Type by the canonical validator.

| Semantic form | discover | author | parse | conform | lower | ruleset | Current witness / note |
| --- | --- | --- | --- | --- | --- | --- | --- |
| product field, named type | yes | yes | yes | yes | yes | n/a | StreetName / Location |
| unary semantic type (`sequence`) | yes | yes | yes | yes | yes | yes | Location `Extensions` / `Ext` |
| derived state `.from` | yes | yes | yes | yes | yes | n/a | Location `Region`, `Province` |
| direct representation `.from` | yes | yes | yes | yes | yes | n/a | canonical direct reuse |
| representation `stringify` | yes | yes | yes | yes | yes | yes | SrvIdentityId |
| representation `represent` | yes | yes | yes | yes | yes | yes | Location |
| representation `select` | yes | yes | yes | yes | yes | yes | Location |
| representation `map` | yes | yes | yes | yes | yes | yes | Location `Ext` |
| representation intrinsic | yes | yes | yes | yes | yes | yes when intrinsic exists | StreetExtension-style mapping |
| transform product input | yes | yes | yes | yes | yes | n/a | StreetName / Location / TicketId / TicketCode |
| transform scalar input | yes | yes | yes | yes | yes | n/a | SrvIdentityId |
| direct product input -> state with no explicit construction steps | yes | yes | yes | yes | yes | n/a | TicketId; absence of `construction` means zero explicit steps and validation proves direct state establishment |
| construction `normalize` | yes | yes | yes | yes | yes | yes | TicketCode `input.Value` + `trim`; normalized value flows into ensure and state construction |
| construction `ensure` | yes | yes | yes | yes | yes | yes when intrinsic exists | StreetName / StreetExtension / TicketCode |
| construction `resolve` | yes | yes | yes | yes | yes | yes | Location `Commune` |
| construction `apply`, direct | yes | yes | yes | yes | yes | yes | Location `StreetName` |
| construction `apply`, mapped/container | yes | yes | yes | yes | yes | yes | Location `StreetExtension` |
| construction `refine` | yes | yes | yes | yes | yes | n/a | Location / StreetName / SrvIdentityId |
| identifier classification + equality intrinsic | yes | yes | yes | yes | yes | yes | TicketId |
| identifier trait on value-object + equality intrinsic | yes | yes | yes | yes | yes | yes | TicketCode |
| identifier classification + equality over semantic type | yes | yes | yes | yes | yes | yes | SrvIdentityId |
| refined trait + `refined-from` | yes | yes | yes | yes | yes | n/a | SrvIdentityId authoring/parsing + lowering witness |
| sum variants | gated | gated | open | open | open | open | historical authoring experiment retained as research only; Name is the next corpus witness |
| maintained values | gated | gated | open | open | open | open | historical authoring experiment retained as research only; IdentityType is the next corpus witness |
| entity / aggregate-root classification | gated | gated | open | open | open | open | not advertised until a canonical consumer crosses the full pipeline |
| aggregate identity | open | open | open | open | open | open | SrvIdentity exposes remaining parity work |
| TicketTrayFilter flattening relation | open | open | open | open | open | open | deliberately gated; `Option<X.Repr> -> string?` is not inferred |

## Artifact metadata acceptance

`tags` belongs to the canonical `.vsir` artifact surface but carries no domain-semantic authority. It is validated by the public `VsirParser`, stripped before semantic interpretation, and is therefore accepted consistently by conformance assessment and by `transpile` / `lower` / `rebase` without changing the semantic document they consume.

This is an acceptance-parity requirement, not a lowering rule:

```text
authorable artifact metadata
  -> accepted by the common artifact parser
  -> semantic effect = none
```

## How to use this matrix

When a consumer exposes a new difficulty, classify it by the first `open`/`partial`/`gated` column rather than weakening another layer.

For example:

```text
discovery cannot describe an already-parseable admitted form
  -> authoring/discovery gap

parser cannot preserve a form present in the specification or concrete admitted corpus
  -> parser/conformance gap

lower cannot consume a conforming form
  -> lowering mechanism or target-knowledge gap

Ruleset lacks a deterministic realization
  -> Ruleset gap

historical authoring knows a form that parser/lower cannot consume
  -> gate it; do not advertise it as public capability yet
```

A successful parity experiment should move a row from left to right using the same semantic artifact. It should not make the row look complete by translating that artifact into a different grammar between authoring and lowering.

The TicketId/TicketCode corrections add an important evidence rule: an implementation restriction in the current validator or authoring surface is not stronger authority than admitted corpus evidence. When the two conflict, locate and repair the first stale layer instead of rewriting the corpus to match the accidental restriction.

## Canonical-surface rule

There is one admitted VSIR 0.1 surface in this experiment and one public artifact parser at the boundary. Conformance assessment and target materialization must enter through that artifact parser rather than bypassing metadata handling or validation.

Pre-normalized experimental grammars are not a compatibility target and must not receive fallback parsing or lowering behavior. Because VSlices currently has one consumer, migration cost is preferred over carrying ambiguous compatibility indefinitely.
