# Document authoring, Part 2: relational artifacts

Status: experiment on PR #10, `experiment/document-authoring-part-2`, targeting `feat/v0.3.0-preview`.

The PR description is the accepted experimental contract. This note records its realization and remaining obligations, not a replacement definition of Docs Standard. Automated validation and Hernán's final consumer smoke are separate evidence. Do not mark the PR ready or merge it before that smoke succeeds.

## Authority and representation debt

- **Docs Standard** owns Document, Nexus and Continuity Path semantics, definition vocabulary and question identity. Candidate `nexus/*.yml` and `continuity-paths/*.yml` accompany the snapshot without becoming manifest-promoted Documents.
- **Template Standard** owns the configured Document materialization template.
- **Tooling** owns constrained loading, authoring, discovery, mutation and reconstruction mechanisms.

**Research-debt pin:** Nexus materialization, Continuity Path materialization, relational front matter, associated-artifact tables and Mermaid graph rendering remain realized directly by Tooling during this experiment. Their eventual Template Standard ownership/contract is unresolved. This is an explicit temporary boundary, not evidence that these representations are universal Docs Standard semantics. Migrating that boundary is not a prerequisite for this experiment.

## Creation and discovery

Standalone creation requires `--target` for every family:

```text
vslices new document <name> --kind <document-type> --target <target>
vslices new nexus <name> --kind <nexus-type> --target <target>
vslices new continuity-path <name> --kind <path-type> --target <target>
```

`--scope` is an explicit classification. Without it, a compatible source scope or a single distinct admitted definition scope may be used. Ambiguous or absent knowledge remains unset: Tooling does not choose the first of several scopes, copy an incompatible scope, or treat a Path's type as its target classification. Current candidate Path definitions do not declare scopes.

Recommendations prepare one of:

```text
vslices new document --from-nexus <artifact>:<selection>
vslices new nexus --from-nexus <artifact>:<selection>
vslices new document --from-path <artifact>:<selection>
vslices new nexus --from-path <artifact>:<selection>
```

The recommendation owns family/type; an explicit conflicting `--kind` fails. Target is inherited unless overridden with `--target`. The recommendation supplies the default relation role, which concrete `--role` may replace.

Arbitrary associations remain available on all three standalone creation commands through `--related-to <artifact> --role <text>`. Recommendations are neither a whitelist nor a completeness requirement. `--related-to` requires a nonempty role; a role without a relation source is ignored. Source modes are mutually exclusive.

Discovery displays the current artifact's questions, recommendations and associated artifacts. It prepares commands but does not recursively discover the linked artifacts. An unmaterialized recommendation offers `new`; a materialized recommendation offers `discovery`. Reverse and non-recommended associations remain visible independently of recommendation slots.

A Nexus may have no open questions. Its associated-artifact table is still present. Nexus descendants become available as their parent questions are answered/defaulted.

A Continuity Path exposes the complete traversal question graph and its recommendations even when questions are unanswered. A trajectory is not a Document completion checklist. Its purpose and recommended traversal are defaulted editable regions, followed by the root question and structured descendants. The complete graph includes node-local recommendations and `connection.text` edges. Mermaid is only the current rendering mechanism.

`update nexus` and `update continuity-path` replace an available question answer. They cannot mutate recommendations or accept arbitrary Mermaid edits as graph semantics. Existing question regions and associated tables must be reconstructible; malformed or missing reconstruction markers cause a diagnostic rather than guessed repair.

## Metadata and ephemeral selections

New artifacts preserve kind, type, target, known scope, initial `draft` status, empty/concrete relations, Tooling provenance, schema version `0.1.0` and materialization template name/version. Legacy minimal Document front matter remains readable; the stronger target requirement applies to creation, not an invented historical target.

Structured discovery selections such as `1`, `1.1` or `3.1.2.1` are ephemeral positions, not persistent semantic identity. Relations created from recommendations carry the source selection plus a **Tooling-owned definition-context fingerprint**. This guards reconstruction of the selection; it does not promote the selection or fingerprint into Docs Standard identity. The fingerprint covers structural ordering, question IDs and recommendation family/type/role, not question wording or current answers.

A changed or unknown fingerprint fails closed (`RELART030`) rather than silently retargeting an existing association. Old WIP recommendation records without the context fingerprint need explicit reconciliation; this experiment does not invent ancestry or introduce an automatic migration/repair command.

## Write guarantees and limits

Creation prepares the new artifact and reciprocal source update before publishing either. The existing transactional file writer checks the captured source hash, stages both changes and rolls back handled commit failures. No success message precedes the transaction result. Tests cover stale input, cancellation, preparation failure and a second-write failure after the first commit, including restoration of original bytes or removal of a newly created file.

This is handled-failure rollback, **not** a claim of crash-atomic multi-file filesystem transactions or exclusion of every concurrent external writer. If rollback itself fails, the underlying writer reports the incomplete recovery and retained backups. Do not describe those stronger guarantees as implemented.

Structural YAML validation rejects unknown keys, wrong node shapes, conflicting recommendation families, missing role/type, duplicate question IDs and malformed candidate surfaces. It does not silently filter malformed entries out of the model. Generated relational artifacts are reconstructed before publication so invalid default-marker content cannot produce a successful but unreadable artifact.

## Automated evidence

The CLI tests use isolated local Docs Standard and Template Standard fixtures and invoke the real `vslices` executable. They cover Part 1 compatibility with explicit targets, metadata, zero/deep-question Nexus cases, both recommendation families from both source families, full unanswered Path navigation, bidirectional relations, role overrides, arbitrary associations, source failures, malformed definitions, snapshot replacement integrity and stale recommendation context. Transaction tests exercise the same writer used by artifact creation.

Synthetic definitions test mechanism behavior, not conformance of every present or future external definition. Native AOT builds and the final real consumer smoke remain separate checks.

## Final consumer smoke (human gate)

From a disposable or backed-up VSlices project with the desired configuration:

```powershell
vslices update self --channel build --pull-request 10
vslices --version
vslices update docs-standard
vslices update template-standard

vslices new nexus smoke-capability --kind capability --target "Smoke capability"
vslices discovery nexus smoke-capability

vslices new continuity-path smoke-domain --kind domain-context --target "Smoke domain"
vslices discovery continuity-path smoke-domain
```

Copy the actual prepared commands from discovery; do not assume fixed selections from this note. Exercise a Document and a Nexus recommendation, inspect each created artifact, then rediscover the source and check the `new` to `discovery` transition and reciprocal association. Exercise one question update from its emitted command and verify that Path graph text remains unchanged when only an answer changes.

Finally create a non-recommended association using an actual artifact path:

```powershell
vslices new document smoke-context --kind context --target "Smoke context" --related-to <existing-artifact> --role "Concrete smoke relation"
```

Verify both metadata records and the associated-artifact table where applicable. Record the actual CLI version, installed standards, commands/selections and outcome. Only then evaluate readiness/merge.
