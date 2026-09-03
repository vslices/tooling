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

Nació desde una fricción concreta de generación documental, pero su alcance actual ya incluye mecanismos para trabajar con VSIR: parsing y validación experimental, lowering determinista hacia C#, semantic rebase conservador, orquestación mediante `lower`, descubrimiento de artefactos, contexto de target, configuración de proyecto, reglas externas y distribución Native AOT.

El CLI actual se expone como `vslices`.

## Por qué importa

VSlices necesita convertir conocimiento semántico explícito en acciones repetibles sin confundir el mecanismo de ejecución con el conocimiento revisable que utiliza.

El tooling importa porque permite automatizar tareas repetibles, producir evidencia de determinismo y conformance, y reducir divergencia entre intención, representación intermedia y materializaciones concretas.

Al mismo tiempo, debe evitar convertirse en una autoridad semántica accidental. El ejecutable no debería contener reglas de lowering concretas cuando esas reglas pueden evolucionar independientemente.

## Alcance actual

La superficie candidata para `v0.1.0-preview` incluye:

* generación documental estructurada
* CLI oficial `vslices`
* `vslices init`
* `vslices transpile`
* `vslices rebase`
* `vslices lower`
* `vslices update --self`
* parsing y validación de la superficie VSIR actualmente soportada
* transpiling determinista para estructuras soportadas
* semantic rebase conservador sobre materializaciones editadas por humanos
* orquestación mediante el mecanismo menos interpretativo actualmente suficiente
* resolución de contexto de targets
* configuración operativa bajo `.vslices/config.yaml`
* descubrimiento compartido de artefactos y `.vslices/.ignore`
* descubrimiento e inicialización de `.vslices/ruleset`
* integración progresiva con tooling autoritativo del target, como .NET/MSBuild
* distribución Native AOT, bootstrap PowerShell en Windows y self-update verificado
* pruebas de determinismo, build y comportamiento del lowering

Todavía no incluye como capacidad de `v0.1.0-preview`:

* cobertura completa de VSIR
* `interpretate` como comando ejecutable
* reconstrucción automática de provenance para rebase
* `update --ruleset`
* verificación semántica completa de una materialización arbitraria
* package manager o sistema de plugins remotos para rulesets
* múltiples targets de producción
* self-hosting completo del tooling

`interpretate` permanece definido conceptualmente como lowering interpretativo cuando las rutas deterministas no bastan y existe autoridad contextual suficiente. Su primera materialización como comando es candidata para `v0.2.0-preview` y no constituye una ausencia accidental de la primera preview.

## Frontera arquitectónica principal

La separación actualmente reconocida es:

```text
.vsir
  = fuente semántica

vslices/ruleset
  = conocimiento oficial y revisable de lowering

project/.vslices/ruleset
  = snapshot local, editable y versionable

project/.vslices/config.yaml
  = política operativa del proyecto

project/.vslices/.ignore
  = exclusiones específicas de discovery

vslices executable
  = mecanismos, orquestación, garantías y adaptadores de target

.vsir.cs
  = materialización editable
```

El CLI puede conocer primitivas de ejecución de reglas, pero las decisiones concretas de mapping pertenecen al ruleset cuando puedan expresarse externamente.

La ausencia de una regla no autoriza guessing ni fallback oculto.

## Modelo de lowering

VSIR no se interpreta como una plantilla que define una única representación textual.

La relación buscada es de conformidad: una materialización es válida cuando satisface el contrato semántico representado por VSIR.

El transpiler determinista construye un witness válido cuando las reglas disponibles permiten resolver el lowering sin interpretación adicional.

Semantic Rebase preserva cambios humanos compatibles cuando el VSIR evoluciona después de una materialización inicial y existe un baseline anterior explícito.

`lower` es la superficie de orquestación: debe seleccionar el mecanismo menos poderoso suficiente y detenerse cuando no puede establecer legítimamente la siguiente operación.

En la primera preview esto significa, de forma conservadora:

