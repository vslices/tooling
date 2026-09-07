# VSlices.Vsir

`VSlices.Vsir` owns the experimental semantic model, parsing and conservative validation of VSIR.

For the current `.vsir` file contract, read [`SPECIFICATION.md`](SPECIFICATION.md) first.

That specification is the semantic entrypoint for humans and AI agents. It is intended to be sufficient to reconstruct the meaning and preferred normalized structure of a `.vsir` file without chat history or consumer-specific migration notes.

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

SPECIFICATION.md
  = current reconstructible file semantics and preferred normalized structure

VSlices.Vsir model/parser/validator/tests
  = executable support for the specification

Ruleset
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
2. Read [`SPECIFICATION.md`](SPECIFICATION.md).
3. Read the model, parser, validator and tests relevant to the case.
4. If the work originates in a consumer, inspect the real `.vsir`, `.vsir.cs`, surrounding source/tests, Ruleset/extensions and target context.
5. Establish semantics before changing syntax.
6. Prefer repeated evidence from a real corpus over a one-off abstraction.
7. Update the specification when the semantic contract changes.
8. Then update executable support with the smallest coherent mechanism.
9. Rerun lowering/build/tests and observe the next unsupported boundary.

Use this evolution loop:

```text
real artifact
  -> reconstruct semantics
  -> normalize repeated structure
  -> identify irreducible distinctions
  -> update specification
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

The goal is for `SPECIFICATION.md` plus executable support to gradually replace scattered historical VSIR notes as the knowledge required to understand and reconstruct `.vsir` files.
