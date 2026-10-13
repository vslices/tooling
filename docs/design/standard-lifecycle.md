# Standard lifecycle — cross-product design contract

Status: **cross-family design contract; Docs Standard and Ruleset Standard projections implemented on PR #16, pending integration**  
Date: 2026-10-13  
Scope: independently installable, named, versionable VSlices standards managed by Tooling.

## Why this abstraction exists

The Docs Standard multiple-installation design revealed a recurring **lifecycle problem**, not a Docs Standard-specific semantic problem. A project may need multiple independent standards of the same family, sourced externally or authored locally. Their coexistence should provide extensibility without replacing or overriding an existing standard.

This contract has two concrete Tooling projections: [Docs Standard](multiple-docs-lifecycle.md) and [Ruleset Standard](multiple-rulesets-lifecycle.md). Ruleset Standard uses the renamed repository `vslices/ruleset-standard` while preserving the legacy manifest kind and project extension boundary. **Template Standard** remains future work. Do not treat branch projections as shipped until the PR is integrated.

## Layers of responsibility

- **Standard family owner**: defines the meaning, manifest format, admissible definition kinds, validation semantics, identity declaration, and what an empty valid source means.
- **Common Tooling lifecycle**: manages names, sources, installation registry, storage classes, acquisition, snapshot replacement, comparison presentation, confirmation, deletion and diagnostics.
- **Family adapter**: validates through the family-specific loader, describes semantic differences, exposes family-specific discovery, checks dependent consumer artifacts and carries family-specific migration conditions.
- **Consumer project**: owns local editable definitions and their decisions. Tooling may manage installed external snapshots but must not silently overwrite original local authoring.

Shared mechanics do not give Tooling authority to invent Ruleset vocabulary, Docs Standard questions, or Template Standard materialization semantics.

## Vocabulary and invariants

**Family**: an admitted kind of standard, e.g. `docs-standard`, planned `ruleset-standard`, `template-standard`.

**Declared name**: `name` in the source's manifest. Its schema/grammar is owned or jointly contracted with the family, not inferred from an archive filename.

**Installation name**: the effective identifier within one family and project. Defaults to declared name for imported standards; explicit positional `[name]` overrides the installation identity, **not** the source manifest. For brand-new local standards the chosen name is also written to the newly created manifest.

**Source**: a GitHub repository/ref, supported archive or physical directory. A physical source imported through `--origin` is still a managed external-origin snapshot; a standard born without `--origin` is project-owned and editable.

**Registry**: stores effective identity, declared name, family, source kind, origin/ref where applicable, and enough provenance to update reproducibly.

**Namespace**: installation names must be unique **within a family** across externally acquired and local installations. Different families may reuse the same name because commands qualify the family. Collisions are rejected without mutations and report how to specify another `[name]`. No silent overwrite, priority order or automatic rename.

**Coexistence**: additional standards do not semantically override others. If a consumer resolves named definitions across multiple standards, the short definition name is available only if unambiguous; otherwise require `<installation-name>.<definition-name>`. Qualification format/escaping and persisted identity are family-specific where necessary.

**Safety**: prepare and validate candidates before changing installed state. Failure preserves prior installation and registry entry. Deleting a project-owned local standard risks original work and needs stronger warning/guards than deleting an imported snapshot.

## Project storage convention

For a family with plural collection directory `<family-collection>`:

```text
.vslices/
  <family-collection>/
    <installation-name>/              # managed snapshot acquired via --origin
    local/
      <installation-name>/            # original project-owned, editable standard
```

The mapping from command family to collection directory is explicit in each projection; do not automatically pluralize CLI command names. Both classes share one logical registry and namespace. A local source is not mirrored as a second managed snapshot.

The `local` segment is reserved for storage classification, not a user-installable standard identity in a way that would conflict with this layout. The exact metadata registry schema is an implementation detail, but it must preserve provenance without duplicating or rewriting source semantics.

## Generic lifecycle commands

Examples use `<family>`; names of specific CLI commands must be projected and accepted per family.

### `new <family> [name] [--origin <source>]`

- Without `--origin`: generate a brand-new, valid, editable local source in `.vslices/<family-collection>/local/<name>/`. Prompt for a missing name interactively; noninteractive execution requires explicit name. Do not fabricate semantic content merely to satisfy a manifest loader; the family defines a valid minimal scaffold.
- With `--origin`: acquire an **existing** standard, validate through that family, select the declared manifest name or positional override, then install and register a managed snapshot.
- Missing physical origin: warn, do not mutate, and never report installation success. Exit status policy is still to be settled for automation.
- An existing path with an invalid manifest is a validation failure, not a missing-path warning.
- Name collision always refuses creation, explains conflict, and advises explicit different name.
- No `--force` on `new`: create-only means no replacement.
- `new` does not imply a semantic inheritance or override relationship.

