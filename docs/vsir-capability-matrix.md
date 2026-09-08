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
gated    canonical/executable evidence may exist, but public authoring does not advertise the form
rejected the proposed relation is deliberately not admitted
n/a      the layer is not required for the semantic form
open     evidence has not crossed the layer yet
```

The current **public authoring** Domain Type envelope is deliberately narrower than canonical parser/lowering coverage:

```text
kind: domain-type
shape: product
classification: value-object | identifier
traits: transform | identifier | refined
```

This envelope governs `new/discovery/update`; it does not define canonical conformance. Conformance is established by `VsirParser` plus the active semantic validation environment. A canonical `sum` or `maintained` artifact can therefore be conforming while public semantic authoring remains gated.

Classification and traits are independent semantic axes. `TicketId` directly witnesses identifier classification. `TicketCode` witnesses value-object classification plus an explicit `identifier` capability trait. Either form establishes identifier semantics and therefore requires equality. Conversely, equality is rejected when neither form establishes identifier capability. `refined` is another semantic capability expressed through traits. The current publicly authorable product subset requires `transform`; specialized canonical forms have their own validated obligations.

Canonical C# contract lowering keeps semantic facts independent:

```text
kind: domain-type
  -> DomainType<T, T.Repr>

identifier capability
  -> Identifier<T>

refined capability
  -> Refined<T, BASE>
```

Removed Framework representation-composite conveniences such as `Identifier<T,T.Repr>` and `Refined<T,BASE,T.Repr>` are not reconstructed as lowering authority.

| Semantic form | discover | author | parse | conform | lower | ruleset | Current witness / note |
| --- | --- | --- | --- | --- | --- | --- | --- |
| domain-type target contract | yes | yes | yes | yes | yes | n/a | every supported `kind: domain-type` materialization carries the justified `DomainType<T,T.Repr>` contract |
| product field, named type | yes | yes | yes | yes | yes | n/a | StreetName / Location |
| unary semantic type (`sequence`) | yes | yes | yes | yes | yes | yes | Location `Extensions` / `Ext`; target realization is `type.sequence` |
| unary semantic type (`optional`) | yes | yes | yes | yes | yes | yes | TicketTrayFilter / Name; target realization is `type.optional` |
| derived state `.from` | yes | yes | yes | yes | yes | n/a | Location `Region`, `Province` |
| direct representation `.from` | yes | yes | yes | yes | yes | n/a | canonical direct reuse |
| representation `stringify` | yes | yes | yes | yes | yes | yes | SrvIdentityId |
| representation `represent` | yes | yes | yes | yes | yes | yes | Location / TicketTrayFilter |
| representation `select` | yes | yes | yes | yes | yes | yes | Location |
| representation `map` | yes | yes | yes | yes | yes | yes | Location `Ext` / aggregate-root sum optional representation |
| representation intrinsic | yes | yes | yes | yes | yes | yes when intrinsic exists | StreetExtension-style mapping |
| transform product input | yes | yes | yes | yes | yes | n/a | StreetName / Location / TicketId / TicketCode |
| transform scalar input | yes | yes | yes | yes | yes | n/a | SrvIdentityId |
| direct product input -> state with no explicit construction steps | yes | yes | yes | yes | yes | n/a | TicketId; absence of `construction` means zero explicit steps and validation proves direct state establishment |
| construction `normalize` | yes | yes | yes | yes | yes | yes | TicketCode `input.Value` + `trim`; normalized value flows into ensure and state construction |
| construction `ensure` | yes | yes | yes | yes | yes | yes when intrinsic exists | StreetName / StreetExtension / TicketCode / Name variants |
| construction `resolve` | yes | yes | yes | yes | yes | yes | Location `Commune` |
| construction `apply`, direct | yes | yes | yes | yes | yes | yes | Location `StreetName` |
| construction `apply`, mapped/container | yes | yes | yes | yes | yes | yes | Location `StreetExtension` |
| construction `refine` | yes | yes | yes | yes | yes | n/a | Location / StreetName / SrvIdentityId |
| intrinsic refine with named outputs | yes | yes | yes | yes | yes | yes | StreetExtension `split-first-rest`; Ruleset supplies condition + named outputs |
| identifier classification + equality intrinsic | yes | yes | yes | yes | yes | yes | TicketId; lowers identifier capability to `Identifier<T>` |
| identifier trait on value-object + equality intrinsic | yes | yes | yes | yes | yes | yes | TicketCode; lowers identifier capability to `Identifier<T>` |
| identifier classification + equality over semantic type | yes | yes | yes | yes | yes | yes | SrvIdentityId |
| equality without identifier capability | n/a | rejected | parse | rejected | n/a | n/a | fail-closed: equality is meaningful only under identifier capability |
| refined trait + `refined-from` | yes | yes | yes | yes | yes | n/a | SrvIdentityId authoring/parsing + lowering witness |
| sum variants | gated | gated | yes | yes | yes | yes for witnessed target nodes | Name proves canonical parser/conformance/lowering; public authoring parity remains gated |
| maintained values | gated | gated | yes | yes | yes | yes for witnessed equality | IdentityType proves canonical parser/conformance/lowering; public authoring parity remains gated |
| aggregate-root sum classification + identity | gated | gated | yes | yes | yes | yes | SrvIdentity proves shared aggregate identity/state/representation plus variants; public authoring parity remains gated |
| entity classification | gated | gated | open | open | open | open | no equivalent canonical consumer witness has crossed the executable path yet |
| TicketTrayFilter optional nominal representation + explicit `represent` | yes | yes | yes | yes | yes | yes | `optional<X.Repr>` remains structural and `represent(state.X)` is explicit |
| implicit `flatten-single-field` | n/a | rejected | n/a | n/a | n/a | rejected | hypothesis closed: historical `string?` convenience does not authorize `Option<X.Repr> -> string?` |

## TicketTrayFilter closure

The experiment did not discover a missing flattening rule. It discovered that the canonical relation was already expressible without flattening once the semantic structure was made explicit:

```yaml
state:
  ProjectReference:
    optional: ProjectReference

