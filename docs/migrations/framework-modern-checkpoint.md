# Migration guide — VSlices Tooling to the modern Framework checkpoint

> Companion handoff: `docs/handoffs/framework-modern-checkpoint-2026-09-27.md`
>
> Target Framework candidate checkpoint:
> `cabbde2305cc5a0111f34e88662878db74cf10cd`
>
> Do not migrate against Framework `master` in this iteration.
>
> Do not use compatibility shims in Framework to make old Tooling output compile.

## Goal

Move VSlices Tooling from the historical Framework target vocabulary to the current Framework checkpoint while preserving VSIR semantic authority.

The migration is intentionally pressure-driven:

```text
pin checkpoint
    -> compile
    -> first concrete incompatibility
    -> identify semantic owner
    -> make minimum lowering/test migration
    -> rerun
    -> observe next incompatibility
```

Do not bulk rename all historical contracts at once.

## 0. Preserve the baseline

Before changing lowering:

1. record current Tooling `main` SHA;
2. record the Framework submodule SHA currently pinned by Tooling;
3. keep existing VSIR corpus and lowering fixtures unchanged unless a fixture is proven to encode retired target realization rather than VSIR semantics;
4. preserve the existing Ruleset snapshot used by the tests.

The migration should make target realization change around the same semantic evidence.

## 1. Pin the Framework checkpoint

Update:

```text
src/framework
```

to:

```text
cabbde2305cc5a0111f34e88662878db74cf10cd
```

Do not point at Framework `master`.

The checkpoint includes:

- current Space contracts;
- `Feature -> Flow<ALG,REQ,RES>`;
- executable point atoms;
- `AlgebraMix`;
- Grounding examples;
- TransformAdapter/analyzer/UI evidence;
- `PointReader.ReadOrDefault` + default required `Read`;
- removal of `AlgebraIO`.

## 2. Repair project geometry before semantic lowering

Historical Tooling references include removed projects:

```text
src/framework/src/VSlices.Application/VSlices.Application.csproj
src/framework/src/VSlices.Domain/VSlices.Domain.csproj
src/framework/src/VSlices.Infrastructure/VSlices.Infrastructure.csproj
```

Do not replace them three-for-three with Space/Work/Grounding.

Inspect each consumer.

Observed evidence:

`VSlices.DocumentGeneration` uses `VSlices.Monads` from the base project and did not require the three historical layers.

The minimum project reference discovered experimentally was:

```xml
<ProjectReference Include="..\framework\src\VSlices\VSlices.csproj" />
```

For `tooling.slnx`, include Framework projects only when Tooling actually builds them. Avoid pulling all Framework projects into Tooling's solution merely to make the tree look symmetrical.

Acceptance condition:

```text
dotnet build tooling.slnx
```

must reach the Tooling/test compilation stage before changing semantic lowerers.

## 3. Move the generated compilation witness to Space

The generated-materialization witness currently locates:

```text
VSlices.Domain.csproj
```

and imports historical namespaces.

Change the witness so target-generated semantic values compile against:

```text
src/framework/src/VSlices.Space/VSlices.Space.csproj
```

with current imports as actually required, for example:

```text
VSlices.Arrows
VSlices.Space
VSlices.Space.Traits
```

Do not add namespaces preemptively.

Expected next failure after this step:

```text
DomainType<...> not found
Transform<...> not found
```

That failure is useful evidence and should not be hidden by aliases.

## 4. Migrate transformation realization first

Historical output:

```csharp
Transform<Target, Input>

static Req<Input, Target>.Full Invariants => ...
```

Current Framework:

```csharp
Transformable<Input, Target>

static Req<Input, Target>.Full Transformation => ...
```

The generic order changes because the current contract reads naturally as:

```text
Transformable<FROM, TO>
```

Recommended first migration:

```text
Transform<Target, Input>
    -> Transformable<Input, Target>

Invariants
    -> Transformation
```

Apply this only where VSIR actually states transformation semantics.

Run the smallest product/sum lowering witnesses immediately after this change.

## 5. Do not replace DomainType mechanically

Historical generated contract:

```csharp
DomainType<T, T.Repr>
```

Current Framework has no equivalent universal marker.

Do **not** invent:

```text
Space<T>
DomainType compatibility alias
SemanticType<T>
```

