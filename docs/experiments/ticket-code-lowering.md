# TicketCode lowering experiment

This branch exercises successive VSlices boundaries against the real `TicketCode` specimen from Ticket Support.

## Observation chain

```text
TicketCode.vsir
  -> real consumer
  -> VSIR100
     normalize not representable

  -> represent normalize explicitly
  -> real consumer
  -> CSL030
     normalize represented, but C# lowerer has no execution mechanism

  -> add ordered normalization dataflow in Tooling
     using the existing deterministic expression renderer

  -> test-only intrinsic.trim rule proves the current renderer is sufficient

  -> real consumer on build5.264 with production Ruleset
  -> CSL031
     No deterministic C# normalization rule is available for 'intrinsic.trim'

  -> ownership boundary confirmed
     normalization mechanism -> Tooling
     trim realization        -> Ruleset

  -> vslices/ruleset#2
     adds only intrinsic.trim using the existing expression renderer

  -> consumer updates Ruleset from experiment/ticket-code-lowering
  -> vslices lower TicketCode
  -> SUCCESS
     deterministic witness computed
     existing human materialization preserved
     lowering lineage established

  -> deterministic witness reveals namespace Tickets.Domain
     while TicketCode.vsir lives under Aggregates/Tickets

  -> target-context experiment
     default namespace = evaluated RootNamespace
                       + full relative directory path from .csproj to .vsir
     expected Tickets.Domain.Aggregates.Tickets

  -> real consumer reruns lower with existing lineage
  -> REB002
     old insertion rebaser cannot explain the namespace conflict precisely

  -> rebase conflict experiment
     distinguish unlocatable ambiguity from concurrent human/deterministic insertion
     expose baseline / human / next deterministic values
     provide explicit CLI resolution

  -> real consumer executes
     vslices lower TicketCode --resolve deterministic
  -> textual rebase succeeds
     namespace updated in human materialization
     deterministic lineage baseline advanced
  -> target compilation fails
     human using-static still references the previous TicketCode namespace

  -> target-semantic refactoring experiment
     resolve the moved symbol through Roslyn
     discover semantic reference blast radius
     stage all required edits
     require explicit human authorization, default N
     validate staged solution
     commit human files + lineage transactionally
```

## Experimental prioritization

Evidence determines what the current machinery can justify and where the observed boundary lies. It does not uniquely determine which unresolved boundary must be investigated next.

Human maintainer interest may therefore prioritize which evidence-compatible experiment is run next. That interest may order the research agenda, but it does not redefine consumer semantics, move a failure to a preferred layer, or justify implementation without discriminating evidence.

`TicketCode` was selected partly because normalization is an interesting boundary after `TicketId`; the namespace experiment followed because the successful deterministic witness made the previously deferred target-context gap concrete. The rebase conflict experiment then followed because the real consumer exposed `REB002` after target context changed. The Roslyn experiment follows because the real resolved materialization compiled incorrectly even though the textual three-way merge itself succeeded.

## Confirmed normalization finding

The current Ruleset primitive is expressive enough for the observed normalization.

A deterministic expression rule of the form:

```yaml
- node: intrinsic.trim
  mode: deterministic
  renderer: expression
  template: "{value}.Trim()"
```

can be consumed by Tooling without a new renderer mode.

Tooling processes construction steps in order and keeps a reference-to-expression environment. A successful normalize rule replaces the current expression for its target, and later ensures plus final state construction consume that updated expression.

For `TicketCode`, the deterministic witness contains semantics equivalent to:

```text
ensure non-empty(input.Value.Trim())
state.Value <- input.Value.Trim()
ordinal equality over state.Value
```

The real consumer confirmed both boundaries:

```text
without intrinsic.trim
  -> CSL031

with intrinsic.trim from vslices/ruleset#2
  -> deterministic lowering succeeds
  -> existing human materialization is not rewritten during configured lineage bootstrap
```

This demonstrates the ownership split:

```text
normalization dataflow / execution mechanism -> Tooling
trim realization knowledge                 -> Ruleset
```

