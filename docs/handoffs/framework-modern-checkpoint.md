# Handoff — Framework modern checkpoint for VSlices Tooling

## Purpose

This document transfers the current Framework modernization work to the Tooling migration thread.

The objective is **not** to reproduce the old Framework API through compatibility aliases. The objective is to let real VSIR cases pressure Tooling against the current Framework model, preserving semantics where they still exist and leaving retired semantics retired.

The migration should continue with the normal VSlices discovery loop:

```text
real generated case
    -> first observable compilation/test boundary
    -> identify who owns that boundary
    -> discover the smallest truthful modern mechanism
    -> lower only that mechanism
    -> rerun
    -> observe the next boundary
```

Do not mass-rewrite the lowering vocabulary from a mapping table.

---

## Framework checkpoint

The current Framework checkpoint candidate is:

```text
vslices/framework
branch: experiment/transform-adapter-analyzer
candidate SHA: 92f6bc692a62695f628ec3f70036ef26e191d7b5
```

This SHA includes the final PointReader/AlgebraIO review described below.

Before pinning it permanently in Tooling, confirm the checkpoint CI for that exact SHA is green.

The Tooling migration branch currently pins the earlier executable-algebra checkpoint:

```text
6566c4222cb4ba9a0b31cafa2a92b670a199224c
```

The first mechanical step for the continuing migration thread should therefore be to move `src/framework` to the final green checkpoint SHA recorded above.

Do not merge Framework to `master` merely to make Tooling consume it. Tooling may intentionally pin this checkpoint while Framework evolution continues on its experimental branch.

---

## Current Framework model

### Space

The currently validated core semantic vocabulary includes:

```text
DiscreteSpace<SELF>
DerivedSpace<SELF, BASE>
MaintainedSpace<SELF>

Transformable<FROM, TO>
Validatable<CTX, SPACE>
Evolvable<SELF, STATE>
```

`TransformableM` and `ValidatableM` still exist but currently lack real consumer pressure. Do **not** make them Tooling lowering targets merely because they exist.

### Work

The Feature boundary is:

```csharp
Feature<F, ALG, REQ, RES>
    -> Flow<ALG, REQ, RES>
```

The Feature owns the Flow. The executable algebra is the Flow runtime.

There is no Free-monad Work program/interpreter layer in the current model.

A Work module owns a simple executable algebra record composed from capability atoms, for example:

```csharp
public sealed record TodoAlgebra(
    PointReader<Todo, TodoId> Reader,
    PointWriter<Todo> Writer,
    PointRemover<Todo, TodoId> Remover,
    TodoIdSource Ids);
```

### Point capability atoms

The current point vocabulary is:

```csharp
PointReader<POINT, ID>
PointWriter<POINT>
PointRemover<POINT, ID>
```

`PointReader` now has one required implementation operation:

```csharp
OptionT<IO, POINT> ReadOrDefault(ID id);
```

and derives:

```csharp
IO<POINT> Read(ID id)
```

with LINQ-like required-value semantics.

The intended law is:

```text
ReadOrDefault
    -> zero or one point
    -> absence remains an expected branch
    -> multiple points for one ID must fail the effect

Read
    -> exactly one point required
    -> absence fails exceptionally, analogous to Single vs SingleOrDefault
```

The default `Read` is also reachable from concrete implementations through an extension method. A concrete reader may define its own `Read`; that specialization must preserve required-read semantics and dispatches through the `PointReader<POINT,ID>` trait as well.

### Algebra composition

Independent module algebras compose with:

```text
AlgebraMix<A, B>
...
AlgebraMix<A, B, C, D, E, F, G>
```

`AlgebraMix` deliberately means only that independently owned executable algebras are available together to the parent Flow.

A child Flow adapts to the larger algebra by runtime projection:

```csharp
child.MapRuntime(
    (AlgebraMix<A, B> mix) => mix.A)
```

Requests adapt independently with `MapRequest`; `ContraMap` adapts runtime and request together.

### AlgebraIO retired

The previous `AlgebraIO<ALG>` evolved from a real interpreter concept in the Free model into only:

```csharp
interface AlgebraIO<ALG>
{
    ALG Algebra { get; }
}
```

That no longer represented IO, interpretation, executable capability, or semantic guarantee.

It has therefore been retired.

Grounding now simply implements the executable capability atoms and may expose the assembled algebra through an ordinary concrete property:

```csharp
public TodoAlgebra Algebra { get; }
```

Do **not** introduce a replacement generic algebra-provider trait during Tooling migration unless a real generic consumer creates that pressure.

### Grounding

Grounding owns realization of executable atoms.

Examples currently validated include:

- in-memory point groundings;
- EF Core point grounding;
- temporal atoms such as `ClockIO` and `DelayIO`.

These mechanisms do not imply Repository, Unit of Work, transaction, tracking, or persistence-boundary semantics.

### Views / TransformAdapter

UI materialization now uses:

```csharp
TransformAdapter<FROM, TO>
```

Two paths are supported:

```text
TO : Transformable<FROM, TO>
    -> delegate directly to TO.Transformation

otherwise:

FROM
    -> TO.Input
    -> TO.Transformation
    -> TO
```

The nested Input convention is analyzer-guarded.

The analyzer ships inside the `VSlices.Space` package and has been pressure-tested through:

- direct Roslyn use;
- `VSlices.Views.Abstract`;
- generated Razor code;
- packaged Views consumption;
- NativeAOT.

Tooling does not currently need to lower this pattern unless a real VSIR/UI case requests it.

---

## Semantics retired from the old Framework

The following old lowering vocabulary must **not** be restored merely to make existing tests pass:

