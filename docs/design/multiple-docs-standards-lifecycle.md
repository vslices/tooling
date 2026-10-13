# Multiple Docs Standards: lifecycle and discovery contract

Status: **design proposal, not implemented**  
Date: 2026-10-13  
Owner of CLI behavior: `vslices/tooling`  
Owner of manifest/definition semantics: `vslices/docs-standard`

## Problem and intention

The present Tooling model has one configured Docs Standard source/ref and an installed snapshot at `.vslices/docs-standard`. This makes adding project-specific documentary vocabulary look like replacing or modifying the installed standard. Instead, **multiple independent standards must coexist**. Extensibility comes from adding standards, **not** semantic overrides, priority ordering, overlays, or inheritance.

This proposal specifies CLI behavior before implementation. Examples are illustrative, not executable claims. Changes to the normative Docs Standard manifest require separate changes in its owning repository.

## Installation identities, sources, and directories

Each standard has a globally unique **installation name** across local and externally acquired installations. The source's `manifest.yaml` declares `name: <string>`; an optional positional `[name]` overrides the installation name **without editing the source manifest**. The declared name and selected installation name must remain distinguishable for provenance and reproducibility. `name` format, validation and compatibility details are to be agreed with Docs Standard.

Proposed layout:

```text
.vslices/
  config.yaml
  docs-standards/
    vslices-standard/         # externally acquired installed snapshot
      manifest.yaml
    atom-dev/                 # another external snapshot
      manifest.yaml
    local/
      serviu/                 # original, editable local standard
        manifest.yaml
        documents/
        nexus/
        continuity-paths/
        support-notes/
```

`local` classifies project-owned/editable definitions, **not** an arbitrary filesystem source: importing `--origin "C:/path/to/folder"` still creates an externally acquired managed installation snapshot at `docs-standards/<name>/`. Local standards exist once, at `docs-standards/local/<name>/`; there is no second installed copy to silently replace.

No installation name may collide with any name under either location. A collision—whether defaulted from a manifest or explicitly supplied—**stops without modifying files**, reports the name conflict, and tells the caller to specify a different `[name]`. Never auto-rename, replace or use `--force` on `new`.

The project registry must record each identity, source kind, and external source/ref where applicable. Names used for discovery are installation names; physical paths and manifest-declared names do not grant precedence.

## `vslices new docs-standard [name] [--origin <source>]`

Two modes, distinguished only by `--origin`:

1. **Without `--origin`**, create a brand-new editable local standard under `.vslices/docs-standards/local/<name>/`. If `[name]` is omitted, request a name interactively; in noninteractive contexts where it cannot be obtained, explain that a name is required. Generate a minimally valid manifest with the selected `name`, and initial family directories only as useful. Do not invent documentary definitions to satisfy a validator. Register it immediately so `discovery` can see it. Local authoring changes the source itself.
2. **With `--origin`**, acquire an existing standard from a GitHub repository/ref, archive, or physical directory; use the source manifest's `name` unless positional `[name]` overrides it. Validate candidate before installing a managed snapshot. Record origin/ref for later updates. A local directory passed via `--origin` is still a separately managed imported snapshot, not a project-owned local standard.

For `--origin` pointing to a nonexistent physical directory, display a **warning**, not a semantic-validation error, and make **no changes**. Distinguish this from an existing directory with an invalid manifest (validation error), unsupported origin, network error, or an installation-name collision. Exit-status handling of the missing-directory warning should be specified explicitly before scripting integrations; no successful installation may be reported.

Examples:

```sh
vslices new docs-standard
vslices new docs-standard serviu
vslices new docs-standard --origin "vslices/docs-standard:main"
vslices new docs-standard serviu-alt --origin "C:/standards/serviu"
```

**Do not expose `--force` on `new`**: it has no legitimate effect consistent with the create-only contract. No separate `--path` creation mode is needed.

## `vslices update docs-standard [name]`

- No `[name]`: update **all externally acquired** installed standards, presenting their plan and requesting confirmation. Project-owned `local/<name>` standards are reported as local and **skipped**: their editable content is already authoritative, and a remote updater must not overwrite it. If the project has only local standards, explain that there are no external snapshots to refresh.
- With `[name]`: target only the named installed standard. For an external standard, refresh from its recorded source/ref unless explicitly overridden with `--origin <source>`. A local-owned standard must not be silently converted or overwritten; report that its source is edited directly. Any future conversion/import/export is a separate explicit operation.
- `--skip-confirm`: do not prompt. Without it, default confirmation is **No**; noninteractive invocations fail safely with advice to supply the flag.
- Default diff mode: prepare a human-reviewable **Markdown update report**, then request confirmation.
- `--std-diff`: show the comparison in CLI output, then request confirmation.
- `--skip-diff`: do not produce a differentiator, but still request confirmation unless `--skip-confirm` is present.
- `--std-diff` and `--skip-diff` are mutually exclusive. Diff mode and confirmation are orthogonal; `--skip-confirm` does not imply `--skip-diff`.
- For all-target updates, show a consolidated plan and ask for confirmation once. Prepare/validate before replacing; update each external installation independently and preserve its prior snapshot on failure. Continue other targets, report per-standard outcomes, and return nonzero when at least one attempted update fails. Do not install an unregistered standard as a side effect of `update`.
- If no standards are installed, report this state and direct users to `new docs-standard`.

Diff reports should expose source/ref, added/changed/removed definitions, known semantic changes (responsibility, questions, cardinality, scopes, recommendations/relations where supported), unknown/uninterpreted changes, and **evidence-based** impacts on existing artifacts when available. Do not claim unchanged semantics merely because unrecognized fields were omitted from a comparison. Reuse the existing Update Document vocabulary only if its actual contract fits; otherwise explicitly document the projection. Report location/retention, including reports for rejected updates, remain an implementation-design decision.

