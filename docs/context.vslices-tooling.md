---
id: context.vslices-tooling
type: context-document
status: active
scope: project
level: L1
---

# Contexto de VSlices Tooling

## Escenario

VSlices Tooling es la superficie ejecutable de la suite VSlices. La línea actual es `v0.2.0-preview` y el CLI se expone como `vslices`.

Su responsabilidad es convertir semántica y conocimiento de lowering explícitos en operaciones repetibles sin transformarse en autoridad semántica accidental.

## Frontera reconocida

```text
consumer project = evidencia
.vsir = semántica
.vsir.cs = witness humano editable
.vslices/config.yaml = política operacional
.vslices/ruleset = snapshot local de conocimiento de lowering
.vslices/lineage = evidencia operacional de ancestry
vslices/ruleset = conocimiento oficial revisable
vslices/tooling = mecanismos, coordinación, garantías y adapters
target-native tooling = autoridad del target
```

La ausencia de una regla no autoriza guessing ni fallback oculto.

## Arquitectura interna consolidada

Los command handlers son adapters delgados.

```text
Commands
  -> TranspilationOperation
  -> RebaseOperation
  -> LoweringCoordinator

LoweringCoordinator
  -> VSlicesProjectContext
  -> LoweringLineageStore / Bootstrap
  -> operations de transpile/rebase

RulesetCommands / UpdateCommands
  -> RulesetSourceMaterializer
  -> RulesetSnapshotInstaller

Project
  -> VSlicesProjectContext
  -> ProjectConfiguration
  -> ArtifactDiscoveryPolicy
```

No existe un service container ni DI introducido por esta consolidación. Las fronteras representan responsabilidades observadas, no una arquitectura ceremonial.

## `lower`

`lower` sigue siendo una superficie de orquestación, no una colección de comandos internos públicos.

Su política actual es:

```text
no witness -> transpile + baseline
trusted baseline -> rebase
exact deterministic witness without lineage -> establish lineage
authorized conventional bootstrap -> store deterministic baseline, preserve human witness, no immediate rebase
otherwise -> stop / explicit ancestry required
```

## Proyecto VSlices

`VSlicesProjectContext` resuelve una sola vez el proyecto más cercano desde una ruta y expone ProjectRoot, VslicesRoot, ConfigurationPath, RulesetRoot, LineageRoot y Configuration. Los mecanismos internos no deben volver a inferir esos roots mediante cadenas de `Parent`.

## Ruleset

Inicialización y actualización comparten adquisición y preparación. Un update nunca reemplaza el snapshot activo antes de que el snapshot preparado haya pasado la validación real del target.

Para C# esa validación utiliza `CSharpLoweringRuleSet.Load`.

## Semántica VSIR consolidada

Unknown semantics fallan cerradas en mappings semánticos conocidos. `traits` es un conjunto de capacidades: orden irrelevante, duplicados inválidos.

No se agregó ningún nuevo concepto VSIR durante esta consolidación.

## Continuidad de lineage

La baseline determinista guardada bajo `.vslices/lineage` es evidencia operacional suficiente demostrada hasta ahora para el three-way rebase. Se decide versionarla por defecto para que otros ambientes puedan continuar el mismo lineage desde el repositorio.

Esto no la vuelve autoridad semántica y no implica reconstrucción automática desde Git history.

## Validación

Existen pruebas semánticas y de lowering en `VSlices.Vsir.CSharp.Tests`, y pruebas de orquestación real en `VSlices.Tooling.Tests` mediante el proceso CLI construido. CI agrega smokes de `update --ruleset`, bootstrap no destructivo, rebase posterior, target context y Native AOT.

Durante el refactor se descubrió que el assembly ejecutable `vslices` colisiona case-insensitively con el assembly Framework `VSlices` al intentar cargarlos juntos desde un test host. No se fragmentó Tooling para ocultar el hallazgo; mientras Tooling sea CLI, la cobertura de orquestación usa la frontera ejecutable real. El naming queda como pregunta futura si aparece un consumidor in-process.

## Límites

No pertenecen a esta consolidación: namespace path policy, nuevos conceptos VSIR, normalización, nuevos targets, interpretate, provenance graph, Git-history ancestry reconstruction, aggregate update ni themes configurables.