```text
DomainType<T, Repr>
Identifier<T>          // old contract
Refined<T, Base>       // old contract
Transform<TO, FROM>
Entity<T, ID>
AggregateRoot<T, ID>
Repository
UnitOfWork
DatabaseIO
HasAlgebra
AlgebraEnv
Free<ALG, A> as Feature boundary
```

Some old concepts may have modern semantic counterparts under specific evidence. Those counterparts must be discovered case by case rather than treated as aliases.

---

## Modern mappings currently supported by evidence

### Transform

Old:

```csharp
Transform<TO, FROM>
```

Modern:

```csharp
Transformable<FROM, TO>
```

The generated property changes from the old `Invariants` surface to:

```csharp
static Req<FROM, TO>.Full Transformation => ...;
```

This mapping already has direct Framework evidence.

### DomainType

There is no modern marker equivalent.

Concrete `Repr`, `To()`, serialization helpers, or nested representation types may remain useful C# realization details, but Tooling must not emit a replacement marker merely to preserve the old inheritance list.

### Identifier

Do not map old `Identifier<T>` mechanically.

Current Framework identifiers such as `TodoId` are modeled as semantic spaces and may implement `DiscreteSpace<T>`, but Tooling must first verify that the VSIR classification being lowered actually expresses the equality/identity semantics owned by `DiscreteSpace`.

### Maintained

`MaintainedSpace<T>` is a plausible modern target when the VSIR case truly means a maintained finite semantic space and can satisfy its `All` contract.

Verify with a real maintained case before promoting this mapping.

### Refined

`DerivedSpace<T, BASE>` is a plausible modern target only when the semantic type is actually a subset/derivation of a base space and can provide `ToBase`.

Do not assume every old `Refined<T,BASE>` case is equivalent.

### Entity / AggregateRoot

No current Framework abstraction is an automatic replacement.

The old classifications may represent documentary semantics that should remain in VSIR without lowering to a Framework interface, or they may reveal a future abstraction. Let real cases decide.

---

## Tooling migration branch

Current branch:

```text
experiment/framework-modern-checkpoint
```

Current PR:

```text
#12 — Experiment modern Framework checkpoint
```

At the time of this handoff, the branch is intentionally partial.

### Changes already made

1. `src/framework` was moved from the very old pinned Framework to the executable-algebra checkpoint.
2. `VSlices.DocumentGeneration.csproj` no longer references removed `VSlices.Application`, `VSlices.Domain`, or `VSlices.Infrastructure`; it references only the Framework base project it actually consumes.
3. `tooling.slnx` no longer lists the removed Framework projects.
4. `GeneratedMaterializationCompilationTests` now compiles generated materialization against `VSlices.Space` and imports:
   - `VSlices.Arrows`;
   - `VSlices.Space`;
   - `VSlices.Space.Traits`.
5. Product/sum lowerers have a **partial** modernization:
   - `DomainType<...>` emission was removed in those paths;
   - `Transform<TO,FROM>` became `Transformable<FROM,TO>`;
   - generated `Invariants` became `Transformation`;
   - a sum root with no remaining contracts no longer emits an invalid dangling colon.

Do not assume these partial edits are the final lowering design.

### Current CI state

Tooling itself still builds successfully.

The latest fully observed partial-migration CI state was:

```text
VSlices.Tooling.Tests
    192 / 192 passing

VSlices.Vsir.CSharp.Tests
    106 / 114 passing
    8 failing
```

The 8 failures are expectations that still assert the old `DomainType`-based output:

1. `TicketIdLoweringTests.TicketId_classification_lowers_to_primitive_domain_type_and_identifier_contracts`
2. `SumDomainTypeLoweringTests.Name_sum_lowers_to_closed_root_and_transform_variants`
3. `AggregateRootSumLoweringTests.SrvIdentity_sum_lowers_shared_aggregate_contract_and_representation`
4. `VsirParserSemanticConservationTests.TicketId_semantics_are_preserved_and_lowered_through_identifier_structure_and_ruleset_equality`
5. `SrvIdentityIdLoweringTests.Refined_domain_type_lowers_to_independent_primitive_contracts`
6. `StreetNameLoweringTests.StreetName_lowering_preserves_the_current_semantic_contract`
7. `StreetExtensionIntrinsicRefineTests.StreetExtension_lowers_intrinsic_refinement_through_Ruleset_relations`
8. `TicketCodeLoweringExperimentTests.Existing_expression_renderer_is_sufficient_for_trim_and_normalized_value_flows_forward`

These failures are **not** evidence that `DomainType` should return.

They are the next migration pressure.

---

## Surfaces that should remain outside Tooling's stable lowering vocabulary for now

### Service / Product specializations

`ServiceFeature`, `ProductFeature`, `ServiceClaim`, and `ProductRole` compile but have little real pressure in the current checkpoint.

In particular, `ServiceClaim` still contains a global static registration/collision mechanism that does not fit cleanly with the newer ownership model.

Do not promote these types into Tooling vocabulary yet.

### Effectful semantic traits

`TransformableM` and `ValidatableM` currently have no meaningful consumers beyond their own definitions.

Do not lower to them until a real case requires effectful semantic transformation/admissibility.

---

## Migration authority

Remember:

```text
Tooling owns the rule language.
Rulesets own the rule vocabulary.
```

The Framework checkpoint is executable evidence about available target semantics. It does not mean every Framework type becomes a VSIR keyword or built-in lowering target.

Likewise:

```text
Built-ins bootstrap the language;
they do not define its semantic ceiling.
```

Use the current ruleset/lineage mechanisms rather than hard-coding every modern Framework name into Tooling when the vocabulary belongs in a ruleset.
