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

La ausencia de una regla no autoriza guessing ni fallback oculto. Un Ruleset tampoco puede inventar una semántica que VSIR no reconoce.

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
  -> SemanticRefactoringCoordinator cuando aparece una consecuencia target-semantic conocida

SemanticRefactoringCoordinator
  -> preflight de namespace move
  -> autorización de análisis
  -> DotNetSemanticRefactoringClient
  -> autorización de mutación
  -> TransactionalFileWriter

RulesetCommands / UpdateCommands
  -> RulesetSourceMaterializer
  -> RulesetSnapshotInstaller

Project
  -> VSlicesProjectContext
  -> ProjectConfiguration
  -> ArtifactDiscoveryPolicy

VSlices.Targets.DotNet
  -> DotNetTargetContextResolver
  -> NamespacePathPolicy

VSlices.Targets.DotNet.Refactor
  -> host administrado separado del Native AOT
  -> RefactorArguments
  -> NamespaceMovePlanner
  -> CompilationValidator
  -> RefactorManifest
```

No existe un service container ni DI introducido por esta consolidación. Las fronteras representan responsabilidades observadas, no arquitectura ceremonial.

## Semántica VSIR

Unknown semantics fallan cerradas en mappings semánticos conocidos. `traits` es un conjunto de capacidades: orden irrelevante, duplicados inválidos.

El experimento TicketCode agregó la primera normalización observada:

```yaml
- normalize:
    target: input.Value
    intrinsic: trim
```

La autoridad queda explícitamente separada:

```text
VSlices.Vsir
  reconoce que trim es una semántica de normalización válida

lowerer / Tooling
  conserva el orden y el dataflow de la normalización

Ruleset
  define cómo materializar intrinsic.trim en un target concreto
```

Un valor de intrinsic desconocido falla en VSIR antes de consultar Ruleset.

## `lower`, `rebase` y autoridad humana

`rebase` es la primitive textual de three-way materialization merge. No promete cierre semántico target-wide.

`lower` es el workflow seguro del proyecto:

```text
no witness -> transpile + baseline
trusted baseline -> rebase
exact deterministic witness without lineage -> establish lineage
authorized conventional bootstrap -> store deterministic baseline, preserve human witness
known target-semantic namespace move -> optional Roslyn semantic workflow
otherwise -> stop / explicit ancestry required
```

Cuando un rebase produce un namespace move conocido, `lower` puede:

```text
preguntar antes de cargar Roslyn
-> descubrir blast radius semántico
-> validar baseline y propuesta
-> presentar archivos/referencias afectados
-> pedir autorización separada antes de modificar código humano
-> aplicar fuentes + lineage en una transacción
```

`--resolve deterministic` sólo autoriza la región textual de conflicto conocida; no concede autoridad implícita sobre referencias semánticas en otros archivos.

## Target context .NET

El namespace default usa `RootNamespace` evaluado por MSBuild y la ruta relativa desde el `.csproj` al `.vsir`.

`NamespacePathPolicy` aplica exclusiones configurables sin hardcodear convenciones del consumer:

```yaml
targets:
  csharp:
    namespace:
      ignore-folders:
        - "Aggregates/*"
        - "Aggregates/**/Entities"
```

`*` representa un segmento, `?` un carácter dentro de un segmento y `**` cero o más segmentos completos. El patrón establece contexto; sólo el último folder matcheado se excluye del namespace.

Un `--namespace` explícito sigue siendo autoridad superior y bypassa la derivación.

## Roslyn como companion administrado

Roslyn/MSBuildWorkspace no se enlaza al ejecutable Native AOT. La distribución contiene:

```text
vslices / vslices.exe
refactor/VSlices.Targets.DotNet.Refactor.dll
refactor/BuildHost-netcore/...
```

El helper recibe el nombre semántico del artifact desde `name:` en VSIR; no lo deriva del nombre físico del archivo.

La validación de compilación es fail-closed: `GetCompilationAsync == null` significa “no se pudo verificar”, no “verificado sin errores”.

La completitud del companion requiere tanto el helper DLL como `BuildHost-netcore`. Startup, updater, staging/archive validation e installer comparten esa misma definición.

## Continuidad de lineage

La baseline determinista guardada bajo `.vslices/lineage` es evidencia operacional suficiente demostrada hasta ahora para el three-way rebase. Se decide versionarla por defecto para que otros ambientes puedan continuar el mismo lineage desde el repositorio.

Esto no la vuelve autoridad semántica y no implica reconstrucción automática desde Git history.

## Ruleset

Inicialización y actualización comparten adquisición y preparación. Un update nunca reemplaza el snapshot activo antes de que el snapshot preparado haya pasado la validación real del target.

Para C# esa validación utiliza `CSharpLoweringRuleSet.Load`.

## Validación

Existen pruebas semánticas y de lowering en `VSlices.Vsir.CSharp.Tests`, pruebas de Tooling en `VSlices.Tooling.Tests`, smoke Roslyn/MSBuildWorkspace y publicación Native AOT para Linux y Windows.

## Watchpoints explícitos

No bloquean este baseline, pero deben preservarse como preguntas concretas:

- cancelar un análisis Roslyn largo debería matar el child process tree y limpiar staging;
- la normalización actual puede repetir la expresión renderizada, por lo que futuros intrinsics deberán ser referencialmente transparentes o bajar a evaluación única;
- optimizar el workspace Roslyn no puede reducir silenciosamente el universo del blast radius;
- no existe todavía un motor genérico de reparación semántica, policy no interactiva de refactorings, provenance graph ni reconstrucción de lineage desde Git.

## Límites actuales

Siguen fuera de alcance: nuevos targets, nuevos intrinsics de normalización más allá de `trim`, interpretate, provenance graph, Git-history ancestry reconstruction, aggregate update, refactorings semánticos genéricos, patrones namespace con negación/prioridad/regex y themes configurables.
