# Agent instructions

- Prefer small, focused changes.
- Keep VSlices concepts explicit.
- Do not introduce abstractions before they are needed.
- Prefer errors as values over exceptions for expected failures.
- Preserve documentation and implementation continuity.
- Run `dotnet build` and relevant tests after code changes.
- For documentation, keep files focused and avoid unnecessary expansion.

## Repository layout

- `src/VSlices.Tooling` is the CLI execution adapter.
- `src/VSlices.DocumentGeneration` owns document generation behavior.
- `src/VSlices.Vsir` owns the experimental VSIR semantic model, parsing, and conservative validation used by tooling.
- `src/VSlices.Vsir.CSharp` owns deterministic C# projection and the current experimental deterministic rebase behavior.
- `src/VSlices.Targets.DotNet` owns .NET target context and delegation to target-native tooling such as `dotnet`.
- `VSlices.Tooling` may call reusable behavior projects.
- Reusable behavior projects must not depend on `VSlices.Tooling`.
- Target-specific tooling should not define VSIR semantics; it may only resolve or materialize target context needed by lowering.

## Current validation commands

- `dotnet restore`
- `dotnet build`
- `dotnet test`