merely to preserve generated shape.

Separate:

```text
semantic facts expressed by VSIR
from
representation conveniences emitted historically
```

If `Repr`, `To()`, state, and representation mappings are still justified, they may remain concrete generated realization without a marker interface.

Tests that assert `DomainType<T,T.Repr>` should be revised only after confirming they assert historical target shape rather than a semantic requirement.

## 6. Pressure identifier semantics independently

Historical Tooling can establish identifier semantics through:

- identifier classification;
- identifier trait/capability;
- equality requirements.

Historical target contract:

```csharp
Identifier<T>
```

Modern Framework evidence such as `TodoId` uses:

```csharp
DiscreteSpace<T>
```

However:

```text
Identifier<T>
    -> DiscreteSpace<T>
```

must **not** be applied as a blind rename.

Pressure a real identifier fixture such as TicketId/SrvIdentityId and answer:

1. what semantic facts does VSIR establish?
2. is equality the only Framework-side consequence?
3. does `DiscreteSpace<T>` express exactly that fact or a broader one?
4. which representation/equality code is Ruleset realization rather than Framework semantics?

Only then update the identifier lowerer/tests.

## 7. Pressure maintained semantics

Modern Framework has:

```csharp
MaintainedSpace<T>
```

Use the existing maintained fixture as pressure.

Do not infer the mapping solely from the shared word "maintained". Confirm:

- the admitted VSIR semantics;
- required equality/identity behavior;
- generated maintained values;
- whether all current obligations fit `MaintainedSpace<T>`.

## 8. Pressure refined/derived semantics

Historical target contract:

```csharp
Refined<T, BASE>
```

Modern Framework exposes:

```csharp
DerivedSpace<T, BASE>
```

Treat this as a **candidate semantic relation**, not a rename.

Validate against an admitted refined fixture:

- source/base ownership;
- conversion back to base;
- transformation/refinement evidence;
- representation behavior.

If the semantics do not match exactly, keep the VSIR fact and discover a different target realization rather than forcing `DerivedSpace`.

## 9. Do not automatically migrate Entity/AggregateRoot

Historical lowering includes:

```text
Entity<T, ID>
AggregateRoot<T, ID>
```

The current Framework checkpoint does not establish direct replacements.

Do not map these to `Evolvable`, `DiscreteSpace`, or another modern trait merely because some examples overlap.

For each canonical aggregate/entity fixture:

```text
VSIR fact
    -> currently admitted?
    -> still target-relevant?
    -> modern Framework equivalent exists?
```

If no equivalent exists, keep the Tooling semantic question open or stop lowering that target contract explicitly.

Do not reintroduce retired DDD markers into Framework.

## 10. Keep Work lowering separate from Space migration

Do not make every domain-type migration emit Work concepts.

Current Work target surface includes:

```text
Feature<F, ALG, REQ, RES>
Flow<ALG, REQ, RES>
PointReader<POINT, ID>
PointWriter<POINT>
PointRemover<POINT, ID>
AlgebraMix<...>
```

These should enter VSIR lowering only when VSIR has corresponding Work semantics.

A semantic Space type does not imply a Feature, algebra, or Grounding.

## 11. PointReader target semantics

If/when Tooling emits Work point capabilities, the current reader contract is:

```csharp
public interface PointReader<POINT, ID>
{
    OptionT<IO, POINT> ReadOrDefault(ID id);

    IO<POINT> Read(ID id) => ...;
}
```

Only `ReadOrDefault` is a Grounding implementation obligation.

Use:

```text
ReadOrDefault
    when absence is expected semantic control flow

Read
    when presence has already been established / is required
```

Do not generate a mandatory concrete `Read` implementation unless the Grounding requires semantics different from the default.

## 12. Do not target AlgebraIO

`AlgebraIO<ALG>` has been removed.

Grounding may expose:

```csharp
public SomeAlgebra Algebra { get; }
```

as an ordinary concrete property.

Do not emit or depend on a generic algebra-provider contract until a real generic operation requires one.

## 13. AlgebraMix replaces the historical AlgebraSum realization

Current composition mechanism:

```text
AlgebraMix<A,B,...>
```

Historical Free-era `AlgebraSum` represented a different mechanism and should not be kept as an alias.

