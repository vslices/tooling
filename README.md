# VSlices Tooling

VSlices Tooling is the executable tooling surface of the VSlices suite. The CLI is named `vslices`.

The current development line is `v0.2.0-preview`. Its governing rule remains:

> Lowering may complete implementation detail. Lowering must not complete missing semantics.

The implemented command surface is:

```text
vslices init
vslices transpile
vslices rebase
vslices lower
vslices update --self
vslices update --ruleset
vslices --version
vslices -v
```

Plain `vslices update` and combined `--self --ruleset` are intentionally undefined until aggregate ordering and partial-failure semantics are justified by evidence.

## Authority boundaries

```text
consumer project
  = concrete software/domain evidence

.vsir
  = semantic source

.vsir.cs
  = human-editable witness constrained by VSIR

.vslices/config.yaml
  = project operating policy

.vslices/ruleset
  = local lowering-knowledge snapshot

.vslices/lineage
  = operational deterministic ancestry evidence

vslices/ruleset
  = official revisable lowering knowledge

vslices/tooling
  = mechanisms, coordination, safety guarantees, CLI and target adapters

target-native tooling
  = target facts already owned by the target ecosystem
```

A missing lowering rule is a stop condition, never permission to guess.

## Internal responsibility tree

Command handlers are CLI adapters, not orchestration containers.

```text
src/VSlices.Tooling/
  Commands/
    VsirCommands.cs
    RulesetCommands.cs
    UpdateCommands.cs

  Lowering/
    TranspilationOperation.cs
    RebaseOperation.cs
    LoweringCoordinator.cs
    Lineage/
      LoweringLineageStore.cs
      LoweringLineageBootstrap.cs

  Project/
    VSlicesProjectContext.cs
    ProjectConfiguration.cs
    ArtifactDiscoveryPolicy.cs

  Rulesets/
    RulesetSourceMaterializer.cs
    RulesetSnapshotInstaller.cs
    RulesetUpdater.cs

  Updates/
    SelfUpdater.cs

  Presentation/
    TerminalOutput.cs
    CliVersion.cs

  CommandInfrastructure.cs
  Program.cs
```

The key flow is:

```text
CLI handler
  -> operation / coordinator
  -> semantic mechanism / project infrastructure / target adapter
```

`TranspilationOperation` owns the reusable path from VSIR + project target/ruleset/context to a deterministic projection. `RebaseOperation` owns deterministic three-way rebase. `LoweringCoordinator` owns the policy for selecting the least-powerful sufficient lowering mechanism. Public commands do not call other public command handlers to reuse behavior.

`VSlicesProjectContext` is the single detected representation of a VSlices project and carries project root, `.vslices` root, configuration, ruleset root and lineage root.

## Semantic conservation

Unknown semantics must not disappear silently.

The parser fails closed for unknown keys in known semantic mappings including:

```text
root
construction
construction step
ensure
condition
failure
equality
```

Mappings whose keys are user-defined data remain open where appropriate, including `state`, `representation`, and `construction.input`.

`traits` are capabilities, not a sequence. Their order is irrelevant; duplicate and unknown traits are explicit validation errors. The current experimental subset still requires `transform`; `identifier` independently requires explicit equality semantics.

## Lowering and lineage

`transpile` requests one deterministic witness when VSIR, ruleset and target context are sufficient.

`rebase` receives conceptually:

```text
previous deterministic projection
+ human materialization
+ next deterministic projection
-> rebased human materialization
```

`lower` coordinates:

```text
no materialization
  -> transpile
  -> record deterministic baseline

materialization + recorded lineage
  -> rebase
  -> update baseline

materialization + no lineage + exact deterministic witness
  -> record current deterministic baseline
  -> preserve witness unchanged

conventional materialization + no lineage + authorized bootstrap
  -> compute current deterministic projection
  -> record it as baseline
  -> preserve human witness byte-for-byte
  -> succeed without immediate rebase

otherwise
  -> stop / require explicit --from ancestry
```

The first bootstrap convention is:

```yaml
lineage:
  bootstrap:
    convention: existing-materialization
```

Despite the name, the human materialization is **not** stored as the deterministic baseline. The convention authorizes lineage to begin at the current point; Tooling computes and stores the current deterministic projection while leaving the human witness untouched.

The next semantic change can then perform a real three-way rebase.