## Human-editable materialization and deterministic witness

The successful consumer run also demonstrates the intended distinction between the deterministic witness and the existing human-editable materialization.

The lineage baseline contains the deterministic projection produced by Tooling, while the existing `TicketCode.vsir.cs` remains byte-for-byte human-owned during bootstrap. Human choices such as formatting, helper constants, static imports, `ToString()`, and other implementation freedoms are therefore not erased merely because Tooling can now produce a satisfying deterministic witness.

The later namespace experiment qualifies one part of that statement: preserving unrelated human **text** does not prove that the resulting human materialization remains semantically valid for its target. Human code outside the textual rebase region may semantically depend on the region that changed.

## Target-context namespace experiment

The successful TicketCode witness exposed the independent namespace question deferred by PR #4.

The previous resolver delegated namespace discovery to `dotnet new class` in the VSIR directory. For the real consumer that produced:

```text
Tickets.Domain
```

The target-context experiment defines the default target namespace explicitly as:

```text
evaluated .csproj RootNamespace
+ every relative directory segment from the .csproj directory to the .vsir directory
```

For the real consumer:

```text
project:
  services/ticket-service/Tickets.Domain/Tickets.Domain.csproj

evaluated RootNamespace:
  Tickets.Domain

VSIR:
  services/ticket-service/Tickets.Domain/Aggregates/Tickets/TicketCode.vsir

relative directory path:
  Aggregates/Tickets

expected namespace:
  Tickets.Domain.Aggregates.Tickets
```

The `.csproj` does not need to declare `<RootNamespace>` explicitly. Tooling asks MSBuild for the evaluated `RootNamespace`, preserving SDK/default/property-import behavior instead of guessing from XML text or hardcoding the project filename.

An explicit `--namespace` remains authoritative and bypasses project discovery as before.

## Target-context scope

This phase changes only default namespace derivation:

```text
find nearest unique .csproj
  -> evaluate RootNamespace through MSBuild
  -> compute directory path relative to .csproj directory
  -> append every directory segment to RootNamespace
```

Regression evidence covers both:

```text
Tickets.Domain.csproj
+ Aggregates/Tickets/TicketCode.vsir
-> Tickets.Domain.Aggregates.Tickets

Arbitrary.Project.csproj
+ explicit RootNamespace Company.Product
+ Features/Orders/OrderId.vsir
-> Company.Product.Features.Orders
```

## Rebase conflict experiment

The first real rerun after changing namespace derivation produced:

```text
REB002: Deterministic insertion anchor is missing or ambiguous in the human projection.
```

The underlying three-way state is more informative than that message:

```text
previous deterministic:
  namespace Tickets.Domain;

human materialization:
  namespace Tickets.Domain.Aggregates;

next deterministic:
  namespace Tickets.Domain.Aggregates.Tickets;
```

Both the human and deterministic branches changed the same insertion point after `Tickets.Domain`. That is a genuine concurrent insertion conflict, not merely an unlocatable anchor.

The rebaser now distinguishes:

```text
REB002
  deterministic location itself cannot be established uniquely

REB004
  deterministic insertion point is known
  human inserted one value there
  next deterministic projection inserted a different value there
```

`REB004` reports the three values needed to understand the conflict:

```text
Baseline insertion: <empty>
Human insertion: '.Aggregates'
Next deterministic insertion: '.Aggregates.Tickets'
```

The default remains conservative: VSlices does not infer that the human intended the deterministic value merely because one string is a prefix of the other.

## Explicit CLI resolution

A maintainer who has inspected the conflict can resolve that exact region explicitly with:

```text
vslices lower TicketCode --resolve deterministic
```

The same strategy is available to explicit `rebase`.

`--resolve deterministic` does **not** replace the complete human materialization. It resolves only the conflicting textual insertion region located through deterministic surrounding context. This authorization is intentionally narrower than authority to modify arbitrary semantic references elsewhere in human-maintained code.

Conceptually:

```text
human before:
  namespace Tickets.Domain.Aggregates;
  + human formatting/helpers/etc.

--resolve deterministic

textual candidate:
  namespace Tickets.Domain.Aggregates.Tickets;
  + same unrelated human text
```

