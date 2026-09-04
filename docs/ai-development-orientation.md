# AI development orientation

This is the shortest reconstructible path for an AI-assisted session working on VSlices Tooling.

Read current repository evidence before relying on chat history. Suggested order:

1. `README.md`
2. `AGENTS.md`
3. `docs/releases/v0.2.0-preview.md`
4. `docs/context.vslices-tooling.md`
5. `docs/rulesets.md`
6. `docs/configuration.md`
7. implementation/tests for the concrete case.

## Cross-repository authority map

```text
consumer project
  = concrete evidence

.vsir
  = semantics

.vsir.cs
  = editable human witness

config.yaml
  = project operating policy

lineage
  = deterministic ancestry evidence

vslices/ruleset
  = revisable target-lowering knowledge

vslices/tooling
  = mechanisms, coordination, guarantees, CLI and target adapters

target-native tooling
  = target-owned facts
```

The core rule is:

> Lowering may complete implementation detail. Lowering must not complete missing semantics.

## Internal Tooling flow

Command handlers are adapters:

```text
command
  -> operation / coordinator
  -> project / ruleset / lineage infrastructure
  -> VSIR / target mechanism
```

`TranspilationOperation` is reusable deterministic projection. `RebaseOperation` is reusable deterministic three-way rebase. `LoweringCoordinator` owns the policy choosing between transpile, lineage establishment, rebase and explicit stop.

Do not make extracted `lower` behavior public merely because it has a class. Do not reuse behavior by calling another command handler.

`VSlicesProjectContext` is the canonical detected project representation. Reuse it instead of deriving project/config/ruleset/lineage roots independently.

## Semantic conservation

Known semantic mappings are fail-closed: unknown keys at root, construction, construction step, ensure, condition, failure and equality produce explicit diagnostics. Do not apply fixed-key rejection to variable-key semantic data maps such as `state`, `representation`, and `construction.input`.

Traits are unordered capabilities. Duplicates and unknown traits fail explicitly. Current subset requires `transform`; `identifier` separately requires equality.

## Ruleset update contract

```text
materialize
-> prepare selected target
-> validate through real target loader
-> atomic swap with rollback
```

Never replace the current ruleset and discover invalidity afterwards.

For GitHub repository sources, `ruleset.ref` may represent branch, tag or commit/direct archive reference. Non-Git sources do not silently reinterpret it as a branch.

## Lineage contract

Bootstrap is non-destructive:

```text
existing conventional human witness
+ no lineage
+ bootstrap authority
-> compute deterministic current projection
-> store deterministic projection
-> preserve human bytes
-> return success
```

Only a later semantic change performs three-way rebase from stored baseline + human witness + next deterministic projection.

`.vslices/lineage` is intended to be version-controlled so another machine/CI can reconstruct automatic rebase from repository state. It is operational evidence, not semantic authority.

## Consumer-project procedure

For each real `.vsir`:

```text
1. establish semantics from consumer evidence
2. ask whether VSIR represents them faithfully
3. ask whether local ruleset carries required lowering knowledge
4. ask whether target-native tooling owns remaining target facts
5. classify any remaining gap
6. change only the owning repository/layer
7. validate against the consumer when possible
```

Gap classes:

```text
semantic representation
parsing/validation
ruleset knowledge
target context
lowering mechanism
rebase/provenance
presentation
no gap
```

Do not proceed to a new sample while a previous experimental boundary is still architecturally unstable unless that instability is explicitly accepted.

## Validation expectations

Prefer evidence at the lowest faithful level and retain real CLI smoke flows for orchestration. Current regression coverage includes project discovery, ruleset update, byte-for-byte non-destructive bootstrap and subsequent automatic rebase.

An in-process Tooling test reference exposed a case-insensitive assembly-name collision (`vslices` executable vs Framework `VSlices`). The dedicated Tooling tests therefore exercise orchestration through the built CLI process instead of introducing a new assembly solely to satisfy tests. Treat the assembly identity as an open architectural finding if Tooling later needs to become a reusable in-process library.

## Continuity

When command semantics, authority, project context, lineage or ruleset behavior changes materially, update the closest repository document in the same change. Repository artifacts must remain sufficient to reconstruct accepted behavior without conversation history.
