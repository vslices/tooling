# TicketCode lowering experiment

This branch exercises the current VSIR lowering boundary against the real `TicketCode` specimen from Ticket Support.

## Baseline

The experiment began with the real consumer stopping while parsing `construction.steps[].normalize`:

```text
VSIR100: Only construction step 'ensure' is supported by the experimental parser.
```

That established semantic representation/parsing as the first boundary.

After representing `normalize` explicitly and keeping lowering fail-closed, the next real consumer run produced:

```text
CSL030: Construction step 'normalize' is represented but not supported by the current C# lowerer (intrinsic 'trim', target 'input.Value').
```

That moved the boundary from VSIR representation into C# lowering mechanics.

## Experimental prioritization

Evidence determines what the current machinery can justify and where the observed boundary lies. It does not uniquely determine which unresolved boundary must be investigated next.

Human maintainer interest may therefore prioritize which evidence-compatible experiment is run next. That interest may order the research agenda, but it does not redefine consumer semantics, move a failure to a preferred layer, or justify implementation without discriminating evidence.

`TicketCode` was selected partly because normalization is an interesting next boundary after `TicketId`; the `VSIR100` and `CSL030` observations themselves still come from the real consumer executions.

## Current experiment

The current question is whether normalization needs a new Ruleset execution primitive or whether the existing deterministic `expression` renderer is already sufficient once Tooling provides reusable normalization dataflow.

Tooling now processes construction steps in order and carries a reference-to-expression environment through the lowering pipeline.

For a normalize step:

```yaml
- normalize:
    target: input.Value
    intrinsic: trim
```

Tooling asks the existing Ruleset expression renderer for:

```text
node: intrinsic.trim
bindings:
  value: <current expression for input.Value>
```

If the rule exists, the returned expression becomes the new expression for that target and is used by later ensures and final state construction. This preserves construction ordering without encoding `Trim()` or another target-specific operation directly in Tooling.

A test-only Ruleset rule:

```yaml
- node: intrinsic.trim
  mode: deterministic
  renderer: expression
  template: "{value}.Trim()"
```

demonstrates that the existing rule primitive is sufficient for the currently observed TicketCode semantics. With only that test rule added, the generated witness applies the normalized expression both to the following `non-empty` ensure and to final state construction.

No new renderer mode is introduced, and the repository Ruleset intentionally still does **not** contain `intrinsic.trim`.

## Expected next observation

Running:

```text
vslices lower TicketCode
```

against the real Ticket Support project and its current Ruleset should now stop with:

```text
CSL031: No deterministic C# normalization rule is available for 'intrinsic.trim'.
```

If that occurs, the experiment has discriminated the two layers:

```text
normalization dataflow / execution mechanism -> Tooling, demonstrated
trim realization knowledge                 -> Ruleset, still missing
```

At that point the next justified repository change belongs to the existing `vslices/ruleset:experiment/ticket-code-lowering` branch: add only the demonstrated `intrinsic.trim` target knowledge, then re-run the real consumer again.

## Non-scope continuity

All prior explicit non-scope remains inherited except for the narrow boundary moved by direct TicketCode evidence.

Still out of scope here:

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
