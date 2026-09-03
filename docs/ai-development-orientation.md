# AI development orientation

This document is the shortest reconstructible path for an AI-assisted development session that needs to analyze a VSlices-enabled project and decide whether a discovered need belongs in the consumer project, `vslices/tooling`, or `vslices/ruleset`.

It is not a substitute for repository evidence. It is a reading order and authority map intended to prevent a future session from rebuilding the current model from conversation history.

## Start here

Before changing VSlices behavior, inspect current repository state rather than relying on remembered conclusions.

For `vslices/tooling`, read in this order:

1. `README.md` — current product surface and authority boundaries.
2. `AGENTS.md` — repository-local editing constraints and project ownership.
3. `docs/releases/v0.2.0-preview.md` — current evolutionary direction.
4. `docs/context.vslices-tooling.md` — architectural context and long-lived responsibilities.
5. `docs/rulesets.md` — executable/ruleset boundary.
6. `docs/configuration.md` — project operating policy.
7. relevant implementation and tests for the concrete command or semantic structure being changed.

When the work originates in a consumer repository, inspect its `.vsir`, `.vsir.cs`, `.vslices/config.yaml`, and `.vslices/ruleset` before modifying Tooling.

## Cross-repository authority map

The current separation is:

```text
consumer project
  = domain/software evidence and concrete VSIR examples

.vsir
  = semantic source

.vsir.cs
  = human-editable materialization constrained by VSIR

vslices/ruleset
  = official revisable target lowering knowledge

consumer/.vslices/ruleset
  = local ruleset snapshot actually used by lowering

vslices/tooling
  = parsing, validation, lowering mechanisms, orchestration,
    target adapters, CLI behavior and operational guarantees

target-native tooling
  = authoritative target facts where available
    (for .NET: dotnet/MSBuild/Roslyn/etc.)
```

Do not move knowledge across these boundaries merely because one repository is easier to edit.

## Decision procedure for a new VSIR case

When a concrete project exposes a new concept or lowering problem, proceed in this order:

```text
1. establish the source behavior and semantics from project evidence
2. determine whether the existing VSIR can represent them faithfully
3. if representation is insufficient, extend VSIR/tooling semantics from the concrete case
4. if representation is sufficient, test whether the local ruleset can lower it
5. if only target mapping knowledge is missing, change vslices/ruleset
6. if a new execution mechanism is required to express an authorized rule, change vslices/tooling
7. delegate target-owned decisions to target tooling before duplicating them
8. classify any residual freedom
```

Residual materialization freedom should currently be classified as one of:

```text
deterministic
rebase-compatible
underdetermined but constrained
unsupported / missing authority
```

Do not introduce interpretive lowering merely because a deterministic rule has not yet been written.

## Core semantic rules

The current model assumes:

```text
CSharpImplementation |= VSIR
```

A transpiled file is one valid witness, not the only valid implementation.

`.vsir.cs` is human-editable source under semantic contract, not disposable generated output.

The main authority rule is:

> Lowering may complete implementation detail. Lowering must not complete missing semantics.

For future interpretive work:

> Interpretation may resolve underdetermined materialization. Interpretation must not manufacture missing authority.

Therefore:

```text
missing deterministic rule
  != permission for an AI to guess

missing authority
  -> stop
```

## `v0.2.0-preview` working direction

The current preview line intentionally develops along two tracks:

```text
CLI experience
  -> identity, presentation, progress and operability

semantic capability
  -> broader real-world VSIR coverage
     -> classify new lowering needs
     -> extend deterministic mechanisms where possible
     -> discover interpretive need only from concrete evidence
```

`vslices interpretate` is a possible future surface, not a feature that must be invented to satisfy the version number.

A candidate interpretive case must remain genuinely underdetermined after VSIR semantics, ruleset knowledge, project evidence and target-native authority have all been considered.

## Consumer-project analysis protocol

For a repository such as `atom-dev-serviu/account-management-product`, an AI-assisted session should not begin by editing VSlices repositories.

First collect concrete examples:

- current `.vsir` documents;
- their hand-written or transpiled `.vsir.cs` materializations;
- surrounding source behavior and tests;
- the project's `.vslices/config.yaml`;
- the project's local `.vslices/ruleset`;
- relevant VSIR documentation in the consumer project;
- target context such as `.csproj`, namespace/folder conventions, compilation behavior and tests.

For each example, record the gap as one of:

```text
semantic representation gap
validation/parsing gap
ruleset knowledge gap
target-context gap
lowering mechanism gap
rebase/provenance gap
presentation-only gap
no gap
```

Only then choose the repository to change.

## How to decide which repository changes

Prefer changing the consumer project when the new information is specific to that project's domain or conventions.

Prefer changing `vslices/ruleset` when VSIR already carries enough semantics and the missing piece is target lowering knowledge executable by existing mechanisms.

Prefer changing `vslices/tooling` when the parser/model cannot represent the semantic structure, when a new generic execution primitive is required, when orchestration/safety behavior changes, or when target context requires a reusable adapter capability.

A concrete case may legitimately require coordinated changes in more than one repository. When that happens, keep the causal chain explicit: project evidence -> semantic requirement -> mechanism/rule change -> validation evidence.

## Evidence expectations

Before promoting a new capability, seek evidence that distinguishes semantics from one convenient implementation.

Useful checks include:

- same VSIR + same ruleset + same target context => same deterministic output;
- removing a required rule stops explicitly;
- changing an external rule can alter lowering without rebuilding the CLI;
- human edits compatible with VSIR remain legitimate;
- target-native tooling is consulted where it owns the fact;
- redirected CLI output remains machine-safe;
- Native AOT and command-level smoke tests still pass when CLI behavior changes.

If evidence is incomplete, document the claim as a hypothesis rather than silently treating it as architecture.

## Continuity rule for future chats

A future AI session should be able to reconstruct the working model from repositories alone.

When a material decision changes one of these boundaries, update the closest authoritative document in the same change. Do not depend on a chat transcript to preserve:

- command semantics;
- version direction;
- ownership between tooling and ruleset;
- VSIR conformance assumptions;
- interpretive authority rules;
- target-tool delegation rules;
- validation expectations.

Conversation history may explain why a decision happened, but repository artifacts must remain sufficient to discover what is currently accepted.