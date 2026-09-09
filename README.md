# VSlices Tooling

VSlices Tooling is the executable tooling surface of the VSlices suite. The CLI is named `vslices`.

The `v0.2.0` release line was built by progressively reconstructing real VSIR from the Ticket Support / Identities corpus. Its governing rule is:

> Lowering may complete implementation detail. Lowering must not complete missing semantics.

Two companion rules are equally important:

> Semantic structure must be represented or explicitly rejected; it must never disappear silently.

> Tooling owns the constrained rule language. Rulesets own the target vocabulary expressed through that language.

## Implemented command surface

```text
vslices init

vslices new vsir <artifact>
vslices discovery vsir <artifact>
vslices update vsir <artifact> ...
vslices search ...

vslices transpile <artifact>
vslices rebase <artifact>
vslices lower <artifact-or-project>

vslices update self
vslices update ruleset

vslices --version
vslices -v
```

`update` is a command group. There is deliberately no legacy `update --self`, `update --ruleset`, plain aggregate `update`, or combined update alias in the current public contract.

The current authoring loop is:

```text
partial evidence
  -> new
  -> discovery
  -> justified update
  -> validation
  -> discovery again
  -> lower when coherent enough
  -> observe executable evidence
  -> return to semantics when necessary
```

`new vsir` establishes only identity:

```yaml
vsir: 0.1
name: StreetName
```

All further semantic assertions flow through `discovery` and `update`.

## Authority boundaries

```text
consumer/source project
  = concrete software/domain evidence

.vsir
  = semantic source for the represented artifact

vslices/intermediate-representation
  = VSIR language semantics

vslices/tooling
  = parser/validator, authoring/discovery/search/update,
    lowering orchestration, lineage/rebase, CLI and target adapters

vslices/ruleset
  = revisable deterministic target-realization knowledge

.vslices/config.yaml
  = project operating policy

.vslices/ruleset/
  = installed source-owned target-knowledge snapshot

.vslices/extensions/
  = project-owned semantic-extension overlay

.vslices/lineage/
  = operational deterministic ancestry evidence

target-native tooling
  = target facts already owned by that ecosystem
    (.NET: MSBuild/Roslyn)

.vsir.cs
  = human-editable executable materialization constrained by VSIR

compile/test/runtime/consumer behavior
  = evidence about the resulting reconstruction
```

A renderer, template, Framework convenience interface, filesystem convention, or historical target materialization does not gain semantic authority merely because it exists.

A missing lowering rule is a stop condition, never permission to guess. A target renderer also cannot create semantic validity by itself: core VSIR semantics must already be recognized, or a project semantic extension must explicitly admit the operation.

## Internal responsibility tree

Command handlers are CLI adapters, not orchestration containers.

```text
src/VSlices.Tooling/
  Authoring/
    VsirArtifactState.cs
    VsirAuthoringContract.cs
    VsirMutationPipeline.cs
    VsirMutationCandidate.cs
    Source/

  Commands/
    NewCommands.cs
    DiscoveryCommands.cs
    UpdateCommands.cs
    SearchCommands.cs
    VsirCommands.cs
    RulesetCommands.cs

  IO/
    AtomicFile.cs

  Lowering/
    TranspilationOperation.cs
    RebaseOperation.cs
    LoweringCoordinator.cs
    Lineage/
    SemanticRefactoring/
      SemanticRefactoringCoordinator.cs
      DotNetSemanticRefactoringClient.cs
      DotNetTypeResolutionClient.cs
      TransactionalFileWriter.cs

  Project/
    VSlicesProjectContext.cs
    ProjectConfiguration.cs
    ProjectExtensions.cs

  Rulesets/
  Updates/
  Presentation/

src/VSlices.Vsir/
  VsirParser.cs
  VsirStructuralContract.cs
  VsirNominalTypeReferences.cs
  shape-specific canonical parsers / validators

src/VSlices.Vsir.CSharp/
  CSharpLanguageLowerer.cs
  CSharpSumDomainTypeLowerer.cs
  CSharpMaintainedDomainTypeLowerer.cs
  CSharpLiteral.cs
  CSharpRebaser.cs
  CSharpLoweringRuleSet.cs

src/VSlices.Targets.DotNet/
  DotNetTargetContextResolver.cs
  Namespace/
    NamespacePathPolicy.cs

src/VSlices.Targets.DotNet.Refactor/
  managed Roslyn/MSBuild companion
```

