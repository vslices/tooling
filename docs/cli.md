# VSlices CLI specification

Status: v0.3.0-preview interaction contract under active experiment.

VSIR language semantics are owned by [`vslices/intermediate-representation`](https://github.com/vslices/intermediate-representation). This document describes CLI behavior. Target realization knowledge belongs to [`vslices/ruleset`](https://github.com/vslices/ruleset). Migration traversal belongs to [`vslices/planifications`](https://github.com/vslices/planifications).

## 1. Public command surface

```text
vslices init

vslices new vsir <artifact>
vslices discovery vsir <artifact>
vslices update vsir <artifact> ...
vslices search ...

vslices transpile <artifact>
vslices rebase <artifact>
vslices lower <artifact-or-project>

vslices update self
vslices update ruleset
vslices update docs-standard
vslices update template-standard

vslices new document <name> --kind <type>
vslices discovery document <name>
vslices update document <name> --question-id <N> --answer "<markdown>"

vslices --version
vslices -v
```

`update` is a command group. The current contract does not include legacy `update --self` / `update --ruleset` aliases or one aggregate updater operation.

`vslices init` creates only the minimum project surface. Its configuration records `documents.template: markdown.question-tree` as the current default Document materialization policy. Ruleset, Docs Standard, and Template Standard are first-installed or refreshed by their own update commands. `init --ruleset-origin`, `--docs-standard-origin`, `--template-standard-origin`, and `--default-origin` are convenience composition over those same operations.

## 2. Core authoring protocol

The governing semantic authoring rule is:

> Declare only what is currently justified by evidence.

Searchable metadata is orthogonal to that semantic rule. `tags` may be authored at any point because tags do not assert domain meaning.

The interaction loop is:

```text
new
  -> establish version + semantic name only

discovery
  -> expose always-available tags metadata operations
  -> expose current semantic affordances
  -> expose command templates
  -> expose structured value grammar when needed

update
  -> atomically apply advertised metadata and/or semantic decisions
  -> validate the complete candidate before persistence
  -> produce next state

discovery
  -> evaluate again
```

See [`semantic-authoring-affordances.md`](./semantic-authoring-affordances.md) for the complete interaction model.

## 3. `new vsir`

`new vsir` always starts from zero semantic knowledge. Its only public input is the semantic name:

```text
vslices new vsir StreetName
```

It creates exactly:

```yaml
vsir: 0.1
name: StreetName
```

`new vsir` exposes no semantic shortcuts such as `--kind`, `--shape`, or `--classification`.

All metadata and semantic knowledge after identity flows through:

```text
new -> discovery -> update -> discovery
```

## 4. `discovery vsir`

`discovery` reports always-available artifact metadata plus the state-dependent semantic frontier. An entry can include path, status, meaning, value kind, allowed values, operations, command templates, and value grammar.

`tags` is always present as optional `set/add/remove` searchable metadata.

Discovery evaluates canonical conformance with the semantic validation environment of the project that owns the artifact. It does not evaluate target lowerability.

An artifact may be incomplete while also containing a present invalid assertion. Discovery preserves both facts: missing required paths do not erase parser/validator diagnostics for knowledge that is already present and invalid.

Discovery can also project candidate mutations without persistence through the same semantic mutation grammar used by `update`.

## 5. Public mutation semantics

`update vsir` uses:

```text
set
add
remove
```

`set` establishes or replaces an assertion. `add/remove` are reserved for genuine collection membership such as `tags` and `traits`.

Repeated options are preserved as distinct mutations in one atomic invocation:

```text
vslices update vsir TicketId --set shape=product --set classification=identifier
vslices update vsir TicketCode --set state.Value=string --set representation.Value=string --add traits=transform --add traits=identifier
```

All mutation routes converge on one candidate-validation boundary before persistence. Legitimate progressive incompleteness remains writable; an assertion that the canonical parser/validator already knows is invalid is rejected without modifying the artifact.

When a structural semantic type shorthand is enriched with field-local metadata, authoring expands it without destroying its type. For example:

```yaml
Value:
  sequence: string
```

plus a representation mapping becomes:

```yaml
Value:
  type:
    sequence: string
  mapping:
    # authored expression
```

rather than placing `sequence` and `mapping` as contradictory siblings.

## 6. Tags versus traits

```text
tags
  -> free-form operational metadata
  -> indexing, grouping and search
  -> never activates semantic obligations
  -> never guides lowering

traits
  -> constrained semantic capabilities
  -> may activate validation and authoring obligations
  -> participates in semantic interpretation
```

The public `VsirParser` validates and removes tags before canonical semantic interpretation. Conformance, `transpile`, `lower`, and `rebase` enter through that same artifact parser.

## 7. Canonical conformance versus public authorability

The current full public Domain Type authoring envelope is:

```text
kind: domain-type
shape: product
classification: value-object | identifier
traits: transform | identifier | refined
```

Classification and traits are independent semantic axes.

```text
TicketId
  classification: identifier
  traits: [transform]

TicketCode
  classification: value-object
  traits: [transform, identifier]
```

Both establish identifier capability and therefore require explicit equality, but target contracts are lowered from the primitive semantic facts independently:

```text
kind: domain-type
  -> DomainType<T, T.Repr>

identifier classification OR identifier trait
  -> Identifier<T>
```

Important distinction:

```text
canonical conformance
  = VSIR authority + canonical parser/validator + validation environment

public authorability
  = which progressive transitions discovery/update can currently guide
```

Real `sum`, `maintained`, and aggregate-root-sum witnesses already have parser/conformance/lowering coverage. Their **public authoring parity remains gated**; they must not therefore be labelled canonically invalid merely because discovery/update do not yet construct them end to end.

## 8. Strict canonical structural contract

Product, sum and maintained remain specialized canonical parsers. They share the structural obligations that must not diverge between forms:

- semantic mapping keys are scalar names;
- explicit `from` is either absent or a non-empty scalar semantic reference;
- a representation coordinate cannot declare both `from` and `mapping`;
- structurally malformed common field declarations fail closed rather than being interpreted as absent knowledge.

This is a shared contract, not a universal parser that erases shape-specific semantics.

## 9. Structured authoring and representation

Named semantic members use `set`. Structured semantic field declarations are admitted through discovery-advertised value grammars rather than generic YAML editing.

Derived state uses `state.<property>.from`. Representation can use direct `.from` or semantic `.mapping`; the two are mutually exclusive for one field.

Representation expressions currently exercised by the corpus include:

```text
stringify
represent
select
map
intrinsic
```

Tooling preserves the authored expression tree rather than silently inserting transformations.

## 10. Transform construction grammar

The current executable construction surface includes:

```text
normalize
ensure
resolve
apply
refine
```

`TicketCode` is the concrete normalize witness:

```text
vslices update vsir TicketCode --set "construction=[{normalize: {target: input.Value, intrinsic: trim}}, {ensure: {condition: {intrinsic: non-empty, args: {value: input.Value}}, failure: {message: 'Debes especificar el correlativo de la solicitud'}}}]"
```

The normalized semantic reference flows forward into later `ensure` and final state construction.

One semantic `apply` covers both direct and mapped/container shapes; target-specific `Apply` versus `ApplySeq` remains lowering/Ruleset knowledge.

`StreetExtension` additionally established intrinsic refinement with named ordered outputs. The language-level contract is in `vslices/intermediate-representation`; target helpers used to realize it do not become VSIR semantics.

## 11. Identifier and refined semantics

Identifier capability can be established through classification or trait. These declarations are structurally distinct but converge on the same discrete-space obligation and primitive `Identifier<T>` C# Framework contract. Domain-type membership is lowered independently as `DomainType<T, T.Repr>`.

Examples:

```text
vslices update vsir TicketId --set classification=identifier
vslices update vsir TicketCode --add traits=identifier
vslices update vsir TicketCode --set "equality={intrinsic: ordinal-equals, by: state.Value}"
```

`refined` remains trait-driven. `SrvIdentityId` combines `classification: identifier` with `traits: [transform, refined]`.

## 12. Search

Search filters use:

```text
<property>:<operator>:<value>
```

The motivating metadata case is:

```text
vslices search --filter tags:contains:ticket
```

Search is read-only and does not turn metadata into semantics.

## 13. Artifact lowering, output and lineage

```text
partial knowledge
  -> new / discovery / update
  -> .vsir artifact
       tags metadata -----> search/index only
       semantic VSIR -----> VsirParser -----> transpile / lower / rebase
  -> target witness
```

`rebase` is the conservative textual three-way primitive. `lower` owns the larger project workflow, including lineage policy and the currently demonstrated target-semantic closure.

Output selection applies to every successful path, including first-lineage bootstrap. If `--stdout` is requested while a human materialization is preserved during bootstrap:

```text
stderr
  -> bootstrap/status evidence

stdout
  -> requested preserved human materialization

disk
  -> human witness unchanged
  -> operational lineage may be established
```

A success status must not omit the result destination the caller requested.

## 14. Project lowering outcomes

`vslices lower <project>` evaluates selected artifacts under one project/Ruleset/extensions/target environment and preserves three outcome classes:

```text
Lowered
Unsupported
Failed
```

An unsupported semantic/target surface may be reported while other artifacts continue to lower; an unsupported-only batch can therefore complete successfully by design.

Environment, target-toolchain, IO and orchestration failures are not reclassified as unsupported. Processing may continue to collect useful diagnostics, but the project invocation returns non-zero if any artifact genuinely failed.

This distinction is part of the CLI automation contract.

## 15. .NET target context

An explicit `--namespace` overrides namespace derivation, not the existence of the related project. If a `.csproj` is discoverable its identity remains in target context for nominal type resolution and other .NET-native facts.

Nominal dependencies are collected by traversing the admitted semantic model, including nested unary semantic types and sum variants, rather than by maintaining a root-field-only list in the .NET adapter.

## 16. Lowering evidence

A lowerer returning success and producing expected text fragments is not sufficient evidence that the generated target is coherent. The v0.2.0 test surface includes generated-materialization compilation witnesses against the Framework submodule pinned by Tooling, including derived state projection and multiline failure literals.

Current important witnesses include:

```text
Location
  -> structured state/representation, resolve, apply, refine

TicketId
  -> identifier classification, equality, direct product input-to-state

TicketCode
  -> value-object + identifier capability, normalize trim, ensure, equality

SrvIdentityId
  -> identifier + refined composition

StreetExtension
  -> intrinsic refinement bindings

Name
  -> sum executable coverage

IdentityType
  -> maintained executable coverage

SrvIdentity
  -> aggregate-root sum executable coverage

TicketTrayFilter
  -> structural optionals + explicit represent projection
  -> no implicit flatten-single-field rule
```

Unknown semantics remain unknown; Tooling does not infer target or domain authority from convenience conventions.


## 17. Document materialization

`new document` and the materialization of newly activated questions during `update document` currently combine three authorities:

```text
installed Docs Standard
  -> Document type + root question

project configuration
  -> documents.template

installed Template Standard
  -> materialization definition
```

The first executable materialization witness is `markdown.question-tree`.

For its current supported surface, Tooling reads the template's question presentation and initial reconstruction contract:

```yaml
representation:
  question:
    presentation:
      kind: heading
      text:
        source: question.text
      level:
        strategy: semantic-depth
        root: 1
    answer:
      region:
        starts: after-question-heading
        ends: before-next-materialized-question-heading
      empty: unanswered

reconstruction:
  question-identity:
    root:
      strategy: document-type-root-question
```

Tooling owns the execution mechanism. It no longer hardcodes Markdown heading depth independently in `new document` and `update document`: both project semantic depth through the same configured Template Standard definition.

Front matter remains Tooling-owned in this slice:

```yaml
---
artifact:
  kind: document
  type: context
---
```

New Documents no longer persist a root placeholder marker. The minimal unanswered form is now:

```markdown
---
artifact:
  kind: document
  type: context
---

# ¿Dónde existe?
```

For this first markerless reconstruction slice, Tooling combines:

```text
artifact.type
+ Docs Standard root identity
+ configured Template Standard root geometry
+ no significant body content after the root heading
-> root is materialized + unanswered
```

The visible root wording is presentation rather than durable identity; `artifact.type` and the Docs Standard define which root question the heading represents.

Existing `vslices:placeholder` markers remain readable as historical compatibility.

Root answers are now markerless as well. For the current `markdown.question-tree` witness, Tooling reconstructs the root answer region from the Template Standard contract:

```text
after root heading
→ root answer begins

next materialized descendant heading
→ root answer ends

no materialized descendant
→ root answer ends at EOF

only whitespace in region
→ unanswered

significant content in region
→ answered
```

Updating an already answered markerless root replaces only that region and preserves later materialized descendants.

Answered descendant questions still use their current VSlices question markers in this slice. Arbitrary headings inside answer content are outside the current contract; Tooling does not attempt to infer whether such headings are documentary vocabulary. A future project-level Docs Standard extension mechanism is the intended direction when real documents require additional documentary vocabulary.

The current CLI intentionally exposes no `--template` or `--format` override. Per-artifact, per-type, inferred, migration-aware template selection, arbitrary answer headings, branching markerless reconstruction, and Docs Standard extension authoring remain non-scope.


## 18. Document question cardinality

Docs Standard questions may optionally declare:

```yaml
cardinality: many
```

Omitted cardinality and explicit `cardinality: one` both mean one AnswerInstance per question occurrence.

The current CLI recognizes both values and exposes them through Document discovery:

```text
[2] ¿Qué términos usamos?
  status: available
  cardinality: many
  action: multiple-answer authoring is not supported in the current preview
```

This slice deliberately does not materialize multiple AnswerInstances yet.

For `cardinality: one`, existing authoring behavior remains unchanged.

For `cardinality: many`, `update document` currently fails closed with `UPDATE109` rather than silently treating the question as singular. Discovery does not emit a mutation command that Tooling cannot yet honor.

Any cardinality other than `one` or `many` is rejected while loading Docs Standard with `DOCS027`.

The next pressure is not parsing cardinality but representing repeated AnswerInstances and the child QuestionOccurrences scoped through each answer.
