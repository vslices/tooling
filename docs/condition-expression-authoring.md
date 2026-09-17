# Composed condition expressions

The `Name` corpus establishes that an `ensure` argument may be a semantic expression rather than only a direct input reference.

The canonical witness is:

```yaml
- ensure:
    condition:
      intrinsic: length-at-most
      args:
        value:
          intrinsic: concat-space
          values:
          - input.Names
          - input.FirstSurname
          - input.SecondSurname
        max: 92
    failure:
      message: El nombre debe tener 92 caracteres o menos
```

This means:

```text
concat-space(input.Names, input.FirstSurname, input.SecondSurname)
  -> semantic string
  -> length-at-most(value, 92)
```

`length-at-most` is not overloaded with collection behavior. The composition is explicit in the VSIR expression tree.

## CLI authoring

`construction` remains an ordered whole-boundary assertion, so the nested expression is authored through the same `set` operation:

```powershell
vslices update vsir FullNameLike `
  --set "construction=[{ensure: {condition: {intrinsic: length-at-most, args: {value: {intrinsic: concat-space, values: [input.Names, input.FirstSurname, input.SecondSurname]}, max: 92}}, failure: {message: 'El nombre debe tener 92 caracteres o menos'}}}, {refine: {state: {Names: input.Names, FirstSurname: input.FirstSurname, SecondSurname: input.SecondSurname}}}]"
```

`discovery` advertises the `ensure` argument as expression-valued:

```text
grammar ensure:
  {ensure: {condition: {intrinsic: <ruleset-intrinsic>, args: {value: <expression>, ...}}, failure: {message: <text>}}}
```

The canonical parser preserves the nested intrinsic expression instead of flattening it to target syntax. Validation checks references recursively. C# lowering recursively renders the nested relation through the active Ruleset before passing the resulting expression to the outer condition rule.

Missing nested target realization fails closed.

## Ruleset boundary

The same target node is reused across semantic contexts:

```text
intrinsic.concat-space
```

There is no separate `condition.concat-space` or `projection.concat-space` semantic relation. Context selects where the expression is consumed; the intrinsic itself remains one relation.

The VSIR language-level semantics are tracked in `vslices/intermediate-representation#1`.
