# Normalize semantic extension / Ruleset boundary experiment

## Summary

This experiment began as a negative control for the boundary established by the TicketCode work:

```text
VSIR
  -> recognizes normalization semantics

Tooling lowering mechanism
  -> preserves ordered normalization dataflow

Ruleset
  -> realizes already-recognized semantics for a target
```

The first real consumer probe confirmed the current boundary: an intentionally unknown normalize intrinsic (`normalize-boundary-probe`) is rejected as `VSIR221` before target lowering can make it executable.

That observation is correct for semantic integrity, but it exposed a second question that changes the scope of the experiment:

> Can a custom Ruleset explicitly declare a new normalization semantic so the CLI can use it without requiring a new `VSlices.Vsir` release, while still preventing target renderers from implicitly inventing semantic vocabulary?

The working hypothesis remains:

> VSIR should remain closed to implicit semantics, but open to explicitly declared semantic extensions.

The experiment is still deliberately limited to `normalize`.

## Observation chain

```text
TicketCode.vsir
  -> normalize: trim
  -> VSIR recognizes trim
  -> Tooling preserves ordered normalization dataflow
  -> missing target rule produces CSL031
  -> vslices/ruleset supplies intrinsic.trim
  -> deterministic C# lowering succeeds

normalize boundary probe
  -> intrinsic: normalize-boundary-probe
  -> VSIR221
  -> semantic validity stops before target realization

case B
  -> semantic extension is explicitly declared
  -> VSIR221 disappears
  -> missing C# realization becomes CSL031

configuration review
  -> declaring semantic identity in the manifest and renderer elsewhere is valid but too ceremonial
  -> the common case is one project with one primary target
  -> multi-target projects are more likely during migration, compatibility, or interoperability work

current refinement
  -> manifest references extension catalogs
  -> one extension entry owns semantic identity
  -> the same entry may optionally carry one or more target realizations
  -> semantic admission and renderer authority remain logically separate even when authored together
```

## Intended authority split

The experiment preserves three independent questions:

```text
1. Is this semantic operation known or explicitly declared?
        -> VSIR semantic validity

2. Is the operation structurally valid in this position?
        -> normalize contract / validation

3. Can this target materialize it?
        -> Ruleset target realization
```

A renderer alone must not answer question 1.

## Current extension shape under test

The manifest now treats extension files the same way it already treats target rule files: as references maintained by the project Ruleset.

```yaml
extensions:
  - extensions/ticketing.yaml

targets:
  csharp:
    rules:
      - csharp/intrinsics.yaml
```

An extension catalog keeps semantic identity and its target realizations together:

```yaml
extensions:
  - node: intrinsic.normalize-boundary-probe
    semantic:
      kind: normalize
    targets:
      csharp:
        mode: deterministic
        renderer: expression
        template: "{value}.Trim()"
```

The `semantic` block grants semantic admission. The `targets.csharp` block grants C# realization. They are co-located for ergonomics, but they remain different authorities inside Tooling.

A declared semantic without a C# realization is therefore valid and should reach `CSL031`:

```yaml
extensions:
  - node: intrinsic.normalize-boundary-probe
    semantic:
      kind: normalize
```

A renderer placed only in `targets.csharp.rules` without any semantic declaration must still leave the VSIR rejected as `VSIR221`.

## Why co-locate semantic identity and realizations?

The common configuration is expected to have one primary language target. Requiring a semantic catalog plus a separate per-language declaration for every custom operation would optimize the common path for a relatively rare multi-target case.

Keeping realizations under one extension instead makes the simple case small while still scaling naturally when a second target is genuinely needed:

```yaml
extensions:
  - node: intrinsic.normalize-rut
    semantic:
      kind: normalize
    targets:
      csharp:
        mode: deterministic
        renderer: expression
        template: "Rut.Normalize({value})"
      typescript:
        mode: deterministic
        renderer: expression
        template: "normalizeRut({value})"
```

That shape is particularly useful for language migrations, compatibility periods, and service interoperability because both realizations remain visibly attached to one semantic identity.

This PR only executes the C# branch of that model. The multi-target shape is recorded as the next exploration boundary, not claimed as fully implemented.

## Experiment sequence

### A. Undeclared semantic

```text
unknown normalize intrinsic
-> VSIR221
```

### B. Declared semantic without C# realization

```text
referenced extension catalog
+ semantic.kind: normalize
+ no targets.csharp realization
-> semantic validation succeeds
-> CSL031
```

### C. Declared semantic with co-located C# realization

```text
referenced extension catalog
+ semantic.kind: normalize
+ targets.csharp renderer
-> semantic validation succeeds
-> deterministic C# lowering succeeds
```

### D. Renderer without declaration

```text
renderer exists in targets.csharp.rules
+ no referenced semantic extension declaration
-> VSIR221
```

This final case keeps the original authority boundary intact.

## Invariants

1. Unknown and undeclared normalize semantics still fail closed.
2. A target renderer never grants semantic validity by itself.
3. A semantic extension must be explicitly declared from a referenced extension catalog.
4. Extension identity survives independently of a particular target realization.
5. Core semantics such as `trim` remain valid without external extension declaration.
6. Declared extensions still satisfy the structural laws of the `normalize` position.
7. Missing C# realization for a valid extension remains distinguishable from unknown semantics (`CSL031` vs `VSIR221`).
8. Co-location is an authoring convenience, not a collapse of semantic and target authority.
9. This PR executes only the `normalize` + C# path; no generic plugin model is assumed.

