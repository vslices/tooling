# Agent instructions

- Prefer small, focused changes.
- Keep VSlices concepts explicit.
- Do not introduce abstractions before they are needed.
- Prefer errors as values over exceptions for expected failures.
- Preserve documentation and implementation continuity.
- Inspect the actual current branch/HEAD and repository evidence before relying on remembered state.
- Run `dotnet build` and relevant tests after code changes.
- For documentation, keep files focused and avoid unnecessary expansion.

## Required orientation

Before changing VSIR parsing, lowering, target context, rulesets, project extensions, project orchestration or command semantics, read [`docs/ai-development-orientation.md`](docs/ai-development-orientation.md).

When work originates in a consumer repository, begin from concrete consumer evidence (`.vsir`, `.vsir.cs`, `.vslices/config.yaml`, local `.vslices/ruleset`, `.vslices/extensions`, surrounding source/tests and target context) before deciding which repository must change.

Keep the causal chain reconstructible:

```text
consumer evidence
  -> semantic requirement
  -> VSIR / extension / ruleset / tooling change
  -> validation evidence
```

## Command architecture

Command handlers are adapters. They should parse the CLI request, invoke an operation/coordinator and present/write the result.

Do not place project discovery, semantic parsing, extension/ruleset loading, lineage policy, rebase policy and persistence together in a public command handler. Do not call one command handler from another to share behavior; share lower-level operations instead.

Current intended boundaries:

```text
Commands
  -> TranspilationOperation / RebaseOperation / LoweringCoordinator
  -> Project / Rulesets / Lineage infrastructure
  -> VSIR / target mechanisms
```

A behavior extracted from `lower` does not automatically deserve a public command.

## Project context

`VSlicesProjectContext` is the single representation of a detected VSlices project. Reuse it for project root, `.vslices`, configuration, installed ruleset, project extensions and lineage paths rather than reconstructing parent relationships independently.

## Repository layout

- `src/VSlices.Tooling` is the CLI execution adapter and orchestration project.
- `src/VSlices.DocumentGeneration` owns document generation behavior.
- `src/VSlices.Vsir` owns the experimental VSIR semantic model, parsing and conservative validation.
- `src/VSlices.Vsir.CSharp` owns deterministic C# projection and deterministic rebase behavior.
- `src/VSlices.Targets.DotNet` owns .NET target context and delegation to target-native tooling.
- reusable behavior projects must not depend on `VSlices.Tooling`.
- target-specific tooling must not define VSIR semantics.

Do not fragment these assemblies merely for symmetry. Extract facets only after concrete complexity requires it.

## Ruleset and project-extension boundary

- `.vslices/ruleset` is the installed, source-owned lowering-knowledge snapshot. `init --force` and `update --ruleset` may replace it atomically.
- `.vslices/extensions` is a project-owned semantic-extension overlay. Ruleset installation/update must not replace or mutate it.
- Project extensions must be explicitly referenced from `.vslices/extensions/manifest.yaml`; merely placing a renderer somewhere never grants semantic validity.
- A project extension may co-locate semantic admission metadata with zero or more target realizations, but those remain separate authorities.
- Target lowering knowledge belongs in the installed Ruleset or an explicitly admitted project extension realization, not embedded CLI expressions when an external rule can represent it.
- Missing lowering knowledge is a stop condition.
- `init` and `update --ruleset` share source materialization/snapshot installation.
- Validate a prepared Ruleset snapshot with the real target loader before replacing the current snapshot.

## Semantic conservation

Unknown keys and structurally invalid nodes in known semantic mappings must fail closed. Do not use type-filtering iteration (`OfType<T>()`) where a malformed YAML node would silently disappear.

Do not apply fixed-key validation to mappings whose keys are user-defined semantic data such as `state`, `representation`, or `construction.input`.

The VSIR document and its validation environment are distinct facts. Project-admitted vocabulary belongs in `VsirValidationContext`, not in `DomainTypeVsir`.

Traits are unordered capabilities. Duplicate traits are invalid unless future evidence changes that contract.

## Lineage

Lineage bootstrap is non-destructive. An authorized existing materialization causes Tooling to compute/store the current deterministic projection as baseline while preserving the human witness byte-for-byte. It must not treat the human file itself as deterministic ancestry.

`.vslices/lineage` is operational evidence and is intended to be version-controlled by default. It is not semantic authority.

## Validation

Run the solution build/tests and preserve CLI smoke coverage for project extensions, `update --ruleset`, lineage bootstrap/rebase and Native AOT behavior.

Current commands:

```text
dotnet restore
dotnet build tooling.slnx --configuration Release
dotnet test tooling.slnx --configuration Release --no-build
```
