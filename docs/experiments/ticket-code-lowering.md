# TicketCode lowering experiment

This branch exercises the current VSIR lowering boundary against the real `TicketCode` specimen from Ticket Support.

## Baseline

Before this experiment the CLI stopped while parsing `construction.steps[].normalize`:

```text
VSIR100: Only construction step 'ensure' is supported by the experimental parser.
```

That established semantic representation/parsing as the first boundary. Ruleset knowledge, C# lowering and lineage had not yet been reached.

## Current experiment

`normalize` is now preserved explicitly as a construction step with:

```yaml
- normalize:
    target: input.Value
    intrinsic: trim
```

The parser remains fail-closed for unknown nested normalize semantics, and validation currently requires normalize targets to refer to known construction input fields.

C# lowering deliberately does not implement normalization yet. When a represented normalize step reaches the C# lowerer it stops explicitly with `CSL030` instead of silently generating a witness that omits the normalization.

No `trim` rule has been added to Ruleset in this step. The next CLI execution against the consumer project is intended to establish the next empirical boundary before deciding whether the missing capability belongs to lowering mechanism, Ruleset knowledge, or both.

## Expected next observation

Running:

```text
vslices lower TicketCode
```

against the Ticket Support consumer should now pass parsing/validation and stop in C# lowering with `CSL030`, naming `trim` and `input.Value`. If it stops elsewhere, that discrepancy becomes the next evidence to investigate rather than being papered over.