### Lineage versioning decision

`.vslices/lineage/` is intended to be version-controlled by default.

It is not semantic authority, but it is continuity evidence required for another developer, machine or CI environment to reconstruct the same automatic three-way rebase without requiring an unavailable historical VSIR. Git history records the evolution of this operational evidence; Tooling does not currently reconstruct lineage from Git history.

No provenance graph is introduced. The deterministic baseline remains the smallest evidence currently demonstrated as necessary.

## Rulesets and updates

`vslices init` and `vslices update --ruleset` share ruleset-source materialization and snapshot installation mechanisms.

The flow is:

```text
source
  -> materialize
  -> prepare selected-target snapshot
  -> validate with the real target loader
  -> atomic replace with backup/rollback
```

For C#, a prepared snapshot must successfully load through `CSharpLoweringRuleSet.Load` before the current `.vslices/ruleset` can be replaced. Missing files, duplicate rules, unsupported renderers/modes and other loader errors stop before swap.

For supported GitHub repository sources, `ruleset.ref` is treated as a real Git reference candidate: branch, then tag, then direct commit/archive reference. Local directories and generic direct ZIP URLs do not silently reinterpret `ref` as a branch.

See [`docs/rulesets.md`](docs/rulesets.md) and [`docs/configuration.md`](docs/configuration.md).

## Project configuration

A normal initialized configuration includes:

```yaml
version: 0.1

targets:
  default: csharp

ruleset:
  source: https://github.com/vslices/ruleset
  ref: main

lineage:
  bootstrap:
    convention: existing-materialization

updates:
  source: https://github.com/vslices/tooling
  channel: preview
```

Configuration is operating policy, not VSIR semantics. Explicit CLI arguments override project configuration where a command supports an override.

## Validation

The repository has two relevant test layers:

```text
tests/VSlices.Vsir.CSharp.Tests
  = VSIR / C# semantic and lowering behavior

tests/VSlices.Tooling.Tests
  = project/ruleset/lineage orchestration through the real CLI boundary
```

Tooling integration tests intentionally execute the built CLI out-of-process. During consolidation, an in-process test reference exposed a case-insensitive assembly identity collision between the executable assembly `vslices` and Framework assembly `VSlices`. The CLI itself remains executable, but ordinary in-process loading of both assemblies is unsafe. This is retained as an architectural finding rather than forcing a new assembly split solely for tests.

CI additionally smoke-tests `update --ruleset`, non-destructive lineage bootstrap, subsequent automatic rebase, discovery, target-context delegation and Native AOT distribution.

## Evidence from Ticket Support

`TicketId.vsir` exposed the current chain incrementally:

```text
semantic conservation
-> identifier/equality representation
-> C# structural lowering
-> external ordinal equality rules
-> ruleset update
-> lineage bootstrap
-> non-destructive bootstrap correction
```

Tooling `build4.218` was exercised against `atom-dev-serviu/access-management-product` from a clean lineage state. The deterministic baseline was created and the existing human `TicketId.vsir.cs` remained unchanged, confirming the bootstrap behavior against the real consumer.

A separate target-context namespace problem was observed and intentionally remains outside this consolidation. The follow-up hypothesis derives namespace from the related `.csproj` (`RootNamespace` or project name) plus relative path, with future project-specific exclusions for organizational-only path segments.

## Explicit future scope

Recorded but not implemented in this consolidation:

- namespace path policy and path exclusions;
- convention selection according to availability;
- whether convention definitions partly belong in `vslices/ruleset`;
- aggregate `vslices update` semantics;
- consistent presentation styles for `transpile`, `rebase`, and `lower`;
- interpretive lowering;
- general provenance graphs or Git-history ancestry reconstruction;
- new VSIR classifications, normalization, target languages, or configurable themes.

## Orientation

Read [`AGENTS.md`](AGENTS.md) and [`docs/ai-development-orientation.md`](docs/ai-development-orientation.md) before semantic or architectural changes. The release direction is in [`docs/releases/v0.2.0-preview.md`](docs/releases/v0.2.0-preview.md), and installation details remain in [`docs/how-to-install-on-windows.md`](docs/how-to-install-on-windows.md).

The repository prefers small evidence-driven extensions over speculative generalization. Material decisions must remain reconstructible from repository artifacts rather than conversation history.
