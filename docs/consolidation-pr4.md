# PR #4 consolidation record

This document records the architecture consolidated after the Ticket Support `TicketId` experiment, so the result can be reviewed independently of chat history.

## Responsibility tree

```text
Commands
  VsirCommands        CLI adapter
  RulesetCommands     initialization policy
  UpdateCommands      explicit update surfaces

Lowering
  TranspilationOperation  deterministic VSIR projection
  RebaseOperation         deterministic three-way rebase
  LoweringCoordinator     least-powerful-sufficient policy
  Lineage/*               baseline persistence/bootstrap authorization

Project
  VSlicesProjectContext   canonical project discovery
  ProjectConfiguration    operating policy persistence
  ArtifactDiscoveryPolicy discovery exclusions

Rulesets
  RulesetSourceMaterializer source acquisition/ref resolution
  RulesetSnapshotInstaller prepare/validate/atomic install
  RulesetUpdater           update policy

Updates
  SelfUpdater              executable update

Presentation
  TerminalOutput / CliVersion
```

## Decisions

1. Command handlers remain adapters; extracted internal behavior does not become a command automatically.
2. Project roots are represented once by `VSlicesProjectContext`.
3. Ruleset init/update share materialization and installation mechanisms.
4. Ruleset candidates are fully target-loader validated before swap.
5. GitHub `ruleset.ref` represents branch/tag/commit candidates; non-Git sources reject ref semantics. Re-initializing with a non-Git source clears a stale Git ref rather than persisting an invalid source/ref combination.
6. Known nested semantic mappings fail closed on unknown keys, and semantic sequences reject children of the wrong YAML node kind instead of silently filtering them out.
7. Traits are unordered capabilities; duplicates invalid.
8. Lineage bootstrap is non-destructive and records deterministic projection, not human witness.
9. `.vslices/lineage` is versionable operational evidence by default.
10. Ruleset path containment follows platform filesystem casing rules: case-insensitive on Windows and ordinal/case-sensitive on other supported platforms.
11. No new assembly, DI system, plugin system or VSIR concept was introduced merely to make the structure symmetrical.

## Regression evidence

Semantic tests cover nested unknown keys, malformed semantic sequence entries, trait ordering/duplicates/unknowns, identifier/equality requirements and missing ruleset equality knowledge.

Tooling tests cover project boundaries, new-lineage creation, exact deterministic lineage establishment, byte-for-byte human preservation during bootstrap, subsequent automatic rebase, source-override ancestry failure, valid ruleset update, invalid-candidate preservation, and `init --force` transitions from Git-backed configuration to a local ruleset source without retaining a stale `ruleset.ref`.

Ruleset loader tests also cover a case-distinct sibling path on case-sensitive platforms so validate-before-swap cannot treat `/tmp/Ruleset/...` as belonging to `/tmp/ruleset/...`.

CI smoke tests cover real `update --ruleset` and lineage bootstrap followed by semantic change/rebase using local fixtures.

## Open questions

- target-context namespace derivation and path exclusions;
- future selector of available bootstrap conventions;
- whether convention definitions partly belong in ruleset;
- aggregate `vslices update` ordering/partial failure;
- CLI styling for lowering commands;
- whether the case-only assembly naming `vslices`/`VSlices` should change if Tooling ever needs in-process reuse;
- whether `LoweringCoordinator` should eventually become presentation-free application orchestration. It currently still persists results/lineage and emits CLI diagnostics/presentation; this is acceptable for PR #4 but should not grow accidentally into a general presentation layer;
- whether `RulesetCommands.Init` eventually deserves an `InitializationOperation`. Its dangerous acquisition/install duplication is already removed, so extraction should wait for new evidence of policy growth.

## `--from` provenance precision

`--from <previous-vsir>` is a recovery mechanism, not guaranteed historical provenance.

It reconstructs the previous deterministic projection by lowering that older VSIR with the lowering machinery, ruleset and target conventions available **now**. Therefore:

```text
T_current(V0)
```

is not guaranteed to equal:

```text
T_historical(V0)
```

when Tooling, Ruleset knowledge or target conventions have changed since `V0` was current.

Recorded `.vslices/lineage` baselines reduce dependence on this reconstruction because they preserve the actual deterministic baseline used by normal lowering. PR #4 does not add Git-history reconstruction, ruleset-version migration or a provenance graph.

## Test-boundary exception

`VSlices.Tooling.Tests` primarily exercises Tooling through the built CLI process. There is one deliberate exception: `RulesetSourceMaterializer.cs` is linked into the test project so the pure GitHub archive-candidate calculation can be tested without referencing the executable assembly.

This is not a claim that the suite is literally 100% black-box. The product orchestration regressions are black-box; the linked-source test is a narrow unit-level exception retained to avoid an assembly split motivated only by tests.

## New finding during consolidation

A normal test-project reference to `VSlices.Tooling` caused .NET test-host assembly resolution to collide with Framework `VSlices` because assembly identity matching is case-insensitive in this scenario. This did not invalidate the CLI execution model, but it did invalidate the assumption that Tooling can currently be treated as an ordinary reusable in-process assembly. Regression tests therefore target the executable boundary rather than adding a test-motivated assembly split.
