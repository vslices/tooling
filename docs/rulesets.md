# External lowering rulesets

VSlices tooling keeps lowering knowledge outside the executable so target mappings can evolve without republishing the CLI.

The current experimental project layout is:

```text
.vslices/
  ruleset/
    manifest.yaml
    manifest.schema.json
    csharp/
      intrinsics.yaml
```

`manifest.yaml` is project-local, editable, and intended to be version-controlled. Its schema is also external; the executable does not embed the manifest schema or the concrete lowering rules.

The current bootstrap contract is intentionally small: tooling discovers `.vslices/ruleset/manifest.yaml`, and a target lowerer reads the files declared for that target.

## Initialization

`vslices init` initializes `.vslices/ruleset` from external state.

Current experimental sources are either a local ruleset directory or an HTTP(S) ZIP archive:

```text
vslices init --from ../my-ruleset
vslices init --from https://example.invalid/vslices-ruleset.zip
```

The source can also be supplied through `VSLICES_RULESET_SOURCE`, allowing plain `vslices init` without compiling a source URL into the CLI.

A later official ruleset repository can therefore become the default operational source without moving its lowering knowledge into the executable. The current implementation deliberately does not introduce a package manager, dependency resolver, or remote plugin system.

## Current rule execution surface

The first executable rule shape is deliberately narrow:

```yaml
rules:
  - node: intrinsic.non-empty
    mode: deterministic
    renderer: expression
    template: "!string.IsNullOrEmpty({value})"
```

The C# lowerer resolves the semantic node and supplies its named bindings. The target expression itself comes from the project-local ruleset.

If a required deterministic rule is absent, lowering stops with a diagnostic instead of falling back to an embedded implementation.

This is an exploratory boundary, not a commitment that every future lowering decision should be expressible as an expression template. New execution primitives should be added only when concrete VSIR nodes require them.
