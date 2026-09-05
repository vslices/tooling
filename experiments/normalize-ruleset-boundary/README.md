# Normalize semantic extension / Ruleset boundary experiment

## Summary

This experiment began from the TicketCode boundary:

```text
VSIR
  -> recognizes normalization semantics

Tooling lowering mechanism
  -> preserves ordered normalization dataflow

Ruleset
  -> realizes already-recognized semantics for a target
```

An intentionally unknown normalize intrinsic (`normalize-boundary-probe`) first established the fail-closed negative control:

```text
undeclared normalize semantic
-> VSIR221
```

The experiment then asked whether a project can explicitly admit a new normalization semantic without waiting for a new `VSlices.Vsir` release, while still preventing target renderers from inventing vocabulary implicitly.

The resulting rule is:

> VSIR remains closed to implicit semantics and open to explicitly declared project semantic extensions.

The experiment remains deliberately limited to `normalize`.

## Review-driven lifecycle refinement

The first extension-catalog shape placed project-owned catalogs below `.vslices/ruleset`. Review exposed a lifecycle contradiction: `.vslices/ruleset` is the replaceable snapshot owned by `init --force` and `update --ruleset`, so project semantics stored below it could be destroyed by a normal Ruleset refresh.

The ownership model is now explicit:

```text
.vslices/ruleset/
  source-owned installed lowering knowledge
  replaceable by Ruleset lifecycle

.vslices/extensions/
  project-owned semantic extension overlay
  preserved by Ruleset lifecycle
```

Project extensions therefore no longer appear in the Ruleset manifest.

Their own project manifest references catalogs:

```yaml
# .vslices/extensions/manifest.yaml
version: 0.1
catalogs:
  - ticketing.yaml
```

A referenced catalog keeps one semantic identity and optional target realizations together:

```yaml
# .vslices/extensions/ticketing.yaml
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

The two blocks still have different authority:

```text
semantic.kind
  -> semantic admission

targets.csharp
  -> realization of that already-valid semantic in C#
```

Physical co-location is an authoring convenience, not an authority collapse.

## One loaded project-extension model

The earlier implementation made semantic admission and C# lowering parse the same extension YAML independently. The refined implementation loads `.vslices/extensions` once into a project-owned model that supplies:

```text
ProjectExtensionCatalogs
  -> referenced catalog graph
  -> path containment
  -> structural validation
  -> semantic declarations
  -> C# realization declarations

VsirValidationContext
  <- semantic declaration view

CSharpLoweringRuleSet additional rules
  <- C# realization view
```

This is not a universal plugin abstraction. It is one representation for one concept that already had multiple consumers.

The installed Ruleset remains independently loaded and validated. Its manifest is no longer allowed to declare project `extensions`.

## Document versus validation environment

Project extension authority is no longer attached to `DomainTypeVsir`.

```text
DomainTypeVsir
  = what the .vsir document says

VsirValidationContext
  = vocabulary explicitly admitted by the current project environment
```

The same document can therefore be evaluated under different validation contexts without pretending that environmental authority is part of the parsed semantic document.

## Fail-closed structural conservation

The extension model and C# Ruleset loader no longer use type-filtering iteration that can silently discard malformed YAML nodes.

The current rule is:

> if a semantic/configuration node is present with the wrong structural type, it produces a diagnostic; it never disappears from processing.

This applies to catalog references, extension entries, semantic metadata, target realization mappings, Ruleset rule-file references and Ruleset rule entries.

## Experiment sequence

### A. Undeclared semantic

```text
unknown normalize intrinsic
-> VSIR221
```

### B. Declared semantic without C# realization

```text
.vslices/extensions manifest references catalog
+ semantic.kind: normalize
+ no targets.csharp realization
-> semantic validation succeeds
-> CSL031
```

### C. Declared semantic with co-located C# realization

```text
same project extension entry
+ semantic.kind: normalize
+ targets.csharp renderer
-> semantic validation succeeds
-> deterministic C# lowering succeeds
```

### D. Renderer without declaration

```text
standalone renderer exists in installed Ruleset
+ no project semantic declaration
-> VSIR221
```

The renderer still cannot grant semantic validity.

## Acceptance evidence — TicketCode vs Risk

The original discriminating pair is complete.

`TicketCode` exercises:

```text
normalize: trim
-> ensure: non-empty
```

`Risk` exercises:

```text
normalize: trim
-> ensure: not-whitespace
```

The real Risk lowering produced the relevant shape:

```csharp
VSlices.Arrows.Req<Input, Risk>.Ensure(
    (Input input) => !string.IsNullOrWhiteSpace(input.Value.Trim()),
    Fail: "Debes especificar el riesgo")
