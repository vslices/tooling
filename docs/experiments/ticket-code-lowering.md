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

For `TicketCode`, a test-only trim rule therefore produces semantics equivalent to:

```text
ensure non-empty(input.Value.Trim())
state.Value <- input.Value.Trim()
```

The real consumer subsequently confirmed the boundary. With Tooling `build5.264` and the production Ruleset still lacking `intrinsic.trim`, running:

```text
vslices lower TicketCode
```

returned:

```text
CSL031: No deterministic C# normalization rule is available for 'intrinsic.trim'.
```

This demonstrates the ownership split:

```text
normalization dataflow / execution mechanism -> Tooling
trim realization knowledge                 -> Ruleset
```

The corresponding Ruleset change is isolated in `vslices/ruleset#2` and adds only the demonstrated `intrinsic.trim` expression rule.

## Next observation

After the consumer updates its local Ruleset snapshot from Ruleset PR #2:

```text
vslices update
vslices lower TicketCode
```

The expected outcome is either a deterministic `TicketCode` witness or a new independent boundary. A new failure must remain an observation to classify rather than justification to expand this experiment preemptively.

## Non-scope continuity

All prior explicit non-scope remains inherited except for the narrow boundary moved by direct TicketCode evidence.

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
namespace path policy
configurable terminal themes
new lineage or rebase semantics
normalization semantics beyond deterministic expression transforms demonstrated by TicketCode
```

The purpose of this note is reconstructibility: future work should be able to recover why Tooling contains generic normalization dataflow while the actual `trim` realization remains external Ruleset knowledge.