If the human projection already contains exactly the next deterministic insertion, rebase treats the textual requirement as already satisfied and preserves that region unchanged.

Unknown resolution names fail explicitly; this experiment introduces only the `deterministic` strategy.

## Real post-rebase compilation finding

The consumer commit produced by the first real explicit resolution was:

```text
f057f23fc630fceb80036534251f6b863fa506e4
vslices lower TicketCode --resolve deterministic
```

The deterministic lineage baseline correctly moved to:

```text
namespace Tickets.Domain.Aggregates.Tickets;
```

and the human materialization's namespace declaration moved to the same namespace.

However, the human materialization also contained a pre-existing static import outside the textual conflict region:

```csharp
using static VSlices.Arrows.Req<
    Tickets.Domain.Aggregates.TicketCode.Input,
    Tickets.Domain.Aggregates.TicketCode>;
```

After the namespace move, those fully-qualified references still pointed to the old symbol location while `TicketCode` now lived at:

```text
Tickets.Domain.Aggregates.Tickets.TicketCode
```

The resulting file therefore failed target compilation.

This classifies the failure independently:

```text
deterministic projection          ✓
target namespace derivation       ✓
three-way textual rebase          ✓
explicit textual resolution       ✓
target-semantic consequences      ✗
```

The rebase did what its textual contract said. The missing capability was target-specific knowledge of semantic references affected by the known namespace move.

## Roslyn semantic namespace refactoring

The new experiment keeps the textual rebaser generic and introduces a separate .NET semantic-refactoring stage after a candidate rebase changes the namespace of the materialized top-level symbol.

The .NET target layer loads the real solution through `MSBuildWorkspace`, resolves the original `INamedTypeSymbol`, and asks Roslyn for semantic references to that symbol. It does not search for namespace strings globally.

Conceptually:

```text
previous human solution
  -> resolve TicketCode symbol

rebased candidate
  -> known namespace move

Roslyn FindReferences(TicketCode)
  -> semantic reference locations
  -> conservative source rewrites to the new fully-qualified symbol
  -> changed-file / reference-count blast radius
```

The blast radius is computed before any real source file is modified. The intended CLI surface is interactive:

```text
Semantic namespace refactoring
Symbol: Tickets.Domain.Aggregates.TicketCode
        -> Tickets.Domain.Aggregates.Tickets.TicketCode
References: N
Files: M

  path/to/FileA.cs (2 semantic references)
  path/to/FileB.cs (1 semantic reference)

This operation will modify human-maintained code outside the deterministic rebase region.
Apply semantic refactoring? [y/N]
```

An empty answer, EOF, `n`, `no`, or any unrecognized answer is rejection. Only an explicit `y`/`yes` approves the cross-file semantic edit.

This establishes a narrower authority model:

```text
automatic authority
  deterministic witness computation
  semantic blast-radius discovery
  staging and validation of a proposed refactoring

--resolve deterministic authority
  resolve the known textual rebase conflict deterministically

additional interactive authority
  modify semantic references outside that textual conflict region
```

`--resolve deterministic` therefore never silently implies authority to edit an arbitrary number of human-maintained files.

## Transactional safety

The semantic refactoring is prepared before authorization and committed only after all safety barriers pass.

```text
compute textual candidate
  -> load real solution/project context
  -> find exact semantic references
  -> prepare changed documents in Roslyn workspace
  -> compile affected projects before change
  -> compile proposed affected projects after change
  -> stage changed files
  -> show blast radius
  -> ask [y/N]
  -> verify source SHA-256 preconditions again
  -> atomically commit staged human files + deterministic lineage baseline
```

If the affected project already has compiler errors, Tooling currently fails closed because this first experiment cannot attribute post-change compiler failures safely against an already-invalid baseline.

If proposed compilation fails, the user rejects, a source file changes after the plan is computed, or the multi-file commit cannot complete, the operation does not intentionally leave a partially advanced lineage. The transaction writer uses preconditions, staged files, backups, and rollback for already-written members.