If VSIR eventually expresses Work-algebra composition, lower to `AlgebraMix` only after confirming that VSIR states structural availability of the child algebras rather than Free-instruction coproduct semantics.

## 14. Do not target provisional Framework surfaces

Keep these outside the migration target unless a real Tooling case pressures them:

```text
ServiceFeature
ProductFeature
TransformableM
ValidatableM
```

Tooling should not enlarge its target vocabulary simply because a Framework type exists.

## 15. TransformAdapter belongs to presentation realization

`TransformAdapter<FROM, TO>` solves presentation adaptation.

It should not silently replace ordinary semantic transformation lowering.

The adapter is relevant when target presentation code needs:

```text
FROM -> TO.Input -> TO
```

under the validated single-input convention.

The Space analyzer is packaged with `VSlices.Space`; Views uses it through `vTextInput<T>`.

Do not make core VSIR domain-type lowering depend on TransformAdapter unless VSIR/presentation semantics explicitly require that adaptation.

## 16. Update compilation expectations in layers

Recommended order:

```text
A. project references
B. compilation witness project/using namespaces
C. transform contract/property
D. remove DomainType marker expectation
E. identifier case
F. maintained case
G. refined case
H. sum cases
I. aggregate/entity cases
J. only then broader corpus review
```

After each letter:

1. run the smallest focused test;
2. inspect generated C#;
3. run generated-materialization compilation;
4. run all `VSlices.Vsir.CSharp.Tests`;
5. only then proceed.

## 17. Keep Ruleset authority separate

The migration must preserve:

```text
Tooling owns the rule language.
Rulesets own the rule vocabulary.
```

Do not move target-native details into Framework just because a Framework contract changed.

Framework traits do not authorize Tooling to add VSIR semantic forms that VSIR does not already admit.

## 18. Review the capability matrix

`docs/vsir-capability-matrix.md` currently contains historical target-contract statements such as:

```text
DomainType<T,T.Repr>
Identifier<T>
Refined<T,BASE>
```

Update this matrix **after executable migration evidence exists**.

The matrix should distinguish:

```text
VSIR semantic support
from
current C# target realization
```

Do not rewrite it first and then force code to match the new prose.

## 19. Treat exploratory PR #12 as evidence only

Existing exploration:

```text
vslices/tooling PR #12
experiment/framework-modern-checkpoint
```

contains partial changes made while discovering the migration boundary.

Do not merge it wholesale.

Useful observations from it:

- pinning the modern Framework itself is viable;
- old solution/project references fail before semantics;
- DocumentGeneration only needs the base Framework project;
- generated compilation should move from Domain to Space;
- `DomainType` and `Transform` are the first retired target contracts exposed by compilation;
- experimental product/sum lowering edits show one possible first step but are not an approved complete migration.

Prefer reconstructing the migration intentionally on the continuation's own branch.

## Acceptance criteria

The migration is not complete because generated text looks plausible.

Minimum acceptance:

```text
dotnet build tooling.slnx
dotnet test tests/VSlices.Vsir.CSharp.Tests
dotnet test tests/VSlices.Tooling.Tests
```

plus existing CI smoke / publish checks.

For each migrated semantic case:

- same canonical VSIR enters the pipeline;
- semantic validation still passes/fails for the same reasons;
- lowering does not invent missing semantics;
- generated C# compiles against the pinned Framework checkpoint;
- removed Framework vocabulary is not recreated through compatibility aliases;
- Ruleset-owned target knowledge remains Ruleset-owned.

## Stop conditions

Stop rather than guess when:

- an old Framework contract has no demonstrated modern equivalent;
- a test expectation mixes semantic intent with historical source shape;
- mapping would make a broader semantic claim than VSIR established;
- current Framework lacks the target concept;
- a Ruleset rule would need to invent a new VSIR semantic fact.

Record the first unsupported boundary and use it as the next design pressure.

## Suggested first continuation slice

Start with one simple product fixture that already has transform semantics.

Suggested path:

```text
StreetName / equivalent simple product
    -> remove DomainType marker
    -> Transformable<Input,Target>
    -> Transformation property
    -> compile witness against Space
    -> green focused tests
```

Then proceed to one identifier fixture.

Do not start with aggregate-root or the full corpus.
