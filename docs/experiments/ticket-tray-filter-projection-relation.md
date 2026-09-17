# TicketTrayFilter projection relation experiment

Status: **closed for this branch**. The original flattening hypothesis was rejected; no implicit `flatten-single-field` relation was admitted.

This branch continues the Ticket Support post-migration reconstruction using the repository rule:

> change only the first layer that can no longer justify the result, then rerun the real consumer before generalizing further.

The reproducible consumer evidence for the final review handoff is pinned to:

```text
atom-dev-serviu/access-management-product
commit: 7bae60d7e1af637cfcac6a802f867bc6979da444
branch at observation time: analysis/ticket-support-post-mortem
artifact: products/ticket-support-product/TicketSupport.Queries/Domain/Search/TicketTrayFilter.vsir
```

The branch name is useful for continuing analysis, but the commit above is the evidence reference for reconstructing this experiment.

The final companion authorities are also pinned for the release review:

```text
vslices/intermediate-representation#1
  merged language amendment
  merge commit: e46ddfcdf8f64f92a6313b421c0030f5a2fce6be

vslices/ruleset#4
  reviewed head: 8ee322ce94ff519843c4d409741dd9088823eeed
  CI #18: success
  merge commit: e2f5ea85283cc3a91e8c87107a06e0c23ccc1668
```

## Original question

The experiment began from an apparent non-isomorphism:

```text
semantic representation
  Option<X.Repr>

historical Query-facing C# materialization
  several string? coordinates
```

The initial research question was whether a target relation such as `flatten-single-field` was needed to justify that difference without allowing Tooling to infer semantic structure from an existing C# witness.

The discriminating rule was:

```text
if more than one realization is plausible
and no authority distinguishes them
-> stop rather than guess
```

## Final TicketTrayFilter result

The normalized consumer artifact expresses the relation directly and structurally:

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

The same pattern is used where applicable for `IncidentTypeReference`, `ResponsibleReference`, `Search`, `Dates`, and the scalar optional coordinates.

The final authority split is therefore:

```text
VSIR
  -> optionality is structural
  -> nominal representation type is preserved
  -> represent(...) is explicit semantic projection

Tooling
  -> parses/validates the structural semantic type
  -> preserves and composes the projection expression
  -> resolves nominal target symbols from target context
  -> does not reconstruct a missing relation from historical C#

Ruleset
  -> type.optional realizes optional<T> for C#
  -> projection.represent realizes represent(...) for C#

historical/human C# materialization
  -> evidence about one witness
  -> not authority to rewrite Option<X.Repr> as string?
```

`flatten-single-field` is therefore **rejected for this experiment**, not merely postponed. No current semantic or target authority justifies it.

A future corpus case may establish a different explicit projection relation, but it must start a new discriminating experiment rather than retroactively treating this rejected hypothesis as latent behavior.

## Project lowering crossed during the experiment

PR #6 recorded project/folder/batch lowering as future work. This branch took the narrowest useful slice into scope: complete .NET project lowering.

The existing subject-oriented CLI now accepts either an artifact or a project:

```text
vslices lower Identities.Domain
```

Resolution is fail-closed when an extensionless symbol is ambiguous:

```text
vslices lower Identities.Domain
  -> ambiguous when both forms exist

vslices lower Identities.Domain.vsir
  -> artifact

vslices lower Identities.Domain.csproj
  -> project
```

Project lowering prepares one coherent environment before visiting artifacts:

```text
project
  -> VSlicesProjectContext
  -> configured target
  -> installed Ruleset
  -> project semantic extension overlay
  -> enumerate project .vsir artifacts
  -> lower each artifact through the existing artifact mechanism
```

Unsupported artifacts remain explicit per-artifact boundaries; supported siblings are not abandoned. Artifact-specific overrides are not generalized into batch semantics without evidence.

Still outside this slice:

```text
--path / folder-scoped lowering
atomic batch materialization
multi-project / solution lowering
cross-project dependency orchestration
```

## Corpus witnesses crossed on the way

The experiment did not jump directly from `TicketTrayFilter` to a projection rule. The project run exposed intermediate real artifacts, each of which changed only the first unsupported layer it reached.

