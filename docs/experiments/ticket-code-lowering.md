# TicketCode lowering experiment

This branch exercises successive VSlices boundaries against the real `TicketCode` specimen from Ticket Support.

The experiment follows one rule:

> change only the first layer that can no longer justify the result, then rerun the real consumer before generalizing further.

## Observation chain

```text
TicketCode.vsir
  -> VSIR100
     normalize not representable

  -> represent normalize explicitly
  -> CSL030
     normalize represented, but C# lowerer has no execution mechanism

  -> ordered normalization dataflow in Tooling
  -> CSL031
     no target-specific intrinsic.trim realization

  -> vslices/ruleset#2 supplies intrinsic.trim
  -> real consumer lower succeeds
     deterministic witness computed
     existing human materialization preserved
     lineage established

  -> deterministic witness exposes target-context namespace gap
     Tickets.Domain vs physical Aggregates/Tickets

  -> evaluated RootNamespace + full relative path
     => Tickets.Domain.Aggregates.Tickets

  -> existing lineage invokes three-way rebase
  -> original REB002 is too opaque

  -> distinguish:
     REB002 = location cannot be established uniquely
     REB004 = location known, human and deterministic branches changed it differently

  -> explicit --resolve deterministic
  -> textual rebase succeeds

  -> target compilation fails
     human static import still points to old TicketCode namespace

  -> finding
     textual preservation != target-semantic validity

  -> managed Roslyn namespace-move experiment
     exact semantic references
     staged conservative rewrites
     compile validation
     blast radius
     explicit human authority
     transactional sources + lineage

  -> real consumer packaging failure
     BuildHost-netcore missing

  -> Native AOT + managed companion runtime-closure fix

  -> real consumer workspace diagnostics
     .NET 10 package-pruning advisory surfaced as MSBuildWorkspace failure

  -> narrow advisory classification
     other workspace failures remain fail-closed

  -> real consumer analysis cost ~58s

  -> separate authorization to start expensive analysis
     from authorization to mutate human-maintained code

  -> namespace policy experiment
     project-specific path patterns allow physical organization
     to differ from namespace organization

  -> review hardening
     semantic authority, companion completeness,
     fail-closed compilation, semantic artifact identity,
     documentation and responsibility extraction
```

## 1. Normalization ownership

The observed VSIR contains:

```yaml
- normalize:
    target: input.Value
    intrinsic: trim
```

The final ownership split is:

```text
VSlices.Vsir
  -> recognizes `trim` as a valid normalization semantic
  -> rejects unknown normalize intrinsic values

C# lowering mechanism
  -> processes construction steps in order
  -> carries a reference-to-expression environment

Ruleset
  -> materializes intrinsic.trim for C#
```

The current Ruleset primitive is sufficient:

```yaml
- node: intrinsic.trim
  mode: deterministic
  renderer: expression
  template: "{value}.Trim()"
```

For TicketCode, the deterministic witness is semantically equivalent to:

```text
ensure non-empty(input.Value.Trim())
state.Value <- input.Value.Trim()
ordinal equality over state.Value
```

The review pass found one semantic-authority leak: an arbitrary intrinsic string could previously reach Ruleset lookup. That is now closed by VSIR validation; only the demonstrated `trim` intrinsic is recognized in this baseline.

Current normalization lowering may repeat a rendered expression in validation and construction. `trim` is pure, so the demonstrated case is valid. Generalization beyond pure transforms requires either referential transparency as an explicit invariant or a single-evaluation intermediate.

## 2. Target-context namespace derivation

The first namespace experiment defines the unfiltered default as:

```text
nearest unique .csproj
  -> evaluate RootNamespace through dotnet msbuild
  -> project-relative VSIR directory
  -> append directory segments
```

For TicketCode:

```text
project:
  services/ticket-service/Tickets.Domain/Tickets.Domain.csproj

RootNamespace:
  Tickets.Domain

VSIR directory:
  Aggregates/Tickets

unfiltered result:
  Tickets.Domain.Aggregates.Tickets
```

MSBuild evaluation is intentional; Tooling does not parse `<RootNamespace>` heuristically or assume the project filename is authoritative.

An explicit `--namespace` remains authoritative and bypasses derived namespace policy.

## 3. Namespace path policy