## Architectural boundary under test

```text
Ruleset manifest
  -> references extension catalogs

extension catalog entry
  -> semantic identity / kind
  -> optional target realizations

VSlices.Vsir
  -> core normalize vocabulary
  -> normalize structural contract
  -> extension-aware semantic validation

VSlices.Vsir.CSharp
  -> ordered normalization dataflow
  -> consumes only the C# realization for an already-valid semantic identity
```

## Acceptance criteria

- preserve `VSIR221` for an undeclared normalize semantic;
- load semantic extension identities through referenced catalog files;
- show a declared normalize extension reaches `CSL031` without a C# realization;
- show the same extension lowers successfully when a C# realization is co-located under `targets.csharp`;
- show a standalone C# renderer still cannot create semantic validity;
- exercise all four states through the real CLI command path;
- keep synthetic experiment knowledge outside the production/core Ruleset surface.

## Downstream project-scoped lowering contract

The current CLI experiment lowers one VSIR artifact at a time, but the extension model exposes a requirement for any future project-scoped lowering operation.

The minimal conceptual shape to carry forward is:

```text
Lower(Target, Scope?)

Target =
    VsirArtifact
  | Project

Scope =
    ProjectRelativePath
```

`Scope` is valid only when `Target` is a project. A project-relative path narrows the set of VSIR artifacts selected for lowering; it does not become an independent semantic or target context.

This implies several downstream laws:

1. A project-scoped lowering operation establishes project context once and applies one coherent Ruleset / extension-catalog / target context to every selected artifact.
2. Batch selection must not alter per-artifact semantic ordering. For example, `normalize -> ensure` remains local to the artifact and must validate the normalized value.
3. Project-relative scope is a selection concern, not a third lowering target alongside VSIR artifact and project.
4. A future batch operation should be atomic with respect to materialization and lineage: either all selected artifacts are valid/lowerable and their materializations are committed, or no partial batch should be written.
5. Artifact discovery, project-name resolution, recursive selection, `--path`, `--ir`, `--proj`, multi-path selection, aggregate diagnostics, and staging mechanics are separate CLI concerns and are not implemented by this PR.

Illustrative future command shapes are therefore intentionally non-normative here:

```text
vslices lower Risk.vsir
vslices lower Identities.Domain.csproj
vslices lower Identities.Domain --path Aggregates
```

The important architectural point for this PR is narrower: extension catalogs are resolved from project context rather than per-artifact renderer lookup, so future project-scoped lowering must preserve one coherent semantic authority across the selected set.

## Pending impact review — next exploration step

This refinement changes more than the syntax of the current experiment. Before generalizing semantic extensions beyond `normalize`, the next exploration should explicitly review how the extension-catalog model changes assumptions already introduced by PR #4, PR #5, and this PR.

Questions to carry forward:

1. **Ruleset manifest and schema** — should extension catalog references become a permanent first-class Ruleset surface, and how should schema/versioning evolve?
2. **Ruleset update/install** — how are project-owned extension catalogs preserved when `vslices update` refreshes upstream Ruleset knowledge?
3. **Target resolution** — what happens when one extension declares several target realizations and the project selects or changes its target?
4. **Duplicate/collision rules** — how should collisions between core rules, project target rules, and extension-catalog realizations be diagnosed?
5. **Provenance and lineage** — which source should be recorded as authority for semantic admission versus the selected target realization?
6. **Rebase/materialization** — does changing only a target realization affect semantic lineage differently from changing the semantic declaration itself?
7. **Multi-target compatibility** — when two realizations live under one semantic identity, what evidence is needed before Tooling can claim that they are behaviorally compatible rather than merely present?
8. **Existing core intrinsics** — should core operations such as `trim` remain split between VSIR vocabulary and target Ruleset knowledge, or does the extension shape reveal a more uniform representation worth exploring later?
9. **Future extension kinds** — only after real `ensure`, equality, invariant, feature, or other consumer boundaries reproduce this pattern should a generic extension abstraction be considered.
10. **Semantic properties** — purity, determinism, idempotence, input/output contracts, and single-evaluation requirements remain deferred until a concrete lowering case demands them.
11. **Project-scoped lowering** — when lowering several artifacts, which project-context elements are established once, which diagnostics stay per-artifact, and what atomicity guarantees are required for materialization and lineage?

The next experiment after closing the current C# path should therefore be an impact audit, not an immediate generalization.

## Non-goals

- define a universal plugin system for every VSIR node;
- allow renderers to implicitly create semantics;
- add a real product-specific normalize semantic without consumer evidence;
- generalize extension declarations to `ensure`, equality, invariants, features, or other kinds in this PR;
- claim multi-target behavioral equivalence merely because multiple renderers can be declared;
- add purity/determinism/idempotence metadata before lowering evidence needs it;
- implement project/folder/batch lowering, project-name resolution, `--path`, `--ir`, or `--proj` in this PR;
- weaken fail-closed behavior for malformed or undeclared semantic input.

## Evidence source

The current consumer corpus still demonstrates `trim` as the concrete normalization intrinsic. `normalize-boundary-probe` remains intentionally synthetic and exists only to isolate the authority and extension mechanism.

The Ticket Support `ActionDescription.vsir` probe supplies the executable consumer boundary. Richer artifacts such as `EmailAddress` should remain later experiments unless they independently reproduce the same extension pressure.
