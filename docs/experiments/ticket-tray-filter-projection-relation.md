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

## Success criterion

The first iteration succeeds when the CLI exposes the earliest unsupported boundary for `TicketTrayFilter` without inventing semantics, and the result is specific enough to design the next discriminating experiment.
