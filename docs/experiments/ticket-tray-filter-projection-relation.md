# TicketTrayFilter projection relation experiment

This branch continues the Ticket Support post-migration reconstruction with the first real specimen whose semantic representation and target-facing representation are intentionally non-isomorphic.

The experiment follows the repository rule:

> change only the first layer that can no longer justify the result, then rerun the real consumer before generalizing further.

## Consumer evidence

The Ticket Support specimen declares semantic representation coordinates such as:

```yaml
representation:
  Search: Option<TicketSearch.Repr>
  ProjectReference: Option<ProjectReference.Repr>
  IncidentTypeReference: Option<IncidentTypeReference.Repr>
  ResponsibleReference: Option<AccountReference.Repr>
  Risk: Option<string>
  Module: Option<string>
  Dates: Option<TicketDateRange.Repr>
```

The current Query-facing C# representation instead contains, among other coordinates:

```csharp
string? ProjectReference
string? IncidentTypeReference
string? ResponsibleReference
```

That difference is evidence to investigate, not permission for Tooling to flatten nominal representations automatically.

## Research question

Can VSlices Tooling lower a semantic representation into an intentionally non-isomorphic C# representation only when an explicit projection relation justifies the structural change, while preserving the distinction between:

1. VSIR semantic representation,
2. target-specific lowering knowledge,
3. the lowering mechanism,
4. target context, and
5. the editable human witness?

## Project lowering enters scope

PR #6 recorded project/folder/batch lowering as future work. This experiment deliberately takes the first, narrowest part of that item into scope: **complete .NET project lowering**.

The command keeps the existing subject-oriented CLI shape:

```text
vslices lower Identities.Domain
```

A lower subject may now resolve to either:

```text
VSIR artifact
.NET project (.csproj)
```

When an extensionless symbol resolves to both, Tooling must not guess:

```text
vslices lower Identities.Domain
  -> ambiguous

vslices lower Identities.Domain.vsir
  -> artifact

vslices lower Identities.Domain.csproj
  -> project
```

Project lowering establishes one coherent lowering environment before processing its artifacts:

```text
project
  -> VSlicesProjectContext
  -> configured target
  -> installed Ruleset
  -> project extension overlay
  -> enumerate project .vsir artifacts
  -> lower each artifact through the existing artifact mechanism
```

The purpose of this first project-level surface is both operational and experimental: existing projects can expose which VSIR artifacts the current semantic/lowering surface already accepts and which explicit boundary each remaining artifact reaches.

An unsupported artifact therefore does not cause supported siblings to be abandoned. The project run reports the per-artifact boundary and a summary while preserving fail-closed behavior inside every individual artifact.

Artifact-specific overrides (`--from`, `--source`, `--output`, `--stdout`, `--namespace`) are not generalized to project semantics by this change. They remain individual-artifact surfaces until concrete evidence establishes meaningful batch behavior.

### Still outside the project-lowering slice

```text
--path / folder-scoped lowering
atomic batch materialization
multi-project / solution lowering
cross-project dependency orchestration
```

The intended future module syntax is recorded only as direction, not implemented here:

```text
vslices lower Identities.Domain --path ValueObjects
```

## First-boundary protocol

Run the real `TicketTrayFilter.vsir` through the current CLI and stop at the first boundary that cannot justify the next result.

Classify the boundary before changing code:

```text
semantic representation
parsing / validation
ruleset knowledge
target context
lowering mechanism
rebase / provenance
consumer-only
```

Expected discriminating outcomes:

- If `Option<T>` or nominal `.Repr` forms cannot be represented faithfully, the first gap is VSIR/model/parsing support.
- If a projection relation is represented but no C# realization is known, the first gap is Ruleset knowledge.
- If the Ruleset can state the realization but the C# projector cannot execute that primitive, the first gap is lowering mechanism.
- If Tooling silently turns `Option<X.Repr>` into `X?` or `string?`, the experiment fails semantic conservation.
- If more than one realization is plausible and no authority distinguishes them, Tooling must stop rather than choose one.

## Projection relation hypothesis

`flatten-single-field` is retained only as a candidate relation from the Ticket Support post-migration analysis. It is not accepted here as canonical syntax, VSIR semantics, Ruleset vocabulary or C# behavior.

Before promoting it, the experiment must establish at least:

- what semantic fact authorizes flattening;
- whether the relation belongs to VSIR or is target-specific knowledge;
- how optionality composes with the relation;
- how the relation behaves for a nominal representation with more than one field;
- how the CLI diagnoses an absent, ambiguous or unsupported relation.

## Ruleset gate

Do not add an executable projection rule merely to make `TicketTrayFilter` pass.

A Ruleset change becomes justified only after Tooling can represent the relevant source and projection relation faithfully and the real consumer reaches a missing-target-knowledge boundary.

The companion branch in `vslices/ruleset` records the same evidence gate without pre-authorizing a projection primitive.

## Explicit non-scope inherited from PR #6

The PR #6 baseline was:

```text
implicit semantics from renderer lookup
a universal semantic plugin system
multi-target execution
extension support for ensure/equality/invariants/features
behavioral-equivalence claims across targets
arbitrary executable extensions
purity/determinism/idempotence metadata without evidence
project/folder/batch lowering
```

This experiment has now taken **project lowering only** out of the final item. Folder/path-scoped lowering and stronger batch semantics remain outside the current slice as described above.

## Nominal C# type resolution

`SrvIdentityId` supplied a concrete target-context gap after its VSIR semantics became representable: the semantic type name `Rut` is sufficient inside VSIR, but C# needs a target symbol such as `Shared.Domain.ValueObjects.Rut` to compile.

This does not introduce a semantic mapping such as `Rut -> string`. The authority split is:

```text
VSIR
  -> semantic nominal type name: Rut

.NET target context
  -> target symbol: Shared.Domain.ValueObjects.Rut
```

Tooling delegates that lookup to the existing Roslyn/MSBuild companion against the related `.csproj` and its referenced assemblies. Missing and ambiguous target symbols fail closed.

The preferred materialization policy is readability-first:

```csharp
using Shared.Domain.ValueObjects;

// ...
private readonly Rut _value;
```

rather than eagerly emitting:

```csharp
private readonly global::Shared.Domain.ValueObjects.Rut _value;
```

`global::` is intentionally reserved as a future conflict-resolution fallback. If real consumer evidence shows that the preferred imports create an unavoidable simple-name collision, Tooling may qualify only the conflicting references while keeping `using` + short names as the normal form. That fallback is not introduced pre-emptively in this slice.

## Success criterion

The first projection iteration succeeds when the CLI exposes the earliest unsupported boundary for `TicketTrayFilter` without inventing semantics, and the result is specific enough to design the next discriminating experiment.

The project-lowering iteration succeeds when a real existing project can be used as a coverage probe: supported artifacts use the existing lowering/lineage behavior, unsupported artifacts remain explicit, and the project shares one prepared Ruleset/extension/target environment rather than rediscovering semantic authority independently for every file.
