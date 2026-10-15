# CLI schema: machine-readable command contract

The **running CLI** is the authority for command names, options, argument positions, aliases, types, defaults and help descriptions. Starting with the implementation in [Tooling PR #16](https://github.com/vslices/tooling/pull/16), it exports ConsoleAppFramework's own registration metadata:

```sh
vslices --version
vslices --cli-schema > cli-schema.json
```

This is a root-level control, not an independently maintained command definition. `--cli-schema` outputs a JSON **array** of `CommandHelpDefinition` entries; its field shapes come directly from the pinned [ConsoleAppFramework.CliSchema](https://github.com/Cysharp/ConsoleAppFramework) package (5.7.13). It does not require a VSlices project. Human color/brand/status output is intentionally suppressed, and the JSON serializer uses the package's source-generated AOT-safe `CliSchemaJsonSerializerContext`.

For example:

```sh
vslices --cli-schema | jq -r '.[].CommandName'
vslices --cli-schema | jq '.[] | select(.CommandName == "read document")'
vslices read document --help
```

The schema is **not** a complete semantic operation contract. The `IsRequired` member is derived from signature defaults rather than operation-level checks. It does not encode confirmation, valid current selections, file writes, compatibility, diagnosis codes, semantic authority or inter-command traversals.

## Versioned artifacts in CI

`.github/workflows/ci.yml` builds the CLI, tests it, exports its **actual executable** schema, verifies representative registrations and uploads one artifact per source commit:

```text
cli-schema-<github-sha>/
  cli-schema.json        # generated from the built executable
  cli-version.txt        # reported by the same executable
  metadata.json          # CLI version, exact source commit, schema type
```

Find artifacts through [CI workflow runs](https://github.com/vslices/tooling/actions/workflows/ci.yml). The artifacts are **snapshots**, not hand-maintained reference documents; they follow GitHub Actions artifact retention policy. Agents with a local executable should prefer **that executable's** `--cli-schema`. Agents without one can use a version/commit-matched artifact and must not mistake a different build's schema for proof of installed capabilities.

CI also checks that Native AOT executables can emit a valid schema. New command registrations and option changes should change the generated JSON automatically, with no additional manually synchronized schema file in the repository.

## Human and agent guides

- [VSlices Tooling agent operational protocol](https://github.com/vslices/protocols/blob/docs/tooling-agent-capability-protocol/vslices-tooling-usage/README.md): how to find and use a supported capability and how to interpret execution evidence.
- [CLI notes](cli.md): behavior and design decisions which cannot be inferred from method signatures.
- Product-specific documents: own semantic validity, not CLI argument grammar.

**Change discipline:** changing a command's name, options, arguments or XML documentation is a change to the machine-readable contract. Test the generated schema against the executable and `--help` in CI. Do not duplicate all option lists into the protocol.