The deterministic lineage baseline participates in the same transaction as the human source changes. A semantic refactoring must not advance lineage while leaving its human projection behind.

## Native AOT boundary

Roslyn/MSBuild workspace support is intentionally not embedded into the Native AOT CLI process.

The distribution now has two execution pieces:

```text
vslices / vslices.exe
  Native AOT coordinator
  owns UX, authority, lineage and transactional commit

refactor/VSlices.Targets.DotNet.Refactor.dll
  managed companion
  owns Roslyn/MSBuild semantic analysis and staged target validation
```

The companion returns a staged plan/manifest; it does not commit the consumer files itself. This keeps Roslyn's dynamic workspace surface outside the AOT core while retaining a single user-facing `vslices` command.

PR/release archives and the installer include the companion directory. The helper-aware self-updater also replaces that directory for subsequent updates. There is one transition caveat: a VSlices build predating the companion only knows how to self-update the native executable, so the first move to a helper-aware experimental build requires bootstrapping the complete artifact once. Subsequent helper-aware builds can update both pieces together.

## Explicit next experiment / non-scope

Folder exclusion is deliberately **not** part of this phase.

The next target-context experiment may introduce an explicit ignore/exclusion mechanism, potentially using patterns, so selected directory segments do not participate in generated namespaces.

For example, such a future policy might allow a consumer to keep a file under:

```text
Aggregates/Tickets/TicketCode.vsir
```

while excluding `Tickets` from namespace derivation and obtaining:

```text
Tickets.Domain.Aggregates
```

That behavior is intentionally not implemented or specified here. This experiment first establishes the unfiltered baseline rule:

```text
RootNamespace + full relative path
```

Only after that behavior is exercised against the real consumer should folder-ignore syntax, matching semantics, ownership, and precedence be designed.

Two CLI surfaces discussed during the semantic-refactoring design are also explicitly **non-scope** for this phase:

```text
vslices lower TicketCode --plan
vslices lower TicketCode --apply-refactorings [values]
```

The current experiment deliberately uses interactive authorization with default `N`. It does not define a non-interactive approval vocabulary, refactoring categories, plan persistence, or a generic semantic-refactoring command surface.

## Current status

TicketCode lowering is successful end-to-end for the semantic surface exercised by the normalization experiment:

```text
normalize trim
ensure non-empty
deterministic state construction
ordinal equality
```

The namespace phase tests a separate target-context concern. The rebase phase makes the resulting human/deterministic conflict explainable and explicitly resolvable without weakening the default conservative merge policy. The Roslyn phase now tests whether known target-semantic consequences of that authorized namespace move can be discovered, validated, shown as a blast radius, separately authorized, and committed transactionally.

## Non-scope continuity

All prior explicit non-scope remains inherited except for the narrow boundaries moved by direct TicketCode evidence.

Still out of scope:

```text
new VSIR concepts or classifications beyond the observed normalize semantic
new Ruleset renderer modes
additional normalization intrinsics
new target languages
interpretate
Git-history ancestry reconstruction
general provenance graph
aggregate update semantics
folder-ignore / namespace-exclusion patterns
project-specific path-segment exclusions
namespace rewriting beyond RootNamespace + full relative path
additional automatic conflict-resolution strategies
implicit preference for deterministic changes on conflict
generic semantic repair of arbitrary compiler failures
semantic refactorings beyond the observed namespace move
non-interactive refactoring approval policy
vslices lower TicketCode --plan
vslices lower TicketCode --apply-refactorings [values]
configurable terminal themes
normalization semantics beyond deterministic expression transforms demonstrated by TicketCode
```

The purpose of this note is reconstructibility: future work should be able to recover why Tooling contains generic normalization dataflow, why `trim` belongs to external Ruleset knowledge, why default namespaces now include the complete project-relative directory path, why folder-ignore policy remains a separate follow-up experiment, why concurrent human/deterministic insertions require explicit resolution rather than inference, and why preserving unrelated human text is not enough when target-semantic dependencies cross the rebase boundary.
