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

The working hypothesis is now:

> VSIR should remain closed to implicit semantics, but open to explicitly declared semantic extensions.

This PR will explore only the smallest demonstrated case: extension of `normalize`. It will not generalize the mechanism to every VSIR concept until later consumer evidence requires it.

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

review of flexibility consequence
  -> current boundary requires a VSlices.Vsir release for every new normalize semantic
  -> this is too restrictive for custom Rulesets and domain-specific operations

scope change
  -> preserve rejection of undeclared semantics
  -> introduce an explicit semantic-extension path
  -> keep semantic declaration separate from target renderer authority
```

## Scope change

The original PR question was:

> Can target-specific Ruleset knowledge cause the CLI to accept a `normalize` intrinsic that VSIR does not recognize?

The answer remains **no**, and that invariant is retained.

The PR now continues one boundary further:

> Can a custom Ruleset carry an explicit declaration that makes a previously unknown `normalize` operation semantically admissible, without making the existence of a renderer sufficient proof of semantic validity?

This means the experiment is no longer only a negative control. It becomes a minimal semantic-extension experiment driven by the exact failure observed at `VSIR221`.

## Intended authority split

The experiment must preserve three distinct questions:

```text
1. Is this semantic operation known or explicitly declared?
        -> VSIR semantic validity

2. Is the operation structurally valid in this position?
        -> normalize contract / validation

3. Can this target materialize it?
        -> Ruleset target realization
```

A target renderer alone must not answer question 1.

Conceptually:

```text
core semantic
  trim
    -> known by VSlices.Vsir

explicit extension semantic
  custom.normalize-x
    -> declared by the active custom Ruleset as a normalize extension
    -> accepted structurally by VSIR without VSlices.Vsir knowing its domain meaning

undeclared semantic
  typo-or-magic
    -> VSIR221
```

The declaration and the renderer may live in the same Ruleset distribution, but they represent different authority:

```text
semantic extension declaration
  !=
target realization
```

## Experiment sequence

### A. Undeclared extension remains rejected

```text
unknown normalize intrinsic
+ matching target renderer
-> VSIR221
-> no C# materialization
```

This preserves the authority boundary discovered by the original negative-control experiment.

### B. Declared extension without target realization

```text
explicitly declared normalize extension
+ no C# renderer
-> semantic validation succeeds
-> C# lowering reaches target capability boundary
-> CSL031
```

This is the key discriminating experiment. It proves that semantic validity and target capability are separate.

### C. Declared extension with target realization

```text
explicitly declared normalize extension
+ matching C# renderer
-> semantic validation succeeds
-> target lowering succeeds
-> deterministic C# materialization
```

### D. Remove the declaration again

The same renderer must become insufficient once the semantic declaration is removed:

```text
renderer still present
+ declaration absent
-> VSIR221
```

This guards against accidentally collapsing extension registration into renderer lookup.

## Minimal extension contract under investigation

The first implementation should describe only what VSIR needs to know to admit a custom operation as a `normalize` semantic.

At minimum, the declaration needs an identity and its semantic category. A candidate shape may resemble:

```yaml
semantic-extensions:
  custom.normalize-x:
    kind: normalize
```

The exact persisted syntax is not considered settled by this document; implementation should choose the smallest representation that fits the existing Ruleset manifest/snapshot model and can be validated deterministically.

Properties such as purity, determinism, idempotence, input/output contracts, or cross-target realizations are deliberately deferred until a real lowering problem requires them.

## Invariants

1. Unknown and undeclared normalize semantics still fail closed.
2. A target renderer never grants semantic validity by itself.
3. A semantic extension must be explicitly declared.
4. Extension identity must survive independently of a particular target renderer.
5. Core semantics such as `trim` remain valid without external extension declaration.
6. Declared extensions must still satisfy the structural laws of the `normalize` position.
7. Missing target realization for a valid extension must remain distinguishable from unknown semantics (`CSL031` vs `VSIR221`).
8. This PR extends only `normalize`; no generic extension framework is assumed yet.

## Architectural boundary under test

```text
VSlices.Vsir
  core normalize vocabulary
  normalize structural contract
  extension-aware semantic validation

active Ruleset semantic declarations
  explicit custom normalize identities
  target-neutral admission evidence

VSlices.Vsir.CSharp / lowering mechanism
  ordered normalization dataflow

Ruleset target rules
  target-specific renderer for the semantic identity
```

The important distinction is that the active Ruleset may carry both declaration and realization, but Tooling must consume them through separate semantic and target-capability boundaries.

## Acceptance criteria

- preserve the real CLI negative-control path that produces `VSIR221` for an undeclared normalize semantic;
- provide one explicit normalize extension declaration through an isolated/custom Ruleset fixture;
- show that the declared extension passes semantic validation without adding the intrinsic to the hard-coded `VSlices.Vsir` core vocabulary;
- show `CSL031` when that valid extension lacks a C# realization;
- add a matching C# realization and show deterministic CLI lowering succeeds;
- remove/omit the declaration while leaving the renderer and show `VSIR221` returns;
- exercise the behavior through the real CLI command path, not only parser/lowerer unit tests;
- keep production/core Ruleset knowledge free of synthetic experiment-only semantics.

## Non-goals

- define a universal plugin system for every VSIR node;
- allow renderers to implicitly create semantics;
- add a real product-specific normalize semantic without consumer evidence;
- add purity/determinism/idempotence metadata before lowering evidence needs it;
- generalize extension declarations to `ensure`, equality, invariants, features, or other kinds in this PR;
- solve arbitrary executable extensions or interpretation;
- weaken fail-closed behavior for malformed or undeclared semantic input.

## Evidence source

The current consumer corpus still demonstrates `trim` as the concrete normalization intrinsic. The synthetic boundary probe is intentionally not a business semantic; it exists to isolate the authority mechanism.

Richer Ticket Support artifacts such as `EmailAddress` expose later validation/equality boundaries and should remain separate experiments. If those later cases need the same extension mechanism, that will be evidence for generalization rather than an assumption made here.