### `update <family> [name]`

- With name: refresh an existing managed installation from recorded origin/ref; an explicitly provided origin may replace that acquisition source for the *same installation identity*.
- Without name: target all managed externally acquired installations of the family; present a consolidated plan and ask once. Project-owned local sources are identified and skipped, since they are already edited directly.
- No installation: explain and direct to `new`; never implicitly install the official source.
- Default: prepare a human-reviewable Markdown diff report and ask for confirmation.
- `--std-diff`: present comparison in CLI; `--skip-diff`: omit diff. They are mutually exclusive.
- `--skip-confirm`: skip confirmation regardless of diff mode; otherwise default answer No. Noninteractive use without the flag fails safely.
- Prepare/validate candidates before prompting/applying. Updates are independently failure-safe per installation; report all attempted successes/failures and nonzero overall status for failures.
- Differences must preserve uninterpreted changes rather than falsely claiming semantic equivalence. Family adapters own semantic comparison, compatibility checks, and consequence reporting; common Tooling owns output mode, approval and storage/retention policy.

### `list <family-collection>`

List the family registry, including local/managed class, installation name, source/ref when relevant. Collection command spelling is projected explicitly.

### `discovery <family> [--<family> <name>] [--structure <name>]`

- Explore admitted definitions in one or more installed standards. Skip interactive standard choice if only one exists.
- Unique short structure name resolves; ambiguous names must be disambiguated; qualified names remain stable. Never use load order as precedence.
- The **family adapter** defines what a structure is, how its responsibility/content is shown, and whether `--structure` is applicable. Do not force Docs Standard question-tree output onto Ruleset or Template Standard.
- Default discovery respects each family's promotion/admission contract; physical file existence alone does not make a definition admitted.
- Explicit selection must also work noninteractively, or return actionable ambiguity.

### `delete <family> [name]`

Requires explicit name; confirmation unless `--skip-confirm`. For managed installations, remove the snapshot and registry entry only, never external source. For local installations, removal means deletion of original editable definitions: prominent warning and protection against active dependencies. Family adapter checks consumers; do not silently break persisted references.

## Shared mechanism, family-specific meaning

| Dimension | Common lifecycle | Docs Standard projection | Ruleset Standard / Template Standard |
|---|---|---|---|
| Installation identity | Manifest name + optional alias | `vslices-standard` proposed official name | Family-owned names, not yet specified |
| Storage | Named managed snapshots + `local/` | `.vslices/docs/` | Collection names to decide |
| Source acquisition | Local directory, GitHub/ref, supported archive | Existing Docs Standard materializer | Reuse mechanisms only where compatible |
| Validation | Validate before install/swap | Document/Nexus/Path/Support Note catalogs | Respect actual target loader or template contract |
| Diff | Markdown, CLI, none; confirmation independent | Documentary definition differences | Ruleset and template meanings must be defined separately |
| Discovery | Identity selection and ambiguity safety | Documentary structures/question graphs | No assumption of documentary structures |
| Existing local overlay | Do not overwrite project-owned work | Local standard is first-class | Current `.vslices/extensions` remains distinct from Ruleset sources |

The current `.vslices/extensions` semantic overlay **is not automatically a local Ruleset Standard** and must not be migrated or overwritten by applying this pattern. Ruleset Standard is now the public product/repository name, not permission to collapse responsibilities.

## Projection and implementation sequence

1. Keep this document as a **proposed cross-family lifecycle contract**, not a new normative standard-definition language.
2. Use the Docs Standard design as its **first concrete projection**, including documentary manifest and family-specific discovery/diff semantics.
3. Review the existing Ruleset Standard projection against the actual C# rule loader, preserving duplicate-node failure and the separation from `.vslices/extensions`. Preserve the existing extensions boundary.
4. Test it separately against Template Standard's actual template materialization/selection behavior.
5. Extract shared Tooling mechanisms only as common behavior is evidenced; avoid one generic engine that erases family-specific invariants.

## Open design questions

- Exact manifest `name` validation per family and alias grammar/qualified-reference representation.
- Family-collection directory naming and future `ruleset` -> `ruleset-standard` command/configuration migration.
- Empty-source validity per family.
- Missing local origin's stable automation exit code.
- Markdown diff report location/retention and semantic completeness guarantees per family.
- Dependency detection before deletion, cross-standard links and conflict management.
- Whether `--structure` is a suitable shared CLI term outside documentary families; not settled by the Docs Standard projection.

These uncertainties must remain visible rather than being silently decided through code reuse.
