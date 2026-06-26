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
- `VSlices.Tooling` may call `VSlices.DocumentGeneration`.
- `VSlices.DocumentGeneration` must not depend on `VSlices.Tooling`.

## Current validation commands

- `dotnet restore`
- `dotnet build`
- `dotnet test`