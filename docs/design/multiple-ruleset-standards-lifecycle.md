# Multiple Ruleset Standards — lifecycle and lowering projection

Status: **Tooling implementation proposal + initial realization on PR #16**, pending the normative `vslices/ruleset-standard` manifest change and release review.
Date: 2026-10-13
Source abstraction: [Standard lifecycle](standard-lifecycle.md)

## Purpose and ownership

This is the **Ruleset Standard-specific projection** of the shared standard lifecycle, alongside [Docs Standard](multiple-docs-lifecycle.md). It is not a new layer of ruleset semantics. The `vslices/ruleset-standard` repository owns its rule manifest and lowering vocabulary, while Tooling owns acquiring, validating, installing, discovering, comparing and retiring versions.

Ruleset Standard remains distinct from `.vslices/extensions`, which is a project-owned **semantic extension overlay**, not an imported/local lowering standard. Importing or updating a standard must not overwrite or migrate project extensions.

The repository was renamed from `vslices/ruleset` to `vslices/ruleset-standard`. The official source is now `https://github.com/vslices/ruleset-standard` at `main`. The existing manifest kind `vslices-ruleset` remains its language discriminator; the product/repository rename does not itself justify a schema-kind rename.

## Installations and names

The official manifest will declare `name: vslices` (see [normative PR #5](https://github.com/vslices/ruleset-standard/pull/5)). A positional `[name]` overrides only the installation identity, without mutating the source manifest. Names must be unique across local and imported Ruleset Standards, independent of Docs Standard names.

```text
.vslices/
  rulesets/
    registry.yaml
    vslices/        # managed imported snapshot
      manifest.yaml
      csharp/
    local/
      serviu/                        # project-owned editable original
        manifest.yaml
  extensions/                        # separate, project-owned semantic overlay
  ruleset/                           # readable legacy singleton, pending migration
```

A new local standard can begin with an empty `targets: {}` and no fabricated rules. No target may be lowered from that standard until its target rules are declared and validated. Importing a physical source directory with `--origin` is a **managed snapshot**, not the project's local authoring copy.

## Commands

```sh
vslices new ruleset-standard serviu
vslices new ruleset-standard --origin "vslices/ruleset-standard:main"
vslices new ruleset-standard alternative --origin "../external-rules"
vslices list rulesets
vslices discovery ruleset-standard
vslices discovery ruleset-standard --ruleset-standard alternative --structure intrinsic.non-empty
vslices discovery ruleset-standard --structure alternative.intrinsic.non-empty
vslices update ruleset-standard
vslices update ruleset-standard alternative --std-diff
vslices update ruleset-standard alternative --skip-diff --skip-confirm
vslices delete ruleset-standard alternative
```

### New

Without `--origin`, creates `local/<name>/` and a valid minimal named manifest. In noninteractive contexts, omitted name fails with a direction to specify one. With `--origin`, acquires a valid existing standard, uses `manifest.name` unless explicitly aliased, validates the configured target through the current lowering loader, stages and registers a managed snapshot. Missing physical source path produces a warning and **no installation**; an existing invalid source produces a failure.

A conflicting installation identity rejects the operation, suggests another `[name]`, and makes no changes. `new` does not have `--force`.

### Update

No name means all managed installations of this family, not Docs Standards; locally authored standards are skipped and never overwritten. A name selects one installed standard. An explicit `--origin` is permitted only with a name and changes that installation's acquisition provenance after successful validation.

The default review mode writes a Markdown Ruleset Standard update report under `.vslices/reports/ruleset-standard-updates/`. `--std-diff` displays it in the CLI; `--skip-diff` omits it. Both are mutually exclusive. Confirmation remains required in every mode except `--skip-confirm`; noninteractive confirmation fails closed. Reports compare admitted C# `node` identity and its mode, renderer, bindings, template, and source-file changes. Unknown compatibility effects are not silently declared safe.

Prepare every candidate before approval; apply each update with its own backup and rollback. A broken source never replaces the previous snapshot. Failure of one installed source must not stop independent valid updates. Project-owned `.vslices/extensions` remains untouched.

### List and discovery

`list rulesets` prints installation identity, source kind, provenance and reference.

`discovery ruleset-standard` asks which standard to inspect unless there is only one; then which admitted lowering rule (`node`). `--ruleset-standard [name]` and `--structure [name]` select those elements directly. C# nodes inherently use dots, e.g. `intrinsic.non-empty`; a fully-qualified reference is `<installed-standard>.intrinsic.non-empty`. Matching the *whole* node name before disambiguation avoids splitting the semantic node at its first dot.

Detail includes target, mode, renderer, template and binding contract, not documentary questions. Discovered rules must be manifest-reachable and accepted by the current C# ruleset validator.

### Delete

`delete ruleset-standard [name]` removes one named installation after confirmation (or `--skip-confirm`). An imported source directory/repository is never deleted. For a local standard, warn that its **original authored definitions** will be removed. Tooling currently conservatively refuses to delete a standard containing C# rules while the project contains VSIR artifacts whose dependencies could be affected. This is a safe restriction until a complete rule-use dependency model is available.

## Actual lowering semantics and acknowledged limits

Multiple Ruleset Standards do not grant precedence to one another. For the current C# target, Tooling combines individually validated rule sets by exact `node`. Nodes present in one standard remain available unqualified; **duplicate node names across installations fail lowering** with a diagnostic naming the competing standards. No arbitrary last-writer-wins replacement is allowed. The project extension overlay is applied after the admitted standard rules with the same existing duplicate-rule rejection.

The discovery qualifier `<standard>.<node>` is an **inspection identity**, not a new VSIR semantic construct. A future design may support intentional selection for conflicting nodes, but this proposal does not invent qualified VSIR intrinsic syntax.

C# is currently the only target with a complete snapshot validator. An empty local standard or a source for a future target does not silently enable unsupported lowering.

## Compatibility and transition

The historical `vslices update ruleset`, `init --ruleset-origin`, `VSLICES_RULESET_SOURCE`, and `.vslices/ruleset` remain supported as **legacy** singleton mechanisms while users transition. The canonical new entrypoints are `new|update|discovery|delete ruleset-standard`, `list rulesets`, `init --ruleset-standard-origin` and `VSLICES_RULESET_STANDARD_SOURCE`. `init --default-origin` uses the new named source path.

The new registry recognizes a legacy singleton with its original provenance and does not assume any locally customized snapshot is official. An explicit lifecycle mutation can migrate the old snapshot into the named collection. Source normalization points to `vslices/ruleset-standard`, preserving historical artifact snapshots and their evidence.

## Tests and evidence

Integration tests should cover: local empty authoring; imported name and alias; collisions across storage classes; absent/invalid physical origin; partial update failure isolation; all-target update versus local skip; confirmation/diff independence; discovery of dotted node names and ambiguity; disjoint lowering; colliding rule nodes; extension-overlay isolation; deletion guard; legacy preservation; and official source URL/ref behavior.

This is the first **Ruleset Standard realization** of the abstract lifecycle. Template Standard has not yet been projected or implemented. Shared mechanics may later be refactored once actual pressure warrants it, without collapsing the family-specific validators into a generic YAML interpretation.
