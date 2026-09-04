# External lowering rulesets

VSlices keeps revisable target-lowering knowledge outside the executable. The official repository is `vslices/ruleset`; a consumer uses a project-local snapshot under `.vslices/ruleset/`.

```text
VSIR
  = semantics

vslices/ruleset
  = official revisable lowering knowledge

project/.vslices/ruleset
  = local version-controlled snapshot

vslices/tooling
  = source acquisition, execution mechanisms, coordination and safety
```

A missing rule means unsupported/unresolved lowering. It never authorizes an embedded fallback or semantic invention.

## Shared acquisition pipeline

`vslices init` and `vslices update --ruleset` share the same lower-level mechanisms:

```text
RulesetSourceMaterializer
  -> materialize source

RulesetSnapshotInstaller.Prepare
  -> copy root files + selected target
  -> validate prepared snapshot

RulesetSnapshotInstaller.Replace
  -> atomic move
  -> backup / rollback on failure
```

`init` owns initialization policy. `update --ruleset` owns update policy. Source materialization and snapshot installation are shared mechanism rather than duplicated command behavior.

## Source and ref semantics

Current source forms:

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

A candidate ruleset must be consumable by the target loader **before** replacing the active snapshot.

For C# this means running the real equivalent of:

```text
CSharpLoweringRuleSet.Load(preparedSnapshot)
```

Validation rejects, among other current cases:

- missing manifest;
- missing selected target;
- declared rule file missing;
- path escaping the ruleset root;
- rule without node;
- duplicate rule node;
- unsupported mode;
- unsupported renderer;
- empty template.

Only a fully validated prepared snapshot reaches replacement. Failure preserves the previous `.vslices/ruleset`.

## Rule execution boundary

The first executable rule class remains deterministic expression rendering. For example, ordinal equality target expressions live in ruleset nodes such as:

```text
equality.ordinal-equals.equals
equality.ordinal-equals.hash
```

Tooling owns the structural obligation to emit equality members when VSIR requires them; the concrete C# comparison/hash expressions remain external ruleset knowledge.

Do not create a provider/plugin/package abstraction until a concrete source or target requires one.
