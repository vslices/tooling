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
```

## Experimental prioritization

Evidence determines what the current machinery can justify and where the observed boundary lies. It does not uniquely determine which unresolved boundary must be investigated next.

Human maintainer interest may therefore prioritize which evidence-compatible experiment is run next. That interest may order the research agenda, but it does not redefine consumer semantics, move a failure to a preferred layer, or justify implementation without discriminating evidence.

`TicketCode` was selected partly because normalization is an interesting next boundary after `TicketId`; the namespace experiment followed because the successful deterministic witness made the previously deferred target-context gap concrete. The rebase conflict experiment then followed because the real consumer exposed `REB002` after target context changed.

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

`--resolve deterministic` does **not** replace the complete human materialization. It replaces only the conflicting insertion region that was located through deterministic surrounding context, then preserves unrelated human edits and records the new deterministic lineage baseline after the write succeeds.

Conceptually:

```text
human before:
  namespace Tickets.Domain.Aggregates;
  + human formatting/helpers/etc.

--resolve deterministic

human after:
  namespace Tickets.Domain.Aggregates.Tickets;
  + same unrelated human formatting/helpers/etc.
```

If the human projection already contains exactly the next deterministic insertion, rebase treats the requirement as already satisfied and preserves the file unchanged.

Unknown resolution names fail explicitly; this experiment introduces only the `deterministic` strategy.

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

## Current status

TicketCode lowering is successful end-to-end for the semantic surface exercised by the normalization experiment:

```text
normalize trim
ensure non-empty
deterministic state construction
ordinal equality
```

The namespace phase tests a separate target-context concern. The rebase phase now makes the resulting human/deterministic conflict explainable and explicitly resolvable without weakening the default conservative merge policy.

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
configurable terminal themes
normalization semantics beyond deterministic expression transforms demonstrated by TicketCode
```

The purpose of this note is reconstructibility: future work should be able to recover why Tooling contains generic normalization dataflow, why `trim` belongs to external Ruleset knowledge, why default namespaces now include the complete project-relative directory path, why folder-ignore policy remains a separate follow-up experiment, and why concurrent human/deterministic insertions require explicit resolution rather than inference.