The key flow is:

```text
CLI adapter
  -> operation / coordinator
  -> semantic mechanism / project infrastructure / target adapter
```

`TranspilationOperation` owns the reusable path from VSIR + project target/ruleset/extensions/context to a deterministic projection. `RebaseOperation` owns deterministic three-way materialization rebase. `LoweringCoordinator` owns the high-level artifact/project `lower` workflow and delegates known target-semantic namespace closure to `SemanticRefactoringCoordinator`.

`VSlicesProjectContext` is the single detected representation of a VSlices project and carries the project root, `.vslices` root, configuration, installed Ruleset root, project extensions root and lineage root.

Filesystem persistence that is not command-specific lives below `IO/`; project configuration does not depend on command infrastructure merely to obtain atomic text replacement.

## Progressive validity, conformance and authorability

These are separate questions:

```text
progressively valid
  = the artifact can participate in progressive reconstruction

conforming
  = the assertions currently present belong to canonical VSIR
    under the active semantic validation environment

publicly authorable
  = discovery/update currently know how to guide creation of that form

lowerable(target, context)
  = the conforming artifact also has sufficient target knowledge,
    project context and executable lowering mechanisms
```

Canonical conformance is owned by `VsirParser` + validator + `VsirValidationContext`, not by the intentionally narrower public authoring vocabulary.

The complete public Domain Type authoring envelope currently proven through `new/discovery/update` is:

```text
kind: domain-type
shape: product
classification: value-object | identifier
traits: transform | identifier | refined
```

Parser/lowering coverage is broader: real `sum`, `maintained`, and aggregate-root-sum witnesses can conform and lower while their public authoring path remains gated until full authoring parity is demonstrated.

An artifact can also be incomplete **and** contain a present invalid assertion. Discovery preserves both facts instead of hiding the invalid assertion behind missing paths.

## Search metadata versus semantic traits

`tags` is operational metadata:

```text
tags
  -> set / add / remove
  -> search, indexing, grouping
  -> no semantic effect
```

`traits` are semantic capabilities and participate in validation/lowering.

The public `VsirParser` validates and strips searchable metadata before canonical semantic interpretation, so tags do not accidentally acquire semantic authority.

Example:

```text
vslices search --filter tags:contains:ticket
```

## Semantic conservation and strict structural parsing

All canonical Domain Type forms enter through one public parser boundary:

```text
.vsir
  -> searchable metadata validation/strip
  -> shared structural contract
  -> shape-specific canonical parser
  -> semantic validation
```

Product, sum and maintained parsers remain specialized, but their common structural obligations are shared. Non-scalar semantic keys fail closed; expanded field declarations reject unknown scalar keys; malformed explicit `from` references fail closed; adding `from` or `mapping` requires an explicit `type`; and contradictory `from + mapping` declarations cannot be interpreted differently by each form.

Structural type shorthand remains valid syntax. When progressive authoring enriches a shorthand field with local source/projection metadata, Tooling first expands the existing semantic type under `type:` instead of persisting an invalid sibling-key layout.

Specialized canonical parser/lowerer paths can still have witness-limited executable coverage. That is an implementation coverage boundary, not a separate meaning for the same VSIR operation; new corpus evidence should extend the relevant primitive rather than infer shape-specific semantics from an accidental implementation gap.

Unknown semantics never disappear silently.

## Construction and representation semantics

The current transform construction surface exercised by the corpus includes:

```text
normalize
ensure
resolve
apply
refine
```

Representation can declare a direct semantic source:

```yaml
Value:
  type: string
  from: state.Name
```

or an explicit mapping:

```yaml
Reference:
  type: AccountReference.Repr
  mapping:
    represent: state.Reference
```

`from` and `mapping` are mutually exclusive for one representation coordinate.

Current representation expression forms include:

```text
stringify
represent
select
map
intrinsic
```

Composition remains explicit. Tooling does not silently rewrite:

```text
select(represent(state.Street), Value)
```

into:

```text
select(state.Street, Value)
```

unless the semantic source establishes that equivalence.

### Normalization

The first core normalization witness is:

```yaml
- normalize:
    target: input.Value
    intrinsic: trim
```

Authority remains split:

```text
VSlices.Vsir
  -> recognizes `trim`

C# lowering mechanism
  -> preserves ordered normalization dataflow

Ruleset
  -> realizes intrinsic.trim for C#
```

Unknown normalize intrinsics fail before Ruleset lookup.

