# Agent instructions

- Prefer small, focused changes.
- Keep VSlices concepts explicit.
- Do not introduce abstractions before they are needed.
- Prefer errors as values over exceptions for expected failures.
- Preserve documentation and implementation continuity.
- Inspect the actual current branch/HEAD and repository evidence before relying on remembered state.
- Run `dotnet build` and relevant tests after code changes.
- For documentation, keep files focused and avoid unnecessary expansion.

## Required orientation for semantic work

Before changing VSIR parsing, lowering, target context, rulesets, or command semantics, read [`docs/ai-development-orientation.md`](docs/ai-development-orientation.md).

When work originates in a VSlices-enabled consumer repository, begin from the concrete consumer evidence (`.vsir`, `.vsir.cs`, `.vslices/config.yaml`, local `.vslices/ruleset`, surrounding source/tests and target context) before deciding that `vslices/tooling` must change.

Keep the causal chain reconstructible:

```text
consumer evidence
  -> semantic requirement
  -> VSIR/ruleset/tooling change
  -> validation evidence
```

Do not use conversation history as the only authority for a material design decision. If a decision changes an architectural or semantic boundary, update the closest repository documentation in the same change.

## Repository layout

- `src/VSlices.Tooling` is the CLI execution adapter.
- `src/VSlices.DocumentGeneration` owns document generation behavior.
- `src/VSlices.Vsir` owns the experimental VSIR semantic model, parsing, and conservative validation used by tooling.
- `src/VSlices.Vsir.CSharp` owns deterministic C# projection and the current experimental deterministic rebase behavior.
- `src/VSlices.Targets.DotNet` owns .NET target context and delegation to target-native tooling such as `dotnet`.
- `VSlices.Tooling` may call reusable behavior projects.
- Reusable behavior projects must not depend on `VSlices.Tooling`.
- Target-specific tooling should not define VSIR semantics; it may only resolve or materialize target context needed by lowering.

## Ruleset boundary

- Target-specific lowering knowledge is external project state under `.vslices/ruleset`, not embedded CLI knowledge.
- The CLI may know how to discover, initialize, and orchestrate a ruleset, but it should not hardcode individual target lowering expressions when those can be represented by the ruleset.
- Missing lowering knowledge is a stop condition. Do not guess a materialization merely because a target language can express one.
- The project-local ruleset is intended to be editable and version-controlled. Updating lowering knowledge should not require republishing the CLI.
- The current manifest and rule formats are experimental. Evolve them from concrete lowering needs rather than prematurely generalizing them.

## Current semantic direction

`v0.2.0-preview` expands real VSIR coverage and classifies the resulting lowering needs before introducing stronger mechanisms.

`interpretate` is not a mandatory roadmap feature. It should emerge only when a concrete case remains genuinely underdetermined but constrained after VSIR semantics, ruleset knowledge, project evidence and target-native authority have been considered.

The authority rule remains:

> Interpretation may resolve underdetermined materialization. Interpretation must not manufacture missing authority.

## Current validation commands

- `dotnet restore`
- `dotnet build`
- `dotnet test`
