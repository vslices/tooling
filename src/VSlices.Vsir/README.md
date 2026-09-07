# VSlices.Vsir

`VSlices.Vsir` owns the experimental executable model, parsing and conservative validation support for VSIR inside Tooling.

The language-level VSIR specification does **not** live in this repository.

For the current `.vsir` file contract, read:

- [`vslices/intermediate-representation/SPECIFICATION.md`](https://github.com/vslices/intermediate-representation/blob/main/SPECIFICATION.md)

That repository owns the intended VSIR language semantics and reconstructible file contract. This project is an executable consumer/implementation of that specification.

The implementation may temporarily lag behind the specification while a semantic experiment is being consolidated. When that happens, keep the discrepancy explicit rather than treating old parser support as the conceptual ceiling or rewriting preferred corpus semantics solely for compatibility.

## Authority

Keep these boundaries distinct:

```text
consumer project
  = concrete domain/software evidence

.vsir
  = semantic source for the represented artifact

.vsir.cs
  = human-editable executable witness constrained by VSIR

vslices/intermediate-representation
  = VSIR language semantics, reconstructible file contract and conformance expectations

VSlices.Vsir model/parser/validator/tests
  = executable Tooling support for that specification

vslices/ruleset
  = revisable target-lowering knowledge

Tooling lowering mechanisms
  = constrained execution of authorized lowering knowledge

target-native tooling
  = authoritative target facts
```

A useful conformance relation remains:

```text
ConcreteImplementation |= VSIR
```

A deterministic materialization is one valid witness. It is not necessarily the only valid source form.

The lowering boundary remains:

> Lowering may complete implementation detail. Lowering must not complete missing semantics.

## Working procedure

When changing VSIR semantics:

1. Read the current branch/HEAD and repository instructions.
2. Read the current specification in `vslices/intermediate-representation`.
3. Read the model, parser, validator and tests relevant to the case.
4. If the work originates in a consumer, inspect the real `.vsir`, `.vsir.cs`, surrounding source/tests, Ruleset/extensions and target context.
5. Establish semantics before changing syntax.
6. Prefer repeated evidence from a real corpus over a one-off abstraction.
7. Update `vslices/intermediate-representation` when the language contract changes.
8. Then update executable Tooling support with the smallest coherent mechanism.
9. Rerun lowering/build/tests and observe the next unsupported boundary.

Use this evolution loop:

```text
real artifact
  -> reconstruct semantics
  -> normalize repeated structure
  -> identify irreducible distinctions
  -> update VSIR specification
  -> update model/parser/validator/tests
  -> lower real artifacts
  -> observe next boundary
```

Unknown or underdetermined semantics must remain visible. Do not add compatibility aliases merely because historical syntax existed, and do not introduce interpretation merely because deterministic lowering knowledge is missing.

## Related orientation

For repository-wide ownership, command orchestration, Rulesets, project extensions, lineage and target adapters, read:

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../../docs/ai-development-orientation.md`](../../docs/ai-development-orientation.md)
- [`../../README.md`](../../README.md)
