# Normalize / Ruleset boundary experiment

## Question

Can target-specific Ruleset knowledge cause the CLI to accept a `normalize` intrinsic that VSIR does not recognize?

The expected answer is **no**.

The previous TicketCode experiment established this responsibility split:

```text
VSIR
  -> recognizes normalization semantics

Tooling lowering mechanism
  -> preserves ordered normalization dataflow

Ruleset
  -> realizes already-recognized semantics for a target
```

This experiment tests that split through the CLI rather than only through parser/lowerer unit tests.

## Why this is the next boundary

`TicketCode` demonstrated the positive path for `normalize: trim`:

1. VSIR represents `trim`.
2. Tooling can carry the normalized value forward.
3. Missing C# target knowledge produces `CSL031`.
4. Adding `intrinsic.trim` to the Ruleset completes lowering.

That proves that Ruleset can complete target realization for known semantics. It does not by itself prove that Ruleset cannot accidentally extend the VSIR semantic vocabulary.

## Probe

Use an intentionally unknown normalization intrinsic in a VSIR fixture and provide an adversarial Ruleset fixture containing a renderer with the matching node name.

The adversarial rule is experiment-only knowledge. It must not be added to the production Ruleset manifest.

Expected CLI behavior:

```text
parse / semantic validation
  -> VSIR221 unknown normalize intrinsic
  -> stop
  -> no lowering
  -> adversarial Ruleset renderer is irrelevant
```

A successful lowering, a missing-rule diagnostic such as `CSL031`, or any target code containing the adversarial renderer would falsify the intended authority boundary.

## Acceptance criteria

- exercise the real CLI command path;
- install/use a Ruleset fixture that contains a matching adversarial renderer;
- invoke lowering/transpilation on VSIR containing the unknown normalize intrinsic;
- observe `VSIR221`;
- produce no C# materialization;
- demonstrate that adding target knowledge cannot create a semantic operation unknown to VSIR.

## Non-goals

- invent a new product normalization semantic;
- add an unobserved normalize intrinsic to canonical VSIR;
- add experiment-only rules to the production Ruleset;
- generalize the Ruleset renderer model beyond evidence from the probe.

## Evidence source

The current consumer corpus still provides `trim` as the observed normalization intrinsic. Richer artifacts such as `EmailAddress` create later boundaries through validation and equality semantics, not a second observed normalize intrinsic. Those should remain separate experiments.
