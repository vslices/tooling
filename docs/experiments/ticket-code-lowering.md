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

## Current experiment

The next question is whether normalization needs a new Ruleset execution primitive or whether the existing deterministic `expression` renderer is already sufficient once Tooling provides reusable normalization dataflow.

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

If the rule exists, the returned expression becomes the new expression for that target and is used by later ensures and final state construction.

A test-only Ruleset rule:

```yaml
- node: intrinsic.trim
  mode: deterministic
  renderer: expression
  template: "{value}.Trim()"
```

demonstrates that no new renderer or execution primitive is required for the current TicketCode semantics: the existing expression renderer can produce both the normalized validation expression and normalized state construction.

The repository Ruleset intentionally still does **not** contain `intrinsic.trim`.

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
normalization dataflow / execution mechanism -> Tooling, now demonstrated
trim realization knowledge                 -> Ruleset, still missing
```

Only after that consumer observation should `intrinsic.trim` be added to the real Ruleset branch.

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

Human maintainer interest may prioritize which unresolved boundary is investigated next, but it does not redefine consumer semantics or move the observed technical boundary.
