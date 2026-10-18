# Handoff — Framework modern executable-algebra checkpoint

> Date: 2026-09-27
>
> Audience: the continuation responsible for migrating VSlices Tooling.
>
> Framework repository: `vslices/framework`
>
> Framework branch: `experiment/transform-adapter-analyzer`
>
> Candidate checkpoint: `cabbde2305cc5a0111f34e88662878db74cf10cd`
>
> Framework master is intentionally **not** the migration target for this handoff.

## Purpose

This handoff records the Framework model reached after recovering `Flow`, removing the Free-monad Work experiment from the active execution model, validating executable Work-module algebras, adding UI transform adaptation, and refining the point capability vocabulary.

Tooling should consume the pinned checkpoint above rather than waiting for Framework `master` to be reconciled.

Do not interpret this document as authority to preserve the old Framework API through compatibility aliases. The migration should preserve VSIR semantics and adapt target realization to the current Framework only where the current Framework actually expresses the same meaning.

## Current architecture

The currently validated coarse separation is:

```text
Space
    semantic values
    admissibility
    transformation
    evolution

Work
    Feature
    Flow
    executable algebraic capability atoms
    algebra composition

Grounding
    concrete realization of executable atoms

Views
    presentation adaptation over semantic transformations
```

The old project taxonomy is not the current target:

```text
VSlices.Domain
VSlices.Application
VSlices.Infrastructure
```

Do not recreate those projects or namespaces merely to keep existing Tooling output compiling.

## Space

The stable/presently evidenced Space vocabulary includes:

```text
DiscreteSpace<T>
DerivedSpace<T, BASE>
MaintainedSpace<T>
Transformable<CTX, FROM, TO>
Transformable<FROM, TO>
Validatable<...>
Evolvable<T, STATE>
```

Not every older VSIR capability has a proven one-to-one mapping to these contracts. In particular, do not mechanically rename old `DomainType`, `Identifier`, `Refined`, `Entity`, or `AggregateRoot` contracts.

## Transformable

Target-owned transformation now has the shape:

```csharp
public interface Transformable<FROM, TO>
    : Transformable<TO, FROM, TO>
    where TO : Transformable<FROM, TO>
{
}
```

The executable rule is exposed as:

```csharp
static abstract Req<FROM, TO>.Full Transformation { get; }
```

The retired lowering shape:

```text
Transform<TO, FROM>
static ... Invariants
```

must not be retained simply for source compatibility.

The current candidate realization is:

```text
Transformable<FROM, TO>
static ... Transformation
```

but every VSIR semantic form must still be checked against its actual meaning before applying this mapping.

## Feature / Flow

The current executable Feature contract is:

```csharp
public interface Feature<F, ALG, REQ, RES>
    where F : Feature<F, ALG, REQ, RES>
{
    static abstract Flow<ALG, REQ, RES> Get();
}
```

Important distinctions:

```text
ALG
    executable algebra owned by the Work module

REQ
    input to one execution

RES
    result of one execution
```

`Feature` does not expose `EnvIO` execution helpers. It owns the Flow contract, not execution of the final IO.

## Executable Work algebras

A `[concept].Work` module defines a simple algebra record from executable capability atoms.

Example:

```csharp
public sealed record TodoAlgebra(
    PointReader<Todo, TodoId> Reader,
    PointWriter<Todo> Writer,
    PointRemover<Todo, TodoId> Remover,
    TodoIdSource Ids);
```

The algebra is the runtime of the module's Features:

```text
Flow<TodoAlgebra, Request, Response>
```

There is no current Free-program/interpreter layer.

Retired active mechanisms include:

```text
Free<ALG, A> as the Feature WorkFlow
FreeAlgebra
HasAlgebra
AlgebraEnv
operation-functor PointReader/Writer/Remover
```

Historical documents may still describe those experiments. They are evidence of trajectory, not the current target API.

## Point capability atoms

### PointReader

The current primitive is:

```csharp
public interface PointReader<POINT, ID>
{
    OptionT<IO, POINT> ReadOrDefault(ID id);

    IO<POINT> Read(ID id) => ...;
}
```

Only `ReadOrDefault` must be realized by Grounding.

Semantics:

```text
ReadOrDefault(id)
    absence is expected
    -> OptionT<IO, POINT>

Read(id)
    presence is expected
    -> IO<POINT>
    None becomes InvalidOperationException
```

The default `Read` is exposed to concrete implementations through an extension method. A concrete Grounding may declare its own `Read`; normal C# member resolution gives that implementation precedence.

This is intentionally analogous to the semantic distinction between `SingleOrDefault` and `Single`.

### PointWriter

