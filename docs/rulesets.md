# Rulesets and project semantic extensions

VSlices separates revisable upstream target knowledge from project-owned semantic extensions.

```text
VSIR
  = semantic document

vslices/ruleset
  = official revisable lowering knowledge

project/.vslices/ruleset
  = installed source-owned snapshot

project/.vslices/extensions
  = project-owned semantic extension overlay

vslices/tooling
  = source acquisition, validation, execution mechanisms, coordination and safety
```

A missing rule means unsupported/unresolved lowering. It never authorizes an embedded fallback or semantic invention.

## Ownership and lifecycle

`.vslices/ruleset` and `.vslices/extensions` intentionally have different lifecycle owners.

```text
.vslices/ruleset
  source-owned
  replaceable by init --force / update --ruleset

.vslices/extensions
  project-owned
  preserved by init --force / update --ruleset
```

This prevents a Ruleset refresh from deleting project semantics while keeping installed Ruleset knowledge reproducible from its configured source.

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

## Shared Ruleset acquisition pipeline

`vslices init` and `vslices update --ruleset` share the same lower-level Ruleset mechanisms:

```text
RulesetSourceMaterializer
  -> materialize source

RulesetSnapshotInstaller.Prepare
  -> copy root files + selected target
  -> validate prepared snapshot

RulesetSnapshotInstaller.Replace
  -> atomic move of .vslices/ruleset
  -> backup / rollback on failure
```

The project extension overlay does not participate in this replacement.

`init` owns initialization policy. `update --ruleset` owns update policy. Source materialization and snapshot installation are shared mechanism rather than duplicated command behavior.

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
