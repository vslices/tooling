---
id: vocabulary.vslices-tooling
type: domain-vocabulary
status: active
scope: project
related:
  - context.vslices-tooling
  - ai-development-orientation
---

# Vocabulario de VSlices Tooling

## VSIR

Representación intermedia semántica utilizada por VSlices para expresar conocimiento que restringe materializaciones concretas sin prescribir necesariamente una única forma textual.

## Materialización

Artefacto concreto que implementa o representa un VSIR para un target determinado.

En C#, la convención actual empareja:

```text
Name.vsir
Name.vsir.cs
```

La materialización `.vsir.cs` es editable por humanos. No se considera output descartable mientras continúe satisfaciendo el contrato semántico correspondiente.

## Conformance

Relación esperada entre un VSIR y una materialización válida.

De forma abreviada:

```text
Implementation |= VSIR
```

Una materialización puede diferir de la salida exacta del transpiler y seguir siendo válida si conserva el contrato semántico.

## Transpile

Lowering determinista desde VSIR hacia un target cuando las reglas y el contexto disponibles permiten construir una materialización sin inferencia interpretativa adicional.

El comando actual es:

```text
vslices transpile
```

Transpile construye un witness válido; no define una única representación privilegiada del VSIR.

## Semantic Rebase

Integración determinista de un cambio de VSIR sobre una materialización previamente editada por humanos, preservando cambios compatibles cuando existe un baseline determinista anterior.

El comando actual es:

```text
vslices rebase
```

Rebase no trata VSIR y materialización como fuentes de igual autoridad semántica y no debe inventar ancestry ausente.

## Lower

Superficie de orquestación que selecciona el mecanismo menos interpretativo suficiente para materializar un VSIR.

El comando actual es:

```text
vslices lower
```

Actualmente puede seleccionar transpile o rebase según la evidencia disponible y debe detenerse cuando no puede establecer legítimamente un baseline o una autoridad requerida.

## Interpretate

Lowering interpretativo para casos donde los mecanismos deterministas no pueden completar una decisión de materialización, pero existen obligaciones semánticas y evidencia contextual suficiente para resolverla sin inventar autoridad.

`interpretate` está definido conceptualmente, pero no forma parte todavía de la superficie ejecutable.

Durante `v0.2.0-preview`, un eventual `vslices interpretate` debe emerger de un caso concreto que permanezca genuinamente underdetermined pero constrained después de considerar VSIR, ruleset, evidencia del proyecto y tooling autoritativo del target.

La ausencia de una regla determinista no demuestra por sí sola que una operación requiera interpretación.

## Ruleset

Conjunto revisable de conocimiento de lowering externo al ejecutable.

El ruleset oficial vive en `vslices/ruleset`; un proyecto materializa un snapshot local bajo:

```text
.vslices/ruleset/
```

La ausencia de una regla requerida no autoriza fallback ni guessing.

## Configuración de proyecto

Política operativa del proyecto expresada en:

```text
.vslices/config.yaml
```

No contiene semántica de VSIR ni reglas concretas de lowering.

La precedencia es:

```text
argumento CLI explícito
  > config.yaml
  > default del ejecutable
```

La política de update, incluido `channel: build` y `pull-request`, pertenece a esta configuración persistente; los flags CLI correspondientes son overrides, no el happy path cotidiano.

## Artifact discovery

Resolución compartida de artefactos VSIR por path o símbolo.

El descubrimiento recursivo excluye siempre `.git/`, `.vslices/`, `bin/` y `obj/`, y puede incorporar exclusiones del proyecto desde:

```text
.vslices/.ignore
```

Un path explícito sigue siendo autoridad incluso cuando el artefacto estaría excluido del descubrimiento recursivo.

## Target context

Información de materialización delegada al tooling autoritativo del target cuando existe.

Para C#/.NET, VSlices puede delegar resolución de contexto a .NET/MSBuild y aceptar un namespace explícito como override autoritativo.

Target context puede resolver detalles de materialización, pero no debe redefinir semántica VSIR.

## Presentation boundary

Frontera estática de presentación de terminal que permite a los comandos expresar roles como `Heading`, `Info`, `Success`, `Warning`, `Error`, `Muted` y `Progress` sin acoplar su semántica a Kokuban, Kurukuru u otra librería concreta.

La presentación puede decorar output interactivo, pero no debe cambiar semántica ni contaminar stdout redirigido/machine-consumable.

## Build identity

Identidad humana de una build instalable de pull request:

```text
build<pr-number>.<run-number>
```

La representación SemVer usada internamente por .NET es detalle de materialización y no debe filtrarse como identidad normal del CLI.

## Self-update

Actualización del ejecutable standalone de VSlices Tooling mediante:

```text
vslices update --self
```

La política normal se lee desde `.vslices/config.yaml`. El artifact descargado debe verificarse antes de reemplazar el binario actual.

## Markdown generado

Salida producida por los mecanismos de generación documental de VSlices Tooling. No implica necesariamente que el documento resultante quede finalizado sin revisión humana.
