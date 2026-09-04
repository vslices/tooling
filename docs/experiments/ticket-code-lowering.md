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

  -> real Ruleset still has no intrinsic.trim
  -> expected next consumer boundary: CSL031
```

## Experimental prioritization

Evidence determines what the current machinery can justify and where the observed boundary lies. It does not uniquely determine which unresolved boundary must be investigated next.

Human maintainer interest may therefore prioritize which evidence-compatible experiment is run next. That interest may order the research agenda, but it does not redefine consumer semantics, move a failure to a preferred layer, or justify implementation without discriminating evidence.

`TicketCode` was selected partly because normalization is an interesting next boundary after `TicketId`; the observed failures themselves still come from real consumer executions.

## Current finding

The current Ruleset primitive is already expressive enough for the observed normalization.

A deterministic expression rule of the form:

```yaml
- node: intrinsic.trim
  mode: deterministic
  renderer: expression
  template: "{value}.Trim()"
```

can be consumed by Tooling without a new renderer mode.

Tooling now processes construction steps in order and keeps a reference-to-expression environment. A successful normalize rule replaces the current expression for its target, and later ensures plus final state construction consume that updated expression.

For `TicketCode`, a test-only trim rule therefore produces semantics equivalent to:

```text
ensure non-empty(input.Value.Trim())
state.Value <- input.Value.Trim()
```

The production Ruleset remains intentionally unchanged so the next real consumer execution can test whether the boundary has moved cleanly into Ruleset knowledge.

## Expected next observation

```text
vslices lower TicketCode
  -> CSL031: No deterministic C# normalization rule is available for 'intrinsic.trim'.
```

If observed, the ownership split is demonstrated as:

```text
normalization dataflow / execution mechanism -> Tooling
trim realization knowledge                 -> Ruleset
```

Only then should the existing `vslices/ruleset:experiment/ticket-code-lowering` branch receive `intrinsic.trim`.

## Non-scope continuity

All prior explicit non-scope remains inherited except for the narrow boundary moved by direct TicketCode evidence.

Still out of scope:

```text
new VSIR concepts or classifications beyond the observed normalize semantic
new Ruleset renderer modes
new target languages
interpretate
Git-history ancestry reconstruction
general provenance graph
aggregate update semantics
namespace path policy
configurable terminal themes
new lineage or rebase semantics
normalization semantics beyond deterministic expression transforms demonstrated by TicketCode
```
