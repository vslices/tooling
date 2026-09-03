# External lowering rulesets

VSlices Tooling keeps lowering knowledge outside the executable so target mappings can evolve without republishing the CLI.

The official ruleset repository is `vslices/ruleset`.

A project consumes a local snapshot under:

```text
.vslices/
  ruleset/
    manifest.yaml
    manifest.schema.json
    csharp/
      intrinsics.yaml
```

The project-local ruleset is editable and intended to be version-controlled. Its manifest schema is also external; the executable does not embed the concrete manifest schema or concrete lowering rules.

## Architectural boundary

The intended separation is:

```text
VSIR
  = semantic source

vslices/ruleset
  = official, revisable lowering knowledge

project/.vslices/ruleset
  = local ruleset snapshot

vslices executable
  = discovery, execution and orchestration mechanisms
```

A useful working rule is:

- when a new operational capability is required, `vslices/tooling` may need to change;
- when new lowering knowledge is discovered for semantic structures the tooling can already execute, `vslices/ruleset` should normally change instead.

The executable may know how to execute supported classes of rule, but concrete target mappings should remain external whenever possible.

No rule means unsupported or unresolved lowering. It does not authorize fallback knowledge embedded in the CLI and it does not authorize an interpreter to invent semantics.

## Manifest and discovery

The current bootstrap contract is intentionally small: tooling discovers `.vslices/ruleset/manifest.yaml`, and a target lowerer loads the rule files declared for that target.

The current manifest starts with:

```yaml
$schema: ./manifest.schema.json
kind: vslices-ruleset
version: 0.1

targets:
  csharp:
    rules:
      - csharp/intrinsics.yaml
```

Both `manifest.yaml` and `manifest.schema.json` live outside the executable so their structure can evolve independently while the CLI remains small.

## Initialization

`vslices init` initializes `.vslices/ruleset` from external state.

The implementation currently accepts either a local ruleset directory or an HTTP(S) ZIP archive:

```text
vslices init --from ../my-ruleset
vslices init --from https://example.invalid/vslices-ruleset.zip
```

The source can also be supplied through `VSLICES_RULESET_SOURCE`.

The official repository `vslices/ruleset` is the intended normal bootstrap source. Integrating that repository as the default source for plain `vslices init` is part of the current iteration; until that wiring is complete, the explicit source mechanisms remain the executable contract.

Once initialized, lowering should operate from project-local state and should not require network access.

## Current rule execution surface

The first executable rule shape is deliberately narrow:

```yaml
rules:
  - node: intrinsic.non-empty
    mode: deterministic
    renderer: expression
    template: "!string.IsNullOrEmpty({value})"
```

The C# lowerer resolves the semantic node and supplies its named bindings. The target expression comes from the local ruleset.

The first official C# rules currently cover the deterministic intrinsics exercised by `StreetName.vsir`:

```text
intrinsic.non-empty
intrinsic.length-at-most
```

If a required deterministic rule is absent, lowering stops with a diagnostic instead of falling back to an embedded implementation.

This is an exploratory boundary, not a commitment that every future lowering decision should be expressible as an expression template. New execution primitives should be added only when concrete VSIR nodes require them.

## Validation properties

Ruleset support should increasingly prove the following properties:

```text
same VSIR
+ same ruleset
+ same target context
= deterministic output
```

It should also demonstrate that:

- removing a required rule makes lowering explicitly unsupported;
- changing an external rule can alter target materialization without recompiling the CLI;
- different authorized rulesets can produce different valid materializations of the same VSIR;
- initialized projects can lower offline;
- ruleset changes are visible and reviewable as ordinary version-controlled changes.

These properties are part of the evidence that lowering knowledge is genuinely external rather than merely duplicated outside the executable.
