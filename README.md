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

Plain `vslices update` and combined `--self --ruleset` remain intentionally undefined until aggregate ordering and partial-failure semantics are justified by evidence.

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

A missing lowering rule is a stop condition, never permission to guess. A Ruleset also cannot create a semantic operation that VSIR itself does not recognize.

## Internal responsibility tree

Command handlers are CLI adapters, not orchestration containers.

```text
src/VSlices.Tooling/
  Commands/
  Lowering/
    TranspilationOperation.cs
    RebaseOperation.cs
    LoweringCoordinator.cs
    SemanticRefactoring/
      SemanticRefactoringCoordinator.cs
      DotNetSemanticRefactoringClient.cs
      SemanticRefactoringAuthorization.cs
      TransactionalFileWriter.cs
  Project/
  Rulesets/
  Updates/
  Presentation/

src/VSlices.Targets.DotNet/
  DotNetTargetContextResolver.cs
  Namespace/
    NamespacePathPolicy.cs

src/VSlices.Targets.DotNet.Refactor/
  Program.cs
  RefactorArguments.cs
  NamespaceMovePlanner.cs
  CompilationValidator.cs
  RefactorManifest.cs
```

The key flow is:

```text
CLI handler
  -> operation / coordinator
  -> semantic mechanism / project infrastructure / target adapter
```

`TranspilationOperation` owns the reusable path from VSIR + project target/ruleset/context to a deterministic projection. `RebaseOperation` owns deterministic three-way materialization rebase. `LoweringCoordinator` owns the high-level `lower` policy and delegates the target-semantic namespace-move subworkflow to `SemanticRefactoringCoordinator`.

`VSlicesProjectContext` is the single detected representation of a VSlices project and carries project root, `.vslices` root, configuration, ruleset root and lineage root.

## Semantic conservation and normalization

Unknown semantics must not disappear silently.

The parser fails closed for unknown keys in known semantic mappings. The validator also owns recognized semantic values.

The first normalization semantic demonstrated by the TicketCode consumer is:

```yaml
- normalize:
    target: input.Value
    intrinsic: trim
```

The current authority split is:

```text
VSlices.Vsir
  -> recognizes `trim` as a valid normalize intrinsic

C# lowering mechanism
  -> preserves construction-step order and normalization dataflow

Ruleset
  -> supplies target-specific realization of intrinsic.trim
```

An unknown normalize intrinsic is rejected by VSIR before Ruleset lookup. This prevents a target rule from inventing domain semantics accidentally.

The demonstrated `trim` renderer is pure. Current lowering may repeat the rendered expression in validation and construction; broader normalization will require either referential transparency or single-evaluation lowering.

## .NET target context and namespace policy

Default C# namespace derivation uses the nearest unique `.csproj`, evaluates `RootNamespace` through MSBuild, then appends the project-relative VSIR directory path after project policy is applied.

Example configuration:

```yaml
targets:
  default: csharp
  csharp:
    namespace:
      ignore-folders:
        - "Aggregates/*"
        - "Aggregates/**/Entities"
```

Pattern semantics are segment-aware:

```text
*   = exactly one directory segment
?   = one character inside a segment
**  = zero or more complete directory segments
```

The complete pattern establishes context, but only the terminal matched folder is excluded from namespace derivation.

Examples:

```text
Aggregates/*
  -> ignores any direct aggregate folder but preserves Aggregates

Aggregates/**/Entities
  -> ignores a terminal Entities folder at any depth under Aggregates

Aggregates/**/*
  -> ignores every descendant folder under Aggregates while preserving Aggregates itself
```

An explicit `--namespace` remains authoritative and bypasses derived namespace policy.

See [`docs/configuration.md`](docs/configuration.md).

## Lowering, rebase and lineage

`transpile` requests one deterministic witness when VSIR, Ruleset and target context are sufficient.

`rebase` is the textual primitive:

```text
previous deterministic projection
+ human materialization
+ next deterministic projection
-> rebased human materialization
```

It remains conservative. `REB002` means the deterministic change location cannot be established uniquely. `REB004` means the location is known but human and deterministic branches changed it differently.

`--resolve deterministic` authorizes only the known textual conflict region.

`lower` is the project workflow:

```text
no materialization
  -> transpile + deterministic baseline

materialization + recorded lineage
  -> textual rebase
  -> optional target-semantic closure for known namespace moves
  -> advance baseline

materialization + no lineage + exact deterministic witness
  -> establish lineage without rewriting the witness

conventional materialization + authorized bootstrap
  -> store current deterministic projection
  -> preserve human witness byte-for-byte

otherwise
  -> stop / require explicit ancestry
```

The direct `rebase` command does **not** promise the same project-wide target-semantic closure as `lower`. That distinction is intentional.

`.vslices/lineage/` is intended to be version-controlled by default. It is continuity evidence, not semantic authority, and Tooling does not currently reconstruct missing lineage from Git history.

## Roslyn semantic refactoring

