# Rulesets and project semantic extensions

VSlices separates revisable upstream target knowledge from project-owned semantic extensions.

```text
VSIR
  = semantic document

vslices/ruleset-standard
  = official revisable lowering knowledge

project/.vslices/ruleset-standards/<name>
  = independently installed source-owned snapshots

project/.vslices/ruleset-standards/local/<name>
  = original editable local lowering standard

project/.vslices/ruleset
  = legacy singleton snapshot

project/.vslices/extensions
  = project-owned semantic extension overlay

vslices/tooling
  = source acquisition, validation, execution mechanisms, coordination and safety
```

A missing rule means unsupported/unresolved lowering. It never authorizes an embedded fallback or semantic invention.

## Ownership and lifecycle

Ruleset Standards and project semantic extensions intentionally have **different
authorities and lifecycles**. Multiple independently named standards can coexist
under `.vslices/ruleset-standards/`: `vslices new ruleset-standard [name]`
creates an editable local standard, while `--origin` imports a managed snapshot.
`vslices update ruleset-standard` updates imported snapshots after reviewing
a diff, without replacing project-owned originals.

The project extension overlay remains independent under `.vslices/extensions`.
It admits project semantics; no source-owned Ruleset Standard installation may
overwrite it. The legacy `.vslices/ruleset` directory and
`vslices update ruleset` command continue as a transitional singleton path.
See [the Ruleset Standard lifecycle specification](design/multiple-ruleset-standards-lifecycle.md)
for the complete contract.

At lowering time, validated rules are combined by exact `node`. Definitions
with different nodes coexist; definitions with the **same node** cause an
explicit conflict rather than silently overriding each other. Discovery uses
`<installation>.<node>` for ambiguity, but this qualifier does not invent new
VSIR intrinsic syntax.

Project extensions are explicitly referenced from their own manifest:

```yaml
# .vslices/extensions/manifest.yaml
version: 0.1
catalogs:
  - ticketing.yaml
```

A catalog may co-locate semantic admission and target realization:

```yaml
# .vslices/extensions/ticketing.yaml
extensions:
  - node: intrinsic.normalize-rut
    semantic:
      kind: normalize
    targets:
      csharp:
        mode: deterministic
        renderer: expression
        bindings: [value]
        template: "Rut.Normalize({value})"
```

The two blocks have different authority:

```text
semantic.kind
  -> admits an operation into the project validation context

targets.csharp
  -> realizes that already-admitted operation for C#
```

A target renderer alone cannot create semantic validity.

## Renderer binding contracts

Each target rule declares the values its renderer expects independently of the concrete template text:

```yaml
- node: projection.map
  mode: deterministic
  renderer: expression
  bindings: [source, bind, value]
  template: "{source}.Map({bind} => {value})"
```

`bindings` is target realization knowledge owned alongside the rule. Tooling does not hardcode a global `node -> bindings` vocabulary.

The current binding-placeholder lexical contract is exact and shared by Tooling and the official Ruleset CI:

```regex
[A-Za-z][A-Za-z0-9_-]*
```

Therefore:

```text
value         valid
source-value  valid
source_value  valid
_value        invalid
```

The same grammar is used to recognize `{placeholder}` names in templates. This is deliberately one cross-repository contract: a catalog must not pass the Ruleset CI with a placeholder name that Tooling cannot recognize, or vice versa.

The loader validates the contract before a Ruleset becomes active:

```text
bindings must be scalar, non-empty and unique
all template placeholders must be declared bindings
all declared bindings must be used by the template
render calls must supply exactly the declared binding set
```

Consequently a typo such as `{banana}` is an invalid Ruleset at load/update time rather than a malformed expression discovered later during materialization or compilation. Names outside the placeholder grammar similarly cannot form a valid used binding.

A constant renderer can explicitly declare `bindings: []`.

## Ruleset acquisition pipeline

`vslices update ruleset` owns both first installation and later refresh:

```text
origin
  -> source + optional ref
  -> RulesetSourceMaterializer
  -> RulesetSnapshotInstaller.Prepare
  -> validate prepared snapshot
  -> RulesetSnapshotInstaller.Replace
  -> record successful provenance
```

The project extension overlay does not participate in this replacement.

`vslices init` does not install Ruleset by default. Its `--ruleset-origin` and `--default-origin` shortcuts delegate to the same update operation rather than implementing another acquisition path.

The preferred compact GitHub origin is `owner/repository:ref`. Existing local directories and direct ZIP URLs may be used directly. The CLI exposes one explicit acquisition surface: `--origin`.

## Source and ref semantics

Current Ruleset source forms:

- existing local directory;
- direct HTTP(S) ZIP archive;
- supported GitHub repository URL.

For GitHub repository sources, `ruleset.ref` is resolved as a Git reference candidate by trying:

```text
branch
-> tag
-> direct archive/commit reference
```

Branch-first resolution preserves the normal experiment workflow. If a branch and tag have the same name, the branch currently wins; this ordering is explicit rather than accidental.

A local directory with `ref` is rejected. A generic direct ZIP with `ref` is also rejected. Tooling does not call a property `ref` while secretly assuming every value means `refs/heads/...`.

## Validate before swap

A candidate Ruleset must be consumable by the target loader **before** replacing the active snapshot.

For C# this means running the real equivalent of:

```text
CSharpLoweringRuleSet.Load(preparedSnapshot)
```

Validation rejects, among other current cases:

- missing manifest;
- missing selected target;
- declared rule file missing;
- path escaping the Ruleset root;
- non-scalar rule-file references;
- non-mapping rule entries;
- rule without node;
- duplicate rule node;
- unsupported mode;
- unsupported renderer;
- empty template;
- malformed or duplicate binding declarations;
- undeclared template placeholders;
- declared bindings unused by their template;
- Ruleset manifests attempting to own project `extensions`.

Only a fully validated prepared snapshot reaches replacement. Failure preserves the previous `.vslices/ruleset` and never mutates `.vslices/extensions`.

## Project extension loading

Project extension catalogs are loaded once from `.vslices/extensions` as one project-owned model. That loader is responsible for:

```text
extension manifest
referenced catalog graph
path containment
semantic declarations
target realizations
renderer binding contracts
structural validation
collision input
```

Malformed YAML is fail-closed. A non-scalar catalog reference, non-mapping extension entry, missing catalog, unsupported semantic kind or structurally invalid target realization is a diagnostic rather than silently disappearing from the model.

The loaded model then provides two views of the same declaration:

```text
VsirValidationContext
  <- semantic admission

CSharpLoweringRuleSet additional rules
  <- C# realization
```

This keeps physical co-location convenient without making the renderer semantic authority.

## Rule execution boundary

The first executable rule class remains deterministic expression rendering. For example, ordinal equality target expressions live in Ruleset nodes such as:

```text
equality.ordinal-equals.equals
equality.ordinal-equals.hash
```

Tooling owns the structural obligation to emit equality members when VSIR requires them; the concrete C# comparison/hash expressions remain external Ruleset knowledge.

The current extension experiment only admits project-owned `normalize` semantics. Do not infer a universal plugin framework from this surface.
