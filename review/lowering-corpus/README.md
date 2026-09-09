# Lowering review corpus

This directory is a review surface for **VSlices Tooling 0.2.0 lowering**.

The goal is deliberately concrete: a reviewer should be able to inspect real `.vsir` inputs, run the same CLI path used by CI, and compare each input with the generated C# materialization without reconstructing witnesses from C# test strings.

## What is authoritative here

- `artifacts/*.vsir` are the review inputs.
- `corpus.tsv` records each artifact's target namespace, provenance, and the capability it is meant to expose.
- `ruleset/` is an exact review snapshot of `vslices/ruleset@e2f5ea85283cc3a91e8c87107a06e0c23ccc1668` (the merge commit of Ruleset #4).
- `Generate.ps1` invokes the **actual VSlices CLI** with `lower ... --stdout` for every corpus entry.
- generated `.vsir.cs` files are evidence produced by a particular Tooling + Ruleset cut; they are intentionally not committed as semantic authority.

The consumer-derived entries pin their source evidence to:

`atom-dev-serviu/access-management-product@7bae60d7e1af637cfcac6a802f867bc6979da444`

The structural review entries come from the executable Tooling test witnesses that broaden coverage beyond those exact consumer files. `corpus.tsv` distinguishes the two explicitly.

## Generate locally

From a checkout containing this directory:

```powershell
pwsh ./review/lowering-corpus/Generate.ps1 `
  -Vslices C:\path\to\vslices.exe
```

A framework-dependent development build is also accepted:

```powershell
pwsh ./review/lowering-corpus/Generate.ps1 `
  -Vslices ./src/VSlices.Tooling/bin/Release/net10.0/vslices.dll
```

The output defaults to `review/lowering-corpus/generated/` and contains:

```text
generated/
  MANIFEST.md
  corpus.tsv
  checksums.sha256
  inputs/       # exact .vsir files reviewed
  outputs/      # C# emitted by `vslices lower ... --stdout`
  logs/         # stderr/stdout evidence from the CLI invocations
  context/      # .NET target-context project used for nominal resolution
  ruleset/      # exact Ruleset snapshot used by the run
```

## CI artifact

`.github/workflows/lowering-review.yml` runs the same `Generate.ps1` flow on pull requests and uploads the resulting directory as a downloadable Actions artifact.

That artifact is intended for manual release review: open `inputs/Foo.vsir` beside `outputs/Foo.vsir.cs` and judge the materialization itself.

## About `NominalTypes.cs`

Some witnesses refer to target-visible nominal types such as `Rut`, `Commune`, `TicketSearch`, or `AccountReference`. The production CLI correctly asks the .NET target adapter to resolve those names through a related `.csproj`.

`NominalTypes.cs` exists only to provide that **target symbol context** to Roslyn/MSBuild for this isolated review project. It does not add VSIR semantics, target rendering rules, members, invariants, or business behavior. The `.vsir` remains the semantic source and the pinned Ruleset remains the C# realization vocabulary.

The generated outputs are not compiled together with these placeholders because several placeholders intentionally have the same nominal names as artifacts being reviewed. Compilation evidence for generated product/sum pipelines remains covered separately by `GeneratedMaterializationCompilationTests` against the Framework submodule pinned by Tooling.