```csharp
public interface PointWriter<POINT>
{
    IO<Unit> Write(POINT point);
}
```

### PointRemover

```csharp
public interface PointRemover<POINT, ID>
{
    IO<Unit> Remove(ID id);
}
```

These atoms do not imply Repository, Unit of Work, transaction, tracking, durability, or persistence ownership.

## Grounding and removal of AlgebraIO

The former contract:

```csharp
AlgebraIO<ALG>
{
    ALG Algebra { get; }
}
```

has been removed from the active model.

Reason:

```text
Grounding exposes ALG
    !=
a generic provider trait is semantically required
```

No generic code consumed `AlgebraIO<ALG>`; it only restated that several concrete Groundings had an `Algebra` property. The `IO` suffix also carried historical interpreter semantics that no longer matched the executable-algebra model.

A concrete Grounding may simply expose:

```csharp
public TodoAlgebra Algebra { get; }
```

If future generic provisioning/composition requires a trait, rediscover the smallest truthful abstraction from that real pressure rather than reviving `AlgebraIO` by default.

## AlgebraMix

Independent module algebras compose structurally through:

```text
AlgebraMix<A, B>
...
AlgebraMix<A, B, C, D, E, F, G>
```

A parent Flow projects the child runtime it needs:

```csharp
child.MapRuntime(
    (AlgebraMix<FileAlgebra, TodoAlgebra> mix) => mix.A)
```

Request adaptation is separate:

```csharp
child.MapRequest(parent => childRequest)
```

`ContraMap` adapts runtime and request together.

The name `AlgebraMix` intentionally avoids asserting categorical product/coproduct semantics.

The older `AlgebraSum` name belonged to a different Free-instruction experiment and is not the current target.

## TransformAdapter and UI

Presentation can use:

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

For the nested-Input path:

1. `TO` declares nested non-generic `Input`;
2. `Input` has exactly one non-empty public constructor;
3. that constructor has exactly one parameter of type `FROM`;
4. `TO : Transformable<TO.Input, TO>`.

`VSlices.Space` embeds the analyzer that validates this convention.

`vTextInput<T>` uses `TransformAdapter<string, T>` rather than forcing every semantic target to own `Transformable<string, T>`.

The adapter has executable evidence through ordinary UI tests, Razor-generated C# diagnostics, package-consumer diagnostics, and a NativeAOT probe.

## Surfaces not yet stable migration targets

Do **not** make Tooling emit these merely because Framework currently contains them:

```text
ServiceFeature / ProductFeature
TransformableM / ValidatableM
```

They do not yet have enough real consumer pressure to make them mandatory target vocabulary.

`ServiceClaim` also retains behavior that deserves separate review before it becomes lowering authority.

## Historical contracts that require migration rather than aliases

Tooling currently contains lowering/tests around historical concepts such as:

```text
DomainType<T, T.Repr>
Identifier<T>
Refined<T, BASE>
Transform<TO, FROM>
AggregateRoot<T, ID>
Entity<T, ID>
VSlices.Domain
VSlices.Application
VSlices.Infrastructure
```

Do not solve the migration by recreating compatibility interfaces inside Framework.

For each semantic fact, ask:

```text
what did VSIR actually state?
    -> what current Framework concept, if any, expresses that fact?
    -> if none exists, is the VSIR semantic still valid independently?
    -> does lowering need a new realization rule, or should the target contract disappear?
```

## Evidence already discovered in Tooling exploration

An exploratory Tooling branch/PR exists:

```text
branch: experiment/framework-modern-checkpoint
PR: #12
```

It is **not** the final migration and should not be merged as-is.

It already exposed useful boundaries:

1. `tooling.slnx` and `VSlices.DocumentGeneration.csproj` referenced removed Framework projects.
2. `VSlices.DocumentGeneration` only actually required the base `VSlices` project.
3. generated-materialization compilation tests still compiled against `VSlices.Domain`.
4. once the witness was pointed at `VSlices.Space`, generated output failed on retired `DomainType` and `Transform` contracts.
5. an exploratory change started replacing `Transform<TO, FROM>` with `Transformable<FROM, TO>` and `Invariants` with `Transformation`.

Treat those changes as evidence/probes, not as approved final lowering design.

## Authority rule for the migration

Preserve:

```text
VSIR
    owns admitted semantics

Tooling
    owns lowering mechanism and fail-closed coordination

Ruleset
    owns target-native realization vocabulary

Framework
    is one target-side semantic/runtime surface
```

A Framework API change does not automatically authorize Tooling to reinterpret VSIR.

The migration must update realization while preserving the distinction between semantic authority and target mechanism.