A real TicketCode rebase proved that preserving unrelated human text does not guarantee target-semantic validity: a namespace declaration moved while human fully-qualified references elsewhere still pointed to the old symbol.

For the observed namespace-move case, `lower` can invoke a managed Roslyn companion after the textual candidate is known.

The authority model is intentionally split:

```text
1. detect a namespace move cheaply
2. ask before loading Roslyn/MSBuildWorkspace
3. discover exact semantic references
4. validate baseline and proposed compilations
5. show blast radius
6. ask separately before mutating human-maintained code
7. re-check source preconditions
8. commit affected sources + lineage transactionally
```

Only explicit `y` / `yes` approves the interactive authority boundaries. Blank, EOF or any unrecognized answer rejects.

The semantic artifact name comes from parsed VSIR `name:` and is carried through `TranspilationResult`; it is not inferred from the `.vsir` filename.

Compilation validation is fail-closed. If Roslyn cannot produce a `Compilation`, Tooling treats that as “could not verify”, not as successful validation.

## Native AOT + managed Roslyn companion

Roslyn/MSBuildWorkspace is deliberately outside the Native AOT executable.

Distribution shape:

```text
vslices / vslices.exe
  Native AOT coordinator

refactor/
  VSlices.Targets.DotNet.Refactor.dll
  BuildHost-netcore/
  BuildHost-net472/
  Roslyn/MSBuild runtime dependencies
```

A complete companion requires at least:

```text
refactor/VSlices.Targets.DotNet.Refactor.dll
refactor/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll
```

Startup health checks, same-build `vslices update --self` repair, downloaded-archive validation, staging validation and the Windows installer all use the same completeness rule. A root helper DLL without `BuildHost-netcore` is incomplete.

The standalone Native AOT CLI emits `UPD016` when it detects an incomplete companion and directs the user to run `vslices update --self`.

## Rulesets and updates

`vslices init` and `vslices update --ruleset` share ruleset-source materialization and snapshot installation mechanisms.

```text
source
  -> materialize
  -> prepare selected-target snapshot
  -> validate with the real target loader
  -> atomic replace with backup/rollback
```

For C#, a prepared snapshot must successfully load through `CSharpLoweringRuleSet.Load` before the current `.vslices/ruleset` can be replaced.

For supported GitHub repository sources, `ruleset.ref` is treated as a real Git reference candidate: branch, tag, then direct commit/archive reference.

See [`docs/rulesets.md`](docs/rulesets.md).

## Validation

Relevant test layers are:

```text
tests/VSlices.Vsir.CSharp.Tests
  = VSIR / C# semantic and lowering behavior

tests/VSlices.Tooling.Tests
  = Tooling orchestration, semantic-refactoring safety and installation health
```

CI also exercises Roslyn/MSBuildWorkspace, non-destructive lineage bootstrap, subsequent rebase, target context and namespace patterns, complete managed companion packaging, and Native AOT artifacts for Linux, win-x64 and win-arm64.

## Evidence from Ticket Support

The current experimental chain is:

```text
TicketId
  -> identifier/equality representation
  -> target lowering rules
  -> project ruleset lifecycle
  -> non-destructive lineage bootstrap

TicketCode
  -> normalize representation
  -> ordered normalization dataflow
  -> external intrinsic.trim realization
  -> evaluated namespace target context
  -> conservative rebase conflict semantics
  -> explicit textual conflict authority
  -> target-semantic namespace consequence
  -> Roslyn semantic blast-radius planning
  -> Native AOT companion/runtime closure
  -> package-pruning workspace diagnostics
  -> explicit analysis cost authority
  -> namespace path policy
  -> review hardening of semantic ownership and fail-closed guarantees
```

The repository deliberately preserves this chain because each mechanism was added only after the real consumer exposed the next unjustified boundary.

## Explicit future scope

Recorded but not implemented in this baseline:

- normalization intrinsics beyond `trim`;
- namespace-pattern negation, precedence or regex semantics;
- semantic refactoring kinds beyond the observed namespace move;
- non-interactive semantic-refactoring approval policy;
- generic compiler repair;
- Roslyn workspace-scope optimization that could weaken blast-radius completeness;
- killing the Roslyn child process tree on cancellation;
- interpretive lowering;
- general provenance graphs or Git-history ancestry reconstruction;
- aggregate `vslices update` semantics;
- configurable terminal themes.

## Orientation

Read [`AGENTS.md`](AGENTS.md) and [`docs/ai-development-orientation.md`](docs/ai-development-orientation.md) before semantic or architectural changes. The release direction is in [`docs/releases/v0.2.0-preview.md`](docs/releases/v0.2.0-preview.md), and the TicketCode experiment is reconstructed in [`docs/experiments/ticket-code-lowering.md`](docs/experiments/ticket-code-lowering.md).

The repository prefers small evidence-driven extensions over speculative generalization. Material decisions must remain reconstructible from repository artifacts rather than conversation history.