### Intrinsic refinement

`StreetExtension` established intrinsic refinement with named ordered outputs:

```yaml
- refine:
    intrinsic: split-first-rest
    value: input.Value
    as:
      Name: name
      Value: value
    failure:
      message: Debes especificar un nombre y un valor, separados por espacio

- refine:
    state:
      Name: name
      Value: value
```

The language-level semantics live in `vslices/intermediate-representation`; Ruleset owns only target realization of that admitted relation.

## Project semantic extensions

`.vslices/ruleset` and `.vslices/extensions` deliberately have different lifecycle owners. `init --force` and `vslices update ruleset` may replace the installed Ruleset snapshot, but they preserve the project-owned extension overlay.

Example:

```yaml
# .vslices/extensions/manifest.yaml
version: 0.1
catalogs:
  - ticketing.yaml
```

```yaml
# .vslices/extensions/ticketing.yaml
extensions:
  - node: intrinsic.normalize-rut
    semantic:
      kind: normalize
    targets:
      csharp:
        mode: deterministic
        renderer: expression
        bindings: [value]
        template: "Rut.Normalize({value})"
```

`semantic.kind` admits the operation; `targets.csharp` realizes that already-admitted operation. The renderer's placeholder vocabulary is exact: every placeholder must be declared in `bindings`, every declared binding must be used, and render calls must provide neither missing nor extra values.

A C# renderer without semantic admission does not make an unknown operation valid. A declared semantic with no C# realization reaches a target-lowering diagnostic instead.

## .NET target context and nominal dependencies

Default C# namespace derivation uses:

```text
nearest unique .csproj
  -> evaluated RootNamespace through MSBuild
  -> project-relative VSIR directory
  -> namespace path policy
```

An explicit `--namespace` overrides the derived namespace **without discarding an already discoverable project identity**. Project context remains available for nominal type resolution and other target-native facts. The test suite exercises that composition end-to-end by resolving a referenced nominal type while an explicit namespace override is active.

Namespace policy example:

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

Nominal type dependencies are collected by traversing the admitted semantic model, including nested semantic types and sum variants, before the .NET adapter asks Roslyn/MSBuild to resolve their target symbols. A type does not escape resolution merely because it appears outside a root product field.

See [`docs/configuration.md`](docs/configuration.md).

## Lowering, project lowering, rebase and lineage

`transpile` requests one deterministic target witness when VSIR, Ruleset, project extensions and target context are sufficient.

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

Output selection is part of that contract on every path. If `--stdout` is requested during lineage bootstrap, status/progress goes to stderr and stdout contains the preserved human materialization requested by the caller. Bootstrap may still establish operational lineage; it does not silently replace the human witness.

`lower <project>` processes the project's selected VSIR artifacts under one project/Ruleset/extensions/target environment and distinguishes per-artifact outcomes:

```text
Lowered
Unsupported
Failed
```

Unsupported semantic/target surface may be reported while other artifacts continue to lower. Environment/toolchain/IO/orchestration failures also allow diagnostic collection to continue, but make the overall project invocation fail. Automation therefore cannot receive exit code 0 for a real MSBuild/Roslyn/toolchain failure merely because processing continued.

The direct `rebase` command does **not** promise the same project-wide target-semantic closure as `lower`.

`.vslices/lineage/` is intended to be version-controlled by default. It is continuity evidence, not semantic authority, and Tooling does not currently reconstruct missing lineage from Git history.

## C# realization guarantees

Target realization now treats two details as explicit shared responsibilities rather than ad-hoc string concatenation:

- semantic state references distinguish stored fields from derived state accessors;
- C# string literals are encoded centrally, including newlines and other control characters across product, sum and maintained emitters that produce string literals.

The test suite includes a generated-materialization compilation witness so a lowerer result is not considered sufficiently evidenced merely because expected text fragments are present.

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

Only explicit `y` / `yes` approves the interactive authority boundaries. Blank, EOF or an unrecognized answer rejects.

Semantic artifact identity comes from parsed VSIR `name:` and is carried through the lowering workflow; it is not inferred from the `.vsir` filename.

Compilation validation is fail-closed. If Roslyn cannot produce a `Compilation`, Tooling treats that as “could not verify”, not as successful validation.

## Native AOT + managed Roslyn companion

Roslyn/MSBuildWorkspace remains outside the Native AOT executable.

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

