# Migration guide — VSlices Tooling to the modern Framework checkpoint

## Goal

Migrate Tooling from the retired Domain/Application/Infrastructure-era Framework contract to the current Space/Work/Grounding model **without compatibility shims and without semantic invention**.

This guide is intentionally procedural.

The migration should be driven by generated cases and compilation witnesses, not by a one-shot rename matrix.

---

## 0. Start from the migration branch

Use:

```text
experiment/framework-modern-checkpoint
```

Do not perform this migration directly on `main`.

Read first:

```text
docs/handoffs/framework-modern-checkpoint.md
```

That handoff records the Framework semantics, current Tooling partial state, and concepts that are intentionally not migration targets yet.

---

## 1. Pin the final green Framework checkpoint

The Framework branch under validation is:

```text
vslices/framework
experiment/transform-adapter-analyzer
```

Move:

```text
src/framework
```

to the exact SHA identified as the final green checkpoint in the handoff.

Do not replace this with Framework `master` merely because `master` is the default branch.

After moving the gitlink:

```bash
git submodule update --init --recursive
dotnet build tooling.slnx --configuration Release
dotnet test tooling.slnx --configuration Release --no-build
```

Record the first failing generated case before changing a lowerer.

---

## 2. Preserve the structural build cleanup already discovered

The migration branch already established that:

- `VSlices.DocumentGeneration` only needs the Framework base `VSlices` project;
- Tooling's solution should not list retired `VSlices.Domain`, `VSlices.Application`, or `VSlices.Infrastructure` projects;
- do not add all modern Framework projects to `tooling.slnx` merely for symmetry.

Generated compilation witnesses should reference the modern Framework project they actually need.

For semantic Space materialization, that is currently:

```text
src/framework/src/VSlices.Space/VSlices.Space.csproj
```

with imports:

```csharp
using VSlices.Arrows;
using VSlices.Space;
using VSlices.Space.Traits;
```

---

## 3. Do not create compatibility aliases

Do not add Framework or Tooling compatibility types such as:

```text
DomainType
Identifier
Refined
Transform
Entity
AggregateRoot
```

only to make old generated C# or tests compile.

A red test that expects one of these names is migration evidence.

The preferred response is:

```text
old expectation
    -> identify the semantic claim it was testing
    -> identify whether that claim still exists
    -> lower the modern claim if it exists
    -> otherwise remove the obsolete target expectation while preserving VSIR semantics
```

---

## 4. Migrate transformation first

This mapping is already supported by direct Framework evidence.

Old:

```csharp
Transform<TO, FROM>
```

Modern:

```csharp
Transformable<FROM, TO>
```

Old generated member:

```csharp
static Req<FROM, TO>.Full Invariants => ...
```

Modern:

```csharp
static Req<FROM, TO>.Full Transformation => ...
```

The migration branch already contains a partial implementation of this change in product and sum lowering.

Pressure it through existing cases before widening it.

Recommended first cases:

1. `StreetName`
2. simple sum variants in `Name`
3. `TicketCode`

Do not modify identifier/refinement/aggregate semantics in the same iteration unless the compiler forces the next boundary.

---

## 5. Retire DomainType without replacing it

`DomainType<T,Repr>` no longer has a Framework semantic counterpart.

Generated C# may continue to contain realization details such as:

```csharp
public readonly record struct Repr(...);
public Repr To() => ...;
```

when the ruleset or materialization contract still needs them.

But the type must not inherit a fictional replacement marker.

Update tests to assert the semantic/realization behavior that still matters, not the removed `DomainType` string.

---

## 6. Pressure identifier semantics separately

Do not mechanically change:

```text
Identifier<T>
    -> DiscreteSpace<T>
```

Instead use a real identifier case such as `TicketId`.

Questions to answer from the VSIR document and current Framework:

1. Does the case require semantic equality?
2. Does `DiscreteSpace<T>` express exactly that requirement?
3. Is identity a documentary classification only, or does generated C# require a Framework contract?
4. Are representation/equality rules owned by the ruleset rather than the Framework trait?

Only after these questions are answered should the identifier lowering be changed.

Use the compilation witness again immediately after the change.

---

## 7. Pressure maintained spaces

For an existing maintained case, verify whether the semantics truly imply:

```csharp
MaintainedSpace<T>
{
    static abstract Seq<T> All { get; }
}
```

If yes, lower that mechanism and update only the affected expectations.

If the VSIR classification means something broader than a finite maintained semantic set, do not force it into `MaintainedSpace<T>`.

---

## 8. Pressure refinement against DerivedSpace

Old:

```text
Refined<T, BASE>
```

Possible modern mechanism:

```text
DerivedSpace<T, BASE>
```