### SrvIdentityId

Established independent primitive target contracts, refined semantics, nominal semantic type resolution, equality-over-semantic-type, and stringify projection.

### Location

Established structural `sequence<T>`, derived state, explicit representation composition (`represent`, `select`, `map`), `resolve`, and one semantic `apply` whose direct/container target realizations may differ without creating two VSIR operations.

### StreetExtension

Established intrinsic refinement with named ordered outputs:

```yaml
- refine:
    intrinsic: split-first-rest
    value: input.Value
    as:
      Name: name
      Value: value
```

The language amendment is tracked in `vslices/intermediate-representation#1`; Ruleset owns the C# condition/output realizations.

### Name

Established canonical `sum` parsing/lowering and nested semantic expressions such as `concat-space` inside another condition argument.

### IdentityType

Established canonical `maintained` parsing/lowering and declared maintained values.

### SrvIdentity

Established aggregate-root sum parsing/lowering, shared state/representation/identity and variant-specific projections.

These witnesses also exposed an important review distinction:

```text
canonical parser/conformance/lowering coverage
  !=
public new/discovery/update authoring parity
```

`sum`, `maintained`, and aggregate-root sum are executable canonical forms in this branch, while full public authoring remains gated. Discovery must report a canonical instance as conforming rather than invalidating it against the narrower authoring vocabulary.

## Conformance environment

Project semantic extensions are environmental authority, not document content.

Therefore any command that claims VSIR conformance uses:

```text
VsirParser
+ owning project's VsirValidationContext
```

when `.vslices/extensions` exists.

`discovery` still does **not** evaluate lowerability. Ruleset availability, target selection and target context remain `lower` concerns.

## Ruleset extensibility result

The ownership boundary remains:

```text
Tooling
  -> constrained rule language
  -> structural validation
  -> exact binding contract
  -> allowed execution mechanisms

Ruleset
  -> semantic/target vocabulary expressible through those mechanisms
  -> deterministic target realizations
```

A new node name does not justify a Tooling code change when an admitted mechanism can already express it. Conversely, renderer/template presence never creates semantic authority.

The exact binding placeholder grammar shared by Tooling and Ruleset CI is:

```regex
[A-Za-z][A-Za-z0-9_-]*
```

## Nominal C# type resolution

`SrvIdentityId` exposed a target-context fact after its VSIR semantics became representable: semantic `Rut` is sufficient in VSIR, while C# needs the target symbol found through the related project/references.

```text
VSIR
  -> nominal semantic type: Rut

.NET target context
  -> target symbol/import resolution through Roslyn/MSBuild
```

This never becomes a semantic mapping such as `Rut -> string`.

## Specialized parser/lowerer coverage watchpoint

Product, sum and maintained paths remain specialized because their shape-specific grammar is not identical. The release review did, however, produce direct evidence that **common field-declaration obligations must be shared** rather than implemented as three subtly different contracts.

The common preflight now owns guarantees such as:

```text
non-scalar semantic keys fail closed
unknown keys in expanded field declarations fail closed
malformed from fails closed
from or mapping on an expanded field requires type
from + mapping is contradictory
```

What remains specialized is corpus-backed executable coverage, not alternate language meaning. For example, a projection form may already be part of canonical VSIR and exercised by product witnesses while no current sum witness has forced the sum lowerer to execute that same form yet. That gap is **witness-limited implementation coverage**.

Do not introduce a speculative universal grammar to remove every remaining duplication. When a new real artifact carries the same semantic form through another specialized path, establish identical specification meaning, add the cross-path regression, then extract the genuinely shared primitive. A specialized implementation gap must not be promoted into shape-specific VSIR semantics.

## Final success criterion

This experiment is complete because:

```text
TicketTrayFilter semantics are preserved without guessed flattening
structural optional and nominal representation types lower deterministically
explicit represent(...) reaches target realization
project lowering exposes unsupported siblings without hiding them
canonical conformance is distinct from public authorability
project semantic extensions participate consistently in conformance
Ruleset vocabulary remains constrained but name-extensible
unknown/underdetermined relations still stop explicitly
```

Future consumer artifacts should start new discriminating experiments rather than growing this one indefinitely.