```text
sin materialización existente
  -> transpile

materialización existente + baseline anterior disponible
  -> rebase

materialización existente + ancestry desconocida
  -> stop
```

## Rulesets externos

El conocimiento de lowering vive fuera del ejecutable en `vslices/ruleset` y se materializa localmente bajo `.vslices/ruleset/` mediante `vslices init`.

Una vez inicializado, los comandos de lowering operan contra estado local sin depender de la red.

El manifest, su schema y las reglas concretas permanecen externos al ejecutable. Cambiar conocimiento de lowering no debería exigir republicar el CLI cuando los mecanismos existentes ya pueden ejecutarlo.

## Configuración de proyecto

`.vslices/config.yaml` representa política operativa, no semántica.

La precedencia reconocida es:

```text
argumento CLI explícito
  > .vslices/config.yaml
  > default del ejecutable
```

Actualmente puede expresar, entre otras cosas, target por defecto, provenance del ruleset y source/channel de actualización del CLI.

No puede desactivar garantías como atomic writes, ausencia de fallback ante reglas faltantes o exclusiones de discovery incorporadas por seguridad.

## Distribución

Se busca mantener el CLI liviano y relativamente estable. Native AOT es la dirección actual de distribución para disponer de un ejecutable autocontenido mientras la mayor parte del conocimiento evolutivo permanezca externo.

La primera preview contempla artifacts `win-x64` y `linux-x64` con checksum SHA-256. Windows dispone de un bootstrap PowerShell que instala el binario standalone bajo `%USERPROFILE%\.vslices\bin` y lo agrega al PATH de usuario.

`vslices update --self` mantiene ese ejecutable standalone a partir de GitHub Releases y verifica el archive antes del reemplazo.

El binario debería cambiar principalmente al aparecer nuevas capacidades operacionales o primitivas de ejecución que no puedan expresarse con los mecanismos actuales.

## Validación actual

El benchmark inicial es `StreetName.vsir`.

Las propiedades que se busca demostrar incluyen:

* mismo VSIR + mismo ruleset + mismo target context => mismo resultado determinista
* una regla ausente produce diagnóstico en lugar de fallback embebido
* cambiar una regla externa puede cambiar el lowering sin recompilar el CLI
* un namespace explícito es autoridad suficiente y no requiere un `.csproj`
* `lower` y `rebase` funcionan a través de su superficie CLI real
* discovery respeta exclusiones built-in y `.vslices/.ignore`
* el tooling puede trabajar offline después de inicializar el ruleset
* el Native AOT real ejecuta la superficie documentada
* artifacts de instalación/actualización se validan antes de reemplazar ejecutables
* build/test del target y conformance semántica son preocupaciones relacionadas pero distintas

Los VSIR siguientes deben introducirse progresivamente para descubrir necesidades reales de parser, modelo, reglas y ejecución antes de generalizar.

Los criterios concretos de release para la primera preview se registran en `docs/releases/v0.1.0-preview.md`.

## Objetivo de dogfooding y self-hosting semántico

Como objetivo de largo plazo, VSlices Tooling debería usar VSIR para describir sus propias partes cuando esas partes pertenezcan a categorías que VSIR afirma poder representar.

Esto no exige generar todo el programa ni convertir cada línea en output del transpiler.

La intención es más estricta y útil: si VSlices puede representar un Domain Type, Feature, Invariant u otro concepto, sus instancias dentro del propio tooling deberían ser candidatas a expresarse mediante `.vsir` y mantenerse usando los mismos mecanismos ofrecidos a otros proyectos.

De esa forma, el tooling puede actuar como dogfooding target, corpus de conformance y fuente continua de evidencia sobre límites reales de VSIR.

## Límites

VSlices Tooling entrega mecanismos. No debe absorber por comodidad conocimiento que corresponde a VSIR, al ruleset, al proyecto consumidor o al tooling autoritativo del target.

Las nuevas abstracciones deberían emerger de casos concretos y no de anticipar todos los futuros targets o formas de lowering.