This is not an alias.

Before using `DerivedSpace`, verify that:

- `T` really is semantically a space derived from/subset of `BASE`;
- a faithful `ToBase()` exists;
- admissibility/transformation rules remain independently represented.

Use `SrvIdentityId` and `StreetExtension` as real pressure cases.

If the old `refined` classification carried different semantics, leave the mismatch explicit instead of forcing `DerivedSpace`.

---

## 9. Leave Entity and AggregateRoot late

There is no current automatic Framework contract for:

```text
Entity<T,ID>
AggregateRoot<T,ID>
```

Do not design replacements while simpler migration cases remain unresolved.

When the migration reaches `SrvIdentity` or another aggregate case:

1. preserve the VSIR/documentary classification;
2. identify what generated behavior actually depended on the old contract;
3. ask whether modern Space/Work semantics already express that behavior;
4. only introduce a new Framework/lowering mechanism if a real behavior remains unexplained.

A classification may legitimately survive in VSIR while lowering to no Framework interface.

---

## 10. Keep Work lowering separate from Space migration

The current Framework Work model is:

```text
Feature<F, ALG, REQ, RES>
    -> Flow<ALG, REQ, RES>
```

with executable module algebra records and capability atoms.

Current point atoms:

```text
PointReader<POINT, ID>
PointWriter<POINT>
PointRemover<POINT, ID>
```

Important PointReader shape:

```csharp
OptionT<IO, POINT> ReadOrDefault(ID id);
IO<POINT> Read(ID id); // derived default, required-value semantics
```

Independent algebras compose through `AlgebraMix`.

Do not add Feature/algebra lowering merely as part of updating old domain-type lowerers. Wait for a real VSIR Feature/Work case.

---

## 11. Do not target retired AlgebraIO

`AlgebraIO<ALG>` has been removed from the current Framework.

Grounding may expose a concrete assembled algebra property, but there is no generic provider trait to lower or generate.

Do not recreate `AlgebraIO`, `HasAlgebra`, or `AlgebraEnv` as migration shims.

---

## 12. UI/TransformAdapter is available but not automatically a Tooling target

Current Framework UI support includes:

```text
TransformAdapter<FROM, TO>
```

and analyzer enforcement for the canonical nested `Input` convention.

This is useful target capability, especially for UI projects, but should enter VSIR/Tooling only when an actual UI authoring/lowering case needs it.

Do not make `TransformAdapter` a generic built-in rule merely because it exists.

---

## 13. Keep provisional Framework surfaces out of lowering

Do not currently make these stable Tooling targets:

```text
ServiceFeature
ProductFeature
ServiceClaim
ProductRole
TransformableM
ValidatableM
```

They either lack sufficient real pressure or still contain design residue that should not become ruleset vocabulary yet.

---

## 14. Test migration discipline

For each case:

```text
1. run existing test
2. inspect generated C#
3. inspect the VSIR source for that case
4. identify which old assertion is semantic vs historical API shape
5. change the smallest lowerer/rule
6. update only expectations invalidated by the semantic decision
7. compile generated C# against the modern Framework
8. rerun the focused test
9. rerun VSlices.Vsir.CSharp.Tests
10. rerun tooling.slnx tests
```

Do not batch-update snapshots/Contains assertions before the generated witness compiles.

---

## 15. Current red-test queue

At handoff, the partial migration has 8 known VSIR C# test failures, all still asserting `DomainType`-era output:

1. TicketId
2. Name sum
3. SrvIdentity aggregate sum
4. TicketId semantic-conservation case
5. SrvIdentityId refined case
6. StreetName
7. StreetExtension intrinsic refine
8. TicketCode

Recommended order:

```text
StreetName
    -> validate plain Transformable lowering

Name sum / TicketCode
    -> validate sum/product realization after DomainType removal

TicketId
    -> discover identifier -> modern Space semantics

SrvIdentityId / StreetExtension
    -> discover refined -> DerivedSpace or other mechanism

SrvIdentity aggregate
    -> last; no automatic AggregateRoot replacement
```

---

## 16. Definition of done for this migration stage

The Space-lowering migration stage is complete when:

- Tooling pins the final green Framework checkpoint;
- no generated C# references retired Framework namespaces/projects;
- no generated C# references `DomainType` or old `Transform`;
- every remaining old classification has either:
  - a demonstrated modern lowering mechanism, or
  - an explicit decision to remain documentary/non-lowered;
- generated compilation witnesses build against modern Framework projects;
- `VSlices.Vsir.CSharp.Tests` are green;
- `VSlices.Tooling.Tests` remain green;
- no compatibility shims were added solely to preserve old output.

Only then should the migration thread decide whether to expand into Work/Feature lowering.
