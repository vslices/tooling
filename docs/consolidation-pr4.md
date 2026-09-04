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
5. GitHub `ruleset.ref` represents branch/tag/commit candidates; non-Git sources reject ref semantics.
6. Known nested semantic mappings fail closed on unknown keys.
7. Traits are unordered capabilities; duplicates invalid.
8. Lineage bootstrap is non-destructive and records deterministic projection, not human witness.
9. `.vslices/lineage` is versionable operational evidence by default.
10. No new assembly, DI system, plugin system or VSIR concept was introduced merely to make the structure symmetrical.

## Regression evidence

Semantic tests cover nested unknown keys, trait ordering/duplicates/unknowns, identifier/equality requirements and missing ruleset equality knowledge.

Tooling tests cover project boundaries, new-lineage creation, exact deterministic lineage establishment, byte-for-byte human preservation during bootstrap, subsequent automatic rebase, source-override ancestry failure, valid ruleset update and invalid-candidate preservation.

CI smoke tests cover real `update --ruleset` and lineage bootstrap followed by semantic change/rebase using local fixtures.

## Open questions

- target-context namespace derivation and path exclusions;
- future selector of available bootstrap conventions;
- whether convention definitions partly belong in ruleset;
- aggregate `vslices update` ordering/partial failure;
- CLI styling for lowering commands;
- whether the case-only assembly naming `vslices`/`VSlices` should change if Tooling ever needs in-process reuse.

## New finding during consolidation

A normal test-project reference to `VSlices.Tooling` caused .NET test-host assembly resolution to collide with Framework `VSlices` because assembly identity matching is case-insensitive in this scenario. This did not invalidate the CLI execution model, but it did invalidate the assumption that Tooling can currently be treated as an ordinary reusable in-process assembly. Regression tests therefore target the executable boundary rather than adding a test-motivated assembly split.
