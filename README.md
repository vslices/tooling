# VSlices Tooling

VSlices Tooling is the executable tooling surface of the VSlices suite.

Its purpose is to provide repeatable mechanisms around VSlices artifacts while keeping revisable semantic and lowering knowledge outside the executable whenever possible.

The current CLI is named `vslices`.

## Current responsibilities

The repository currently contains tooling for:

- structured document generation for VSlices documentation;
- parsing and validating the experimental VSIR surface used by current benchmarks;
- deterministic VSIR-to-C# transpilation for supported structures;
- conservative semantic rebase experiments over human-edited materializations;
- .NET target-context discovery;
- project-local ruleset discovery and initialization.

The commands currently being explored include:

```text
vslices init
vslices transpile
vslices rebase
```

`interpretate` and `lower` are design directions, not yet stable commands.

## Tooling vs. lowering knowledge

A central architectural boundary is:

```text
vslices executable
  = execution and orchestration mechanisms

vslices/ruleset
  = official, revisable lowering knowledge

project/.vslices/ruleset
  = local ruleset snapshot used by a project

.vsir
  = semantic source

.vsir.cs
  = editable materialization
```

Concrete target lowering mappings should not be embedded in the CLI when they can be expressed as external rules. The executable may know how to execute supported classes of rules, but the rules themselves belong in the ruleset.

A missing rule is not permission to guess. Unsupported lowering remains explicit.

See `docs/rulesets.md` for the current experimental contract.

## VSIR lowering model

VSIR does not define one privileged source-code rendering. It constrains the space of acceptable materializations.

For an implementation `I` and VSIR document `V`, the intended relation is:

```text
I satisfies V
```

A deterministic transpiler constructs one valid materialization when the lowering knowledge is complete enough. Human edits remain legitimate as long as the resulting source continues to satisfy the VSIR contract.

Semantic rebase is being explored for the case where VSIR evolves after a generated materialization has already been edited by a human.

## Ruleset initialization

`vslices init` materializes a ruleset under:

```text
.vslices/ruleset/
```

The current implementation accepts a local ruleset directory or an HTTP(S) ZIP source. The official ruleset lives in `vslices/ruleset`; integrating it as the normal bootstrap source is part of the current iteration.

Once initialized, transpilation should be able to operate from local state without requiring network access.

## Distribution direction

The CLI is intended to remain lightweight. Native AOT is the preferred distribution direction so `vslices` can be shipped as a small self-contained executable while configuration and lowering knowledge remain external.

This is a deployment goal rather than a semantic constraint on the tooling design.

## Validation strategy

The current benchmarks begin with `StreetName.vsir` and progressively introduce new VSIR structures only when concrete examples require them.

Important properties to preserve include:

- deterministic output for the same VSIR, ruleset, and target context;
- no hidden fallback when a rule is absent;
- the ability to change lowering behavior through the external ruleset without recompiling the CLI;
- offline operation after initialization;
- technical validation through build/test and target-specific tooling;
- semantic verification as a distinct concern from compilation.

## Long-term dogfooding objective

A long-term objective is incremental semantic self-hosting: whenever VSlices claims it can represent a kind of software concept, the tooling itself should become a candidate for expressing its own instances of that concept through `.vsir` artifacts.

This does not mean every line of VSlices Tooling must be generated. The stronger goal is that representable semantics inside the tooling are described and maintained using the same VSIR and lowering mechanisms provided to other projects.

The tooling can then serve simultaneously as a dogfooding target, conformance corpus, and source of evidence about gaps in VSIR.

## Status

VSIR lowering and ruleset support are experimental. The repository should prefer small, evidence-driven extensions over speculative generalization.
