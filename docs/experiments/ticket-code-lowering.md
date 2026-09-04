# TicketCode lowering experiment

This branch exercises the current VSIR lowering boundary against the real `TicketCode` specimen from Ticket Support.

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
```

## Experimental prioritization

Evidence determines what the current machinery can justify and where the observed boundary lies. It does not uniquely determine which unresolved boundary must be investigated next.

Human maintainer interest may therefore prioritize which evidence-compatible experiment is run next. That interest may order the research agenda, but it does not redefine consumer semantics, move a failure to a preferred layer, or justify implementation without discriminating evidence.

`TicketCode` was selected partly because normalization is an interesting next boundary after `TicketId`; the observed failures themselves still come from real consumer executions.

## Confirmed finding

The current Ruleset primitive is already expressive enough for the observed normalization.

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

## Independent target-context finding

The deterministic lineage baseline currently uses:

```text
namespace Tickets.Domain;
```

while the existing human materialization uses:

```text
namespace Tickets.Domain.Aggregates;
```

This does not invalidate the TicketCode normalization result: bootstrap stores the deterministic witness as lineage evidence and preserves the existing materialization unchanged.

It is, however, concrete evidence for the target-context namespace question explicitly deferred by PR #4. That question remains independent from normalization and should be investigated separately before generalizing namespace/path policy.

## Current status

TicketCode lowering is now successful end-to-end for the semantic surface exercised by this experiment:

```text
normalize trim
ensure non-empty
deterministic state construction
ordinal equality
```

The consumer has established lineage without destructive rewrite.

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
namespace path policy beyond recording the observed target-context mismatch
configurable terminal themes
new lineage or rebase semantics
normalization semantics beyond deterministic expression transforms demonstrated by TicketCode
```

The purpose of this note is reconstructibility: future work should be able to recover why Tooling contains generic normalization dataflow, why `trim` belongs to external Ruleset knowledge, and why the namespace mismatch is a separate target-context experiment rather than a hidden part of normalization lowering.