Startup health checks, same-build `vslices update self` repair, downloaded-archive validation, staging validation and the Windows installer share the same completeness rule. A root helper DLL without `BuildHost-netcore` is incomplete.

## Ruleset lifecycle and updates

`vslices init` and `vslices update ruleset` share Ruleset-source materialization and snapshot installation mechanisms:

```text
source
  -> materialize
  -> prepare selected-target snapshot
  -> validate with the real target loader
  -> atomic replace .vslices/ruleset with backup/rollback
```

For C#, a prepared snapshot must successfully load through `CSharpLoweringRuleSet.Load` before the current snapshot can be replaced.

The independent updater surfaces are:

```text
vslices update self
vslices update ruleset
```

There is no aggregate update operation in v0.2.0.

See [`docs/rulesets.md`](docs/rulesets.md).

## Evidence and tests

Important witness progression:

```text
StreetName
  -> known green control

TicketId
  -> identifier/equality
  -> semantic conservation
  -> trusted lineage + non-destructive bootstrap

TicketCode
  -> normalize trim
  -> target context / namespace policy
  -> conservative rebase
  -> Roslyn namespace semantic closure

Risk
  -> negative control for semantic admission vs target realization

SrvIdentityId
  -> refined + identifier contracts
  -> nominal target symbol resolution

Location
  -> sequence types + derived state + representation composition
  -> resolve/apply/refine

StreetExtension
  -> intrinsic refine named bindings

Name
  -> sum canonical parse/lower witness

IdentityType
  -> maintained canonical parse/lower witness

SrvIdentity
  -> aggregate-root sum witness

TicketTrayFilter
  -> structural optional types + explicit represent projections
  -> no implicit flatten-single-field relation
```

The test layers are:

```text
tests/VSlices.Vsir.CSharp.Tests
  -> parser/validation/lowering/target-context evidence
  -> common malformed-structure negatives across canonical forms
  -> generated C# compilation witness

tests/VSlices.Tooling.Tests
  -> authoring mutation laws
  -> project context/extensions
  -> project lowering outcomes and exit-code semantics
  -> output destination/bootstrap contracts
  -> Ruleset lifecycle and semantic-refactoring safety

CI process smokes
  -> actual CLI composition
  -> Roslyn/MSBuildWorkspace
  -> lineage/rebase
  -> target context
  -> extension/ruleset lifecycle
  -> Native AOT packaging
```

The final TicketTrayFilter consumer evidence is pinned to:

```text
atom-dev-serviu/access-management-product
commit 7bae60d7e1af637cfcac6a802f867bc6979da444
products/ticket-support-product/TicketSupport.Queries/Domain/Search/TicketTrayFilter.vsir
```

The mutable `analysis/ticket-support-post-mortem` branch is useful for continued work, but the SHA above is the reproducible release witness.

## Explicit future scope

Recorded but not implemented in v0.2.0:

- full public authoring parity for witnessed `sum`, `maintained`, entity/aggregate-root forms;
- semantic vocabulary beyond current corpus evidence;
- invocation-local numbered selection for ambiguous artifact symbols;
- lowering multiple explicitly selected artifacts in one command invocation;
- folder/scoped/batch lowering beyond whole-project subjects;
- a stable CLI visual language before choosing canonical colors/spinners/progress semantics;
- generic compiler repair or semantic-refactoring families beyond the observed namespace move;
- stronger provenance/Git-history ancestry graphs;
- aggregate updater ordering/recovery semantics;
- interpretive lowering.

These are starting points for later evidence, not incomplete promises of v0.2.0.

## Orientation

Start with:

1. [`AGENTS.md`](AGENTS.md) — repository operating rules;
2. [`docs/ai-development-orientation.md`](docs/ai-development-orientation.md) — fresh-context development orientation;
3. [`docs/releases/v0.2.0-preview.md`](docs/releases/v0.2.0-preview.md) — release-line reconstruction and final hardening;
4. [`docs/experiments/ticket-tray-filter-projection-relation.md`](docs/experiments/ticket-tray-filter-projection-relation.md) — final experiment closure;
5. [`docs/vsir-capability-matrix.md`](docs/vsir-capability-matrix.md) — executable/public-authoring evidence matrix;
6. [`docs/semantic-authoring-affordances.md`](docs/semantic-authoring-affordances.md) — interaction contract.

The repository prefers small evidence-driven extensions over speculative generalization. Material decisions must remain reconstructible from repository artifacts rather than conversation history.