representation:
  ProjectReference:
    type:
      optional: ProjectReference.Repr
    mapping:
      represent: state.ProjectReference
```

The responsibilities are therefore:

```text
VSIR
  -> structural optionality + nominal representation + explicit projection

Tooling
  -> preserve/validate/compose that structure

Ruleset
  -> realize type.optional and projection.represent for C#

historical human C# convenience
  -> evidence, not authority to invent flattening semantics
```

No `flatten-single-field` vocabulary was admitted.

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
discovery cannot describe a canonical conforming form
  -> public authoring/discovery gap; conformance remains canonical

parser cannot preserve a form present in the specification or concrete admitted corpus
  -> parser/conformance gap

lower cannot consume a conforming form
  -> lowering mechanism or target-knowledge gap

Ruleset lacks a deterministic realization
  -> Ruleset gap

canonical parser/lower support exists but new/update cannot construct the form
  -> keep public authoring gated; do not report the canonical artifact invalid
```

A successful parity experiment should move a row from left to right using the same semantic artifact. It should not make the row look complete by translating that artifact into a different grammar between authoring and lowering.

The TicketId/TicketCode corrections add an important evidence rule: an implementation restriction in the current validator or authoring surface is not stronger authority than admitted corpus evidence. When the two conflict, locate and repair the first stale layer instead of rewriting the corpus to match the accidental restriction.

The SrvIdentityId bulk-lowering observation adds a target-side version of the same rule: a convenience Framework composite must not collapse independent VSIR facts. Canonical lowering emits the primitive contracts justified by each fact and leaves convenience composition to human code or a later target optimization layer.

## Canonical-surface rule

There is one admitted VSIR 0.1 surface in this experiment and one public artifact parser at the boundary. Conformance assessment and target materialization must enter through `VsirParser`, including the active project semantic validation context when one exists.

The public authoring frontier is intentionally a separate, narrower capability surface. Its absence cannot invalidate a canonical artifact.

Pre-normalized experimental grammars are not a compatibility target and must not receive fallback parsing or lowering behavior. Because VSlices currently has one consumer, migration cost is preferred over carrying ambiguous compatibility indefinitely.