* Instance;

private static Risk Instance(Input input) =>
    new(input.Value.Trim());
```

This establishes that the generic lowering mechanism preserves:

```text
input.Value
  -> Trim()
  -> not-whitespace validation
  -> construct from normalized value
```

`not-whitespace` is core VSIR semantics justified by real consumer evidence, with its C# realization in the installed Ruleset. It is not a project extension.

The repeated `.Trim()` remains a future watchpoint for single-evaluation/purity/determinism; it is acceptable for the current core `trim` intrinsic and is not a failure of this experiment.

## Lifecycle acceptance criteria

The refinement adds lifecycle requirements to A/B/C/D:

- `.vslices/extensions` survives `vslices update --ruleset`;
- `.vslices/extensions` survives `vslices init --force`;
- an installed Ruleset manifest cannot claim ownership of project extensions;
- a missing referenced project catalog fails closed;
- wrong YAML node types fail closed rather than being filtered away;
- a target realization without semantic metadata is invalid;
- the VSIR document remains independent of its validation context.

## Review invariants

1. Unknown and undeclared normalize semantics fail closed.
2. A target renderer never grants semantic validity by itself.
3. Custom semantic validity requires an explicitly referenced project declaration.
4. Extension identity remains distinct from a target realization.
5. Core semantics such as `trim` remain valid without project extension declaration.
6. Declared extensions still obey the structural laws of `normalize`.
7. Missing target realization remains distinguishable from unknown semantics (`CSL031` vs `VSIR221`).
8. Co-location does not collapse semantic and target authority.
9. Project extensions live under `.vslices/extensions`, not the replaceable Ruleset snapshot.
10. Ruleset lifecycle never destroys the project extension overlay.
11. Malformed semantic/configuration nodes never disappear silently.
12. This PR implements only project `normalize` admission and C# realization.
13. Synthetic experiment semantics do not leak into the production/core Ruleset.
14. Ordered normalize -> ensure composition validates the normalized value, not the raw input.

## Downstream project-scoped lowering contract

The extension model also exposes a requirement for future lowering across several VSIR artifacts, but this PR does not implement that CLI feature.

```text
Lower(Target, Scope?)

Target =
    VsirArtifact
  | Project

Scope =
    ProjectRelativePath
```

`Scope` is valid only for a project target. A project-relative path narrows the selected VSIR set; it does not create a third context kind.

A future project-scoped operation should:

1. establish project context once;
2. load one coherent Ruleset + project extension overlay + target context for the selected operation;
3. preserve each artifact's local semantic ordering;
4. treat scope as selection only;
5. commit materialization/lineage atomically rather than leaving a partial result.

Illustrative future syntax:

```text
vslices lower Risk.vsir
vslices lower Identities.Domain.csproj
vslices lower Identities.Domain --path Aggregates
```

Artifact discovery, project-name resolution, recursive selection, `--path`, `--ir`, `--proj`, aggregate diagnostics and batch staging remain outside this PR.

## Remaining impact review

The ownership/lifecycle question is now resolved by the project overlay. Remaining later questions include:

1. target selection when an extension carries several realizations;
2. collision policy between installed Ruleset rules and project realization rules;
3. provenance/lineage for semantic admission versus realization;
4. rebase consequences when only realization changes versus semantic declaration;
5. evidence required for behavioral compatibility across multiple targets;
6. whether future real extension kinds reproduce this pattern strongly enough to justify a broader abstraction;
7. whether future lowering evidence requires purity, determinism, idempotence, input/output contracts or single-evaluation guarantees;
8. project-scoped lowering selection, diagnostics and atomicity.

## Non-goals

- define a universal plugin system;
- allow renderers to create semantics implicitly;
- make source Rulesets own project extension catalogs;
- generalize extensions to ensure/equality/invariants/features in this PR;
- claim multi-target behavioral equivalence;
- add purity/determinism/idempotence metadata without evidence;
- implement project/folder/batch lowering in this PR;
- weaken fail-closed semantic conservation.