The later consumer experiment established that physical folder organization and namespace organization are not always isomorphic.

Project policy now supports:

```yaml
targets:
  csharp:
    namespace:
      ignore-folders:
        - "Aggregates/*"
        - "Aggregates/**/Entities"
```

Semantics:

```text
*   = exactly one directory segment
?   = one character inside one segment
**  = zero or more complete directory segments
```

The full pattern establishes context, but only the terminal matched folder is removed.

Examples:

```text
Aggregates/*
  -> ignores direct children such as Tickets or Orders
  -> preserves Aggregates

Aggregates/Tickets/Entities
  -> ignores Entities only in that exact context

Aggregates/**/Entities
  -> ignores a terminal Entities folder at any depth below Aggregates

Aggregates/**/*
  -> ignores every descendant folder below Aggregates
  -> preserves Aggregates itself
```

The current Ticket Support policy is deliberately narrower:

```yaml
ignore-folders:
  - "Aggregates/*"
  - "Aggregates/**/Entities"
```

That policy allows `TicketCode.vsir` under `Aggregates/Tickets` to derive `Tickets.Domain.Aggregates`, which matches the existing human materialization and avoids manufacturing a namespace move solely from physical organization.

The pattern language is owned by `NamespacePathPolicy` inside the .NET target assembly; project/MSBuild discovery remains in `DotNetTargetContextResolver`.

## 4. Conservative three-way materialization rebase

The original real namespace change produced:

```text
previous deterministic:
  namespace Tickets.Domain;

human:
  namespace Tickets.Domain.Aggregates;

next deterministic:
  namespace Tickets.Domain.Aggregates.Tickets;
```

Both human and deterministic branches changed the same insertion point.

The rebaser therefore distinguishes:

```text
REB002
  deterministic change location cannot be established uniquely

REB004
  location is known, but human and deterministic branches changed it differently
```

`--resolve deterministic` authorizes only that known textual conflict region. It does not replace the entire human materialization and does not authorize arbitrary semantic edits in other files.

This remains a three-way **materialization** merge driven by deterministic witnesses; it is not an AST-semantic merge.

## 5. Real post-rebase semantic failure

The first consumer execution of:

```text
vslices lower TicketCode --resolve deterministic
```

produced consumer commit:

```text
f057f23fc630fceb80036534251f6b863fa506e4
```

The namespace declaration moved correctly, but a human static import outside the textual conflict region still referenced:

```csharp
Tickets.Domain.Aggregates.TicketCode
```

while the type had moved to:

```text
Tickets.Domain.Aggregates.Tickets.TicketCode
```

Classification:

```text
deterministic projection          ✓
target namespace derivation       ✓
three-way textual rebase          ✓
explicit textual resolution       ✓
target-semantic consequences      ✗
```

The narrow finding is:

> preserving unrelated human text does not prove that the target materialization remains semantically valid.

## 6. Roslyn semantic namespace refactoring

The textual `CSharpRebaser` stays generic. The known .NET namespace-move consequence is handled by a separate managed companion using Roslyn + `MSBuildWorkspace`.

The helper now has explicit responsibilities:

```text
Program.cs
  -> thin process host

RefactorArguments
  -> CLI protocol parsing

NamespaceMovePlanner
  -> solution/symbol/reference planning

CompilationValidator
  -> fail-closed baseline/proposal compilation checks

RefactorManifest
  -> staged-plan protocol
```

The semantic artifact identity passed to Roslyn comes from parsed VSIR `name:` carried by `TranspilationResult`, not from the `.vsir` filename.

The helper resolves the original `INamedTypeSymbol`, uses `SymbolFinder.FindReferencesAsync`, prepares conservative rewrites, validates compilation, stages changed files, and returns a manifest. It never commits consumer files itself.

If Roslyn finds a reference that cannot be conservatively rewritten, the operation fails explicitly, e.g. `DOTNET031`.

## 7. Two authority boundaries

A real consumer run showed Roslyn workspace analysis taking about 58 seconds. That produced a distinct authority concern before mutation is even considered.

The workflow now asks twice:

```text
known namespace move
  -> Analyze semantic blast radius with Roslyn? [y/N]

if approved and plan is valid
  -> show References: N / Files: M
  -> Apply semantic refactoring? [y/N]
```

