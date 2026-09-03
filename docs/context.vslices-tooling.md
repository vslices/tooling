---
id: context.vslices-tooling
type: context-document
status: active
scope: project
level: L1
related:
 - continuity.iteration
---

# Contexto de VSlices Tooling

## Escenario

VSlices Tooling es la superficie ejecutable de tooling de la suite VSlices.

Nació desde una fricción concreta de generación documental, pero su alcance actual ya incluye mecanismos para trabajar con VSIR: parsing y validación experimental, lowering determinista hacia C#, rebase semántico conservador, descubrimiento de contexto de target y carga de reglas externas.

El CLI actual se expone como `vslices`.

## Por qué importa

VSlices necesita convertir conocimiento semántico explícito en acciones repetibles sin confundir el mecanismo de ejecución con el conocimiento revisable que utiliza.

El tooling importa porque permite automatizar tareas repetibles, producir evidencia de determinismo y conformance, y reducir divergencia entre intención, representación intermedia y materializaciones concretas.

Al mismo tiempo, debe evitar convertirse en una autoridad semántica accidental. El ejecutable no debería contener reglas de lowering concretas cuando esas reglas pueden evolucionar independientemente.

## Alcance actual

Incluye:

* generación documental estructurada
* CLI oficial `vslices`
* parsing y validación de la superficie VSIR actualmente soportada
* transpiling determinista para estructuras soportadas
* experimentos de semantic rebase sobre materializaciones editadas por humanos
* resolución de contexto de targets
* descubrimiento e inicialización de `.vslices/ruleset`
* integración progresiva con tooling autoritativo del target, como .NET/MSBuild
* pruebas de determinismo, build y comportamiento del lowering

Todavía no incluye como capacidad estable:

* cobertura completa de VSIR
* `interpretate` como comando productivo
* orquestación estable de `lower`
* verificación semántica completa de una materialización arbitraria
* package manager o sistema de plugins remotos para rulesets
* self-hosting completo del tooling

## Frontera arquitectónica principal

La separación actualmente reconocida es:

```text
.vsir
  = fuente semántica

vslices/ruleset
  = conocimiento oficial y revisable de lowering

project/.vslices/ruleset
  = snapshot local, editable y versionable

vslices executable
  = mecanismos de ejecución y orquestación

.vsir.cs
  = materialización editable
```

El CLI puede conocer primitivas de ejecución de reglas, pero las decisiones concretas de mapping pertenecen al ruleset cuando puedan expresarse externamente.

La ausencia de una regla no autoriza guessing ni fallback oculto.

## Modelo de lowering

VSIR no se interpreta como una plantilla que define una única representación textual.

La relación buscada es de conformidad: una materialización es válida cuando satisface el contrato semántico representado por VSIR.

El transpiler determinista construye un witness válido cuando las reglas disponibles permiten resolver el lowering sin interpretación adicional.

Semantic Rebase se explora para preservar cambios humanos compatibles cuando el VSIR evoluciona después de una materialización inicial.

## Rulesets externos

El conocimiento de lowering se está moviendo a `vslices/ruleset`.

`vslices init` materializa ese conocimiento bajo `.vslices/ruleset/`. Una vez inicializado, los comandos de lowering deberían poder operar contra estado local sin depender de la red.

El manifest, su schema y las reglas concretas permanecen externos al ejecutable.

## Distribución

Se busca mantener el CLI liviano y relativamente estable. Native AOT es la dirección preferida de distribución para disponer de un ejecutable autocontenido mientras la mayor parte del conocimiento evolutivo permanezca en configuración externa.

El binario debería cambiar principalmente al aparecer nuevas capacidades operacionales o primitivas de ejecución que no puedan expresarse con los mecanismos actuales.

## Validación actual

El benchmark inicial es `StreetName.vsir`.

Las propiedades que se busca demostrar incluyen:

* mismo VSIR + mismo ruleset + mismo target context => mismo resultado determinista
* una regla ausente produce diagnóstico en lugar de fallback embebido
* cambiar una regla externa puede cambiar el lowering sin recompilar el CLI
* el tooling puede trabajar offline después de inicializar el ruleset
* build/test del target y conformance semántica son preocupaciones relacionadas pero distintas

Los VSIR siguientes deben introducirse progresivamente para descubrir necesidades reales de parser, modelo, reglas y ejecución antes de generalizar.

## Objetivo de dogfooding y self-hosting semántico

Como objetivo de largo plazo, VSlices Tooling debería usar VSIR para describir sus propias partes cuando esas partes pertenezcan a categorías que VSIR afirma poder representar.

Esto no exige generar todo el programa ni convertir cada línea en output del transpiler.

La intención es más estricta y útil: si VSlices puede representar un Domain Type, Feature, Invariant u otro concepto, sus instancias dentro del propio tooling deberían ser candidatas a expresarse mediante `.vsir` y mantenerse usando los mismos mecanismos ofrecidos a otros proyectos.

De esa forma, el tooling puede actuar como dogfooding target, corpus de conformance y fuente continua de evidencia sobre límites reales de VSIR.

## Límites

VSlices Tooling entrega mecanismos. No debe absorber por comodidad conocimiento que corresponde a VSIR, al ruleset, al proyecto consumidor o al tooling autoritativo del target.

Las nuevas abstracciones deberían emerger de casos concretos y no de anticipar todos los futuros targets o formas de lowering.