Examples:

```sh
vslices update docs-standard
vslices update docs-standard serviu-alt
vslices update docs-standard serviu-alt --origin "../candidate"
vslices update docs-standard --std-diff --skip-confirm
vslices update docs-standard serviu-alt --skip-diff
```

## `vslices list docs-standards`

List every installed standard (local and external), effective installation name, source kind, source location and ref when available. This is a registry view, not a scan that grants undeclared files semantic authority.

## `vslices discovery docs-standard`

Interactively select a standard, then a structure and show its declared responsibility, available questions/graph, scope, cardinality, purpose, traversal, recommendations, relationships, family, status and provenance **where those fields exist for that family**.

- If there is only one installed standard, skip the standard selection.
- `--docs-standard [name]` selects an installed standard explicitly.
- `--structure [name]` selects a structure; the selection applies across Documents, Nexus, Continuity Paths and Support Notes. If unique across installed standards, it can be resolved without a standard selector. Otherwise request disambiguation interactively; noninteractive use reports candidates.
- Full identity uses `<standard>.<structure>`; the short `<structure>` form is valid only when unambiguous. No implicit first-match or priority by installation order.
- Distinguish **manifest-promoted** structures from repository-local candidates. Candidate YAML files do not become installed/admitted vocabulary just because they exist on disk. Discovery can expose candidates only through an explicitly designed, visibly labeled mode; default discovery describes promoted structures.
- Nexus need not have questions; its composition and recommendations matter. Continuity Paths expose a continuity graph; Support Notes expose their small auxiliary question surface. Do not force Document-shaped output on all families.
- Persisted artifact references should preserve qualified definition identity so a later installation cannot retroactively make them ambiguous.

Examples:

```sh
vslices discovery docs-standard
vslices discovery docs-standard --docs-standard serviu
vslices discovery docs-standard --structure integration-report
vslices discovery docs-standard --docs-standard serviu --structure integration-report
```

## `vslices delete docs-standard [name]`

Require explicit `[name]`; show intended target and confirm unless `--skip-confirm` is set. Remove only the named external snapshot and its registry entry, or the named project-owned local standard and its registry entry; **never** delete an external source directory/repository. Warn prominently that deleting a local standard removes its original editable definitions. Inspect existing references before deletion; block on active references by default until a dedicated, explicit override policy is designed. Default confirmation is No; noninteractive use requires `--skip-confirm`.

## Source manifest and promotion boundary

The Docs Standard owner must add a required `name: string` field (official manifest value proposed: **`vslices-standard`**), with identity validation. A newly created, empty local standard must be structurally valid; currently the Document catalog loader requires at least one registered Document. That requirement needs review at the owning semantics boundary before CLI scaffolding is implemented.

Existing `vslices/docs-standard/main` evidence at design time:
- `manifest.yaml` lists **16 Documents** and **four Support Notes** (draft, result, validation, risk).
- Nexus definition YAML exists (`capability-nexus.yml`, `service-consumption-nexus.yml`), but its README explicitly calls these candidates and they are **not** in the manifest.
- Continuity Path YAML exists (confirmed `business-driver.yml`); its README also describes candidate definitions not in the manifest.
- The inspected branches `feat/nexus-definition-spec` and `feat/support-notes-definition` do not provide a manifest promoting both candidate families.

**Do not merely add every YAML to the manifest**: inspect each candidate against its owning definition contract and decide promotion explicitly. When promoted, extend the manifest and snapshot installation/catalog discovery for Nexus and Continuity Paths. Keep `support-notes` registered; they are already there. Changes to that normative manifest belong to `vslices/docs-standard`, not this Tooling PR.

## Migration, safeguards, and implementation notes

Current Tooling code has singular `DocsStandardSource`/`DocsStandardRef`, fixed `.vslices/docs-standard` target, and catalog lookup at that path. This is a real storage/configuration and resolution redesign, not only argument parsing.

We are currently our own consumers, so a straightforward migration of existing project installations is acceptable. Preserve provenance and data; **do not presume an existing singleton snapshot is official**. Add explicit tests for migration rather than silently dropping old state.

Important invariants to test:
- Local and external installations coexist; global name collisions are rejected with actionable renaming advice.
- `new` never overwrites an existing standard; locally created standards are editable and never auto-refreshed.
- Missing physical origin warns without modifying project state; invalid existing origin fails validation.
- `update` without name selects all external snapshots, asks once, and skips project-owned local standards.
- Diff modes and confirmations behave independently; invalid candidate never replaces valid installed data.
- Short structure names resolve only if unique; fully qualified names are stable and order-independent.
- Family-specific discovery respects promoted manifest definitions and reports actual semantics rather than inferred ones.
- Deleting one standard does not affect another; deletion of local originals and referenced standards is guarded.

The command handlers should stay thin adapters. Reuse project context, origin resolution, candidate validation, snapshot safety and catalog mechanisms; do not introduce duplicated command-driven installation logic.

## Not decided in this document

- Exact grammar and constraints for manifest `name` and qualified structure names.
- How an empty standard is represented across manifest families.
- Whether the missing-local-origin warning returns a dedicated nonzero status or a warning-specific success status (must not claim installation success).
- Markdown report path, retention and relation to normative Update Document.
- Formal compatibility/dependency analysis between document instances and definition changes.
- Future intentional conversion of local-owned standards into managed snapshots, or vice versa.

These are **not** preconditions for implementing the already specified independent coexistence and lifecycle, except where directly required for a particular operation.