Only explicit `y` / `yes` approves. Blank, EOF and unrecognized answers reject.

These permissions are distinct:

```text
1. authorize potentially expensive target-semantic analysis
2. authorize mutation of human-maintained source files
```

`--resolve deterministic` grants neither implicitly.

## 8. Transactional safety

The target-semantic workflow is delegated from `LoweringCoordinator` to `SemanticRefactoringCoordinator`.

```text
textual candidate
  -> detect namespace move
  -> analysis authorization
  -> Roslyn plan
  -> baseline compilation validation
  -> proposed compilation validation
  -> staged source files
  -> blast radius
  -> mutation authorization
  -> SHA-256 precondition re-check
  -> transactionally commit human sources + lineage
```

Failure to obtain a Roslyn `Compilation` is itself a validation failure. “Could not verify” is never treated as “verified”.

Existing compiler errors, invalid proposed compilation, source changes after planning, rejection, or transaction failure stop without intentionally advancing lineage independently.

`TransactionalFileWriter` remains unabstracted beyond its current concrete consumer because no second real use case justifies a general transaction framework.

## 9. Native AOT boundary and companion health

Roslyn/MSBuildWorkspace is intentionally not embedded in the Native AOT CLI.

Distribution:

```text
vslices / vslices.exe
  Native AOT coordinator

refactor/
  VSlices.Targets.DotNet.Refactor.dll
  BuildHost-netcore/
  BuildHost-net472/
  Roslyn/MSBuild dependencies
```

The real consumer exposed a package that contained the root helper DLL but lacked `BuildHost-netcore`.

A complete companion is now defined once as:

```text
helper DLL
+ BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll
```

That definition is shared by startup health, same-build self-update repair, downloaded-archive validation, staging validation and the Windows installer.

A root helper DLL without its build host is therefore incomplete and `vslices update --self` must repair it rather than report “up to date”.

## 10. Workspace diagnostics

The real consumer also exposed .NET 10 package-pruning advisories that `MSBuildWorkspace` can surface as `WorkspaceDiagnosticKind.Failure` even though normal `dotnet` treats them as warnings.

The implementation does not ignore workspace failures generally. Only the known pruning advisory shape is non-fatal; unrelated project/SDK/import/workspace failures remain fail-closed through `DOTNET022`.

## `lower` vs direct `rebase`

This distinction is deliberate:

```text
rebase
  = textual primitive

lower
  = project workflow
    + lineage policy
    + known target-semantic closure when required
```

The direct `rebase` command therefore does not promise the same Roslyn post-rebase closure as `lower`.

## Review-hardening findings

The code-review pass before merge found five concrete closure issues and three responsibility extractions:

```text
1. unknown normalize intrinsic could leak semantic authority to Ruleset
   -> fixed in VSIR validation

2. companion startup health and updater repair disagreed on completeness
   -> one completeness authority shared across startup/updater/installer

3. GetCompilationAsync == null skipped validation
   -> fail closed

4. Roslyn symbol name inferred from filename
   -> semantic name carried from parsed VSIR

5. canonical docs described old scope
   -> release/context/README/experiment refreshed

organizational:
  -> split Roslyn helper host/planner/validator/manifest/arguments
  -> extract SemanticRefactoringCoordinator
  -> extract NamespacePathPolicy
```

These are review-hardening changes, not new product experiments.

## Follow-up watchpoints

Documented but intentionally not implemented in this PR:

- cancelling long Roslyn analysis should eventually terminate the child process tree and clean staging deterministically;
- reducing Roslyn workspace scope must not reduce blast-radius completeness;
- normalization beyond demonstrated pure transforms requires referential transparency or single evaluation;
- no generic compiler-repair engine, semantic refactoring framework, non-interactive approval vocabulary, Git-history ancestry reconstruction or provenance graph is introduced.

## Current status

The semantic surface exercised by TicketCode is:

```text
normalize trim
ensure non-empty
deterministic state construction
ordinal equality
```

The branch now also contains the target-context, namespace policy, conservative materialization rebase and known namespace-move semantic safety mechanisms discovered by the same consumer.

The final merge gate is not additional feature work. It is:

```text
review-hardened HEAD
-> green CI
-> final Ticket Support consumer smoke
-> freeze
-> merge decision
```
