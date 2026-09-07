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
partial  some forms are admitted but corpus parity is incomplete
n/a      the layer is not required for the semantic form
open     evidence has not crossed the layer yet
```

| Semantic form | discover | author | parse | conform | lower | ruleset | Current witness / note |
| --- | --- | --- | --- | --- | --- | --- | --- |
| product field, named type | yes | yes | yes | yes | yes | n/a | StreetName / Location |
| unary semantic type (`sequence`) | yes | yes | yes | yes | yes | yes | Location `Extensions` / `Ext` |
| derived state `.from` | yes | yes | yes | yes | yes | n/a | Location `Region`, `Province` |
| direct representation `.from` | yes | yes | yes | yes | yes | n/a | IdentityType-style direct reuse |
| representation `stringify` | yes | yes | yes | yes | yes | yes | SrvIdentityId |
| representation `represent` | yes | yes | yes | yes | yes | yes | Location |
| representation `select` | yes | yes | yes | yes | yes | yes | Location |
| representation `map` | yes | yes | yes | yes | yes | yes | Location `Ext` |
| representation intrinsic | yes | yes | yes | yes | yes | yes when intrinsic exists | StreetExtension-style mapping |
| transform product input | yes | yes | yes | yes | yes | n/a | StreetName / Location |
| transform scalar input | yes | yes | yes | yes | yes | n/a | SrvIdentityId |
| construction `ensure` | yes | yes | yes | yes | yes | yes when intrinsic exists | StreetName / StreetExtension |
| construction `resolve` | yes | yes | yes | yes | yes | yes | Location `Commune` |
| construction `apply`, direct | yes | yes | yes | yes | yes | yes | Location `StreetName` |
| construction `apply`, mapped/container | yes | yes | yes | yes | yes | yes | Location `StreetExtension` |
| construction `refine` | yes | yes | yes | yes | yes | n/a | Location / StreetName |
| equality intrinsic | yes | yes | yes | yes | yes | yes | IdentityType-style equality |
| equality over semantic type | yes | yes | yes | yes | yes | yes | SrvIdentityId |
| sum variants | partial | partial | open | open | open | open | Name is the next corpus witness to verify |
| maintained values | partial | partial | open | open | open | open | IdentityType is the next corpus witness to verify |
| refined / `refined-from` | open | open | partial | partial | partial | n/a | SrvIdentityId exposes remaining authoring parity work |
| aggregate identity | open | open | open | open | open | open | SrvIdentity exposes remaining parity work |
| TicketTrayFilter flattening relation | open | open | open | open | open | open | deliberately gated; `Option<X.Repr> -> string?` is not inferred |

## How to use this matrix

When a consumer exposes a new difficulty, classify it by the first `open`/`partial` column rather than weakening another layer.

For example:

```text
discovery cannot describe an already-parseable form
  -> authoring/discovery gap

parser cannot preserve a form present in the specification
  -> parser/conformance gap

lower cannot consume a conforming form
  -> lowering mechanism or target-knowledge gap

Ruleset lacks a deterministic realization
  -> Ruleset gap
```

A successful parity experiment should move a row from left to right using the same semantic artifact. It should not make the row look complete by translating that artifact into a different grammar between authoring and lowering.

## Canonical-surface rule

There is one admitted VSIR 0.1 surface in this experiment. Pre-normalized experimental grammars are not a compatibility target and must not receive fallback parsing or lowering behavior.

Because VSlices currently has one consumer, migration cost is preferred over carrying ambiguous compatibility indefinitely.
