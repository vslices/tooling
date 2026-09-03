---
id: context.vslices-tooling
type: context-document
status: active
scope: project
level: L1
related:
 - continuity.iteration
 - ai-development-orientation
---

# Contexto de VSlices Tooling

## Escenario

VSlices Tooling es la superficie ejecutable de tooling de la suite VSlices.

Nació desde una fricción concreta de generación documental, pero su alcance actual incluye mecanismos para trabajar con VSIR: parsing y validación experimental, lowering determinista hacia C#, semantic rebase conservador, orquestación mediante `lower`, descubrimiento de artefactos, contexto de target, configuración de proyecto, reglas externas, presentación de terminal y distribución Native AOT.

El CLI se expone como `vslices`.

La línea actual de evolución es `v0.2.0-preview`.

## Por qué importa

VSlices necesita convertir conocimiento semántico explícito en acciones repetibles sin confundir el mecanismo de ejecución con el conocimiento revisable que utiliza.

El tooling permite automatizar tareas repetibles, producir evidencia de determinismo y conformance, y reducir divergencia entre intención, representación intermedia y materializaciones concretas.

Al mismo tiempo, debe evitar convertirse en una autoridad semántica accidental. El ejecutable no debería contener reglas de lowering concretas cuando esas reglas pueden evolucionar independientemente.

## Alcance implementado

La superficie ejecutable actual incluye:

```text
vslices init
vslices transpile
vslices rebase
vslices lower
vslices update --self
vslices --version
vslices -v
```

Además incluye:

* generación documental estructurada;
* parsing y validación de la superficie VSIR actualmente soportada;
* transpiling determinista para estructuras soportadas;
* semantic rebase conservador sobre materializaciones editadas por humanos;
* orquestación mediante el mecanismo menos interpretativo actualmente suficiente;
* resolución de contexto de targets;
* configuración operativa bajo `.vslices/config.yaml`;
* descubrimiento compartido de artefactos y `.vslices/.ignore`;
* descubrimiento e inicialización del ruleset oficial bajo `.vslices/ruleset`;
* integración progresiva con tooling autoritativo del target, como .NET/MSBuild;
* presentación de terminal centralizada para identidad, estados y progreso interactivo;
* distribución Native AOT, bootstrap PowerShell en Windows y self-update verificado;
* builds instalables de pull request para `win-x64`, `win-arm64` y `linux-x64`;
* pruebas de determinismo, build, Native AOT y comportamiento real del lowering.

Todavía no incluye como capacidad ejecutable consolidada:

* cobertura completa de VSIR;
* `interpretate` como comando;
* reconstrucción automática de provenance para rebase;
* `update --ruleset`;
* verificación semántica completa de una materialización arbitraria;
* package manager o sistema genérico de plugins remotos para rulesets;
* múltiples targets de producción;
* self-hosting completo del tooling;
* themes configurables por usuario.

## Dirección de `v0.2.0-preview`

La preview actual no está definida por una feature única obligatoria.

Avanza en dos pistas:

```text
CLI experience
  -> identidad, presentación, progreso y operabilidad

semantic capability
  -> representar más casos VSIR reales
     -> descubrir gaps reales
     -> ampliar reglas/mecanismos deterministas cuando corresponda
     -> identificar interpretación sólo si queda libertad genuinamente underdetermined pero constrained
```

`interpretate` permanece definido conceptualmente como lowering interpretativo cuando las rutas deterministas no bastan y existe autoridad contextual suficiente.

Su materialización como comando no es requisito ceremonial de la versión. Debe aparecer sólo cuando un caso concreto lo justifique.

## Frontera arquitectónica principal

La separación actualmente reconocida es:

```text
consumer project
  = evidencia concreta de dominio/software

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
  = mecanismos, orquestación, garantías, CLI y adaptadores de target

target-native tooling
  = hechos de target que ya poseen autoridad propia

.vsir.cs
  = materialización editable bajo contrato semántico
```

El CLI puede conocer primitivas de ejecución de reglas, pero las decisiones concretas de mapping pertenecen al ruleset cuando puedan expresarse externamente.

La ausencia de una regla no autoriza guessing ni fallback oculto.

Para el procedimiento cross-repo que decide dónde debe vivir un cambio, `docs/ai-development-orientation.md` es la orientación operativa actual.

## Modelo de lowering y conformance

VSIR no se interpreta como una plantilla que define una única representación textual.

La relación buscada es de conformidad:

```text
Implementation |= VSIR
```

Una materialización es válida cuando satisface el contrato semántico representado por VSIR.

El transpiler determinista construye un witness válido cuando las reglas disponibles permiten resolver el lowering sin interpretación adicional.

Semantic Rebase preserva cambios humanos compatibles cuando el VSIR evoluciona después de una materialización inicial y existe un baseline anterior explícito.

`lower` selecciona el mecanismo menos poderoso suficiente y debe detenerse cuando no puede establecer legítimamente la siguiente operación.

Actualmente:

```text
sin materialización existente
  -> transpile

materialización existente + baseline anterior disponible
  -> rebase

materialización existente + ancestry desconocida
  -> stop
```

Una `.vsir.cs` es código humano editable. Diferir textualmente del witness determinista no implica drift si la materialización continúa satisfaciendo VSIR.

## Hipótesis de lowering interpretativo

La regla reconocida es:

> Interpretation may resolve underdetermined materialization. Interpretation must not manufacture missing authority.

Por lo tanto:

```text
no deterministic rule
  != interpretate automáticamente

no authority
  -> stop
```

Antes de clasificar una decisión como interpretativa deben considerarse:

* semántica disponible en VSIR;
* conocimiento disponible en ruleset;
* evidencia concreta del proyecto;
* contexto autoritativo del target;
* capacidad de resolver la decisión mediante una nueva regla determinista o una primitive genérica justificable.

## Rulesets externos

El conocimiento de lowering vive fuera del ejecutable en `vslices/ruleset` y se materializa localmente bajo `.vslices/ruleset/` mediante `vslices init`.

Una vez inicializado, los comandos de lowering operan contra estado local sin depender de la red.

El manifest, su schema y las reglas concretas permanecen externos al ejecutable. Cambiar conocimiento de lowering no debería exigir republicar el CLI cuando los mecanismos existentes ya pueden ejecutarlo.

Regla de ownership:

```text
nuevo conocimiento de lowering ejecutable con mecanismos existentes
  -> normalmente vslices/ruleset

nueva capacidad operacional / primitive de ejecución / parser-model capability
  -> potencialmente vslices/tooling
```

## Configuración de proyecto

`.vslices/config.yaml` representa política operativa, no semántica.

La precedencia reconocida es:

```text
argumento CLI explícito
  > .vslices/config.yaml
  > default del ejecutable
```

Actualmente puede expresar target por defecto, provenance del ruleset y source/channel de actualización del CLI, incluido seguimiento de una build de pull request.

La política persistente debe preferirse sobre repetir flags en cada invocación. Por ejemplo, un proyecto en canal `build` configura su pull request una vez y usa normalmente:

```text
vslices update --self
```

No puede desactivar garantías como atomic writes, ausencia de fallback ante reglas faltantes o exclusiones de discovery incorporadas por seguridad.

## Presentación de terminal

La presentación interactiva se centraliza detrás de una frontera estática (`TerminalOutput`) que representa roles semánticos de presentación sin convertir Kokuban o Kurukuru en contrato de comando.

Actualmente se usa para identidad/version, `init` y `update --self`, incluyendo branding, detalles, resultados y progreso donde existe espera real.

La regla es:

> Presentation may decorate command output. Presentation must not change command semantics.

Output redirigido o machine-consumable debe permanecer limpio. Themes configurables son posible trabajo futuro, no parte necesaria del contrato actual.

## Distribución

Se busca mantener el CLI liviano y relativamente estable. Native AOT es la dirección actual de distribución para disponer de un ejecutable autocontenido mientras la mayor parte del conocimiento evolutivo permanezca externo.

Los RIDs actualmente producidos son:

```text
win-x64
win-arm64
linux-x64
```

Windows dispone de bootstrap PowerShell que instala el binario standalone bajo `%USERPROFILE%\.vslices\bin`, verifica checksum y puede agregarlo al PATH de usuario.

`vslices update --self` mantiene ese ejecutable standalone. Además de canales de release, un proyecto puede seguir la última build exitosa de un PR mediante `updates.channel: build` y `updates.pull-request` en configuración.

Las builds de PR usan identidad humana:

```text
build<pr-number>.<run-number>
```

La representación SemVer interna usada por .NET no constituye la identidad de producto visible.

## Validación actual

`StreetName.vsir` fue el benchmark inicial, no el límite del modelo.

La estrategia de `v0.2.0-preview` es introducir progresivamente más VSIR reales para descubrir necesidades de parser, modelo, reglas, target context, rebase e interpretación antes de generalizar.

Las propiedades que se busca demostrar incluyen:

* mismo VSIR + mismo ruleset + mismo target context => mismo resultado determinista;
* una regla ausente produce diagnóstico en lugar de fallback embebido;
* cambiar una regla externa puede cambiar el lowering sin recompilar el CLI;
* un namespace explícito es autoridad suficiente y no requiere un `.csproj`;
* `lower` y `rebase` funcionan a través de su superficie CLI real;
* discovery respeta exclusiones built-in y `.vslices/.ignore`;
* el tooling puede trabajar offline después de inicializar el ruleset;
* Native AOT ejecuta la superficie documentada;
* artifacts de instalación/actualización se validan antes de reemplazar ejecutables;
* output interactivo puede enriquecerse sin romper output redirigido;
* build/test del target y conformance semántica son preocupaciones relacionadas pero distintas.

## Objetivo de dogfooding y self-hosting semántico

Como objetivo de largo plazo, VSlices Tooling debería usar VSIR para describir sus propias partes cuando esas partes pertenezcan a categorías que VSIR afirma poder representar.

Esto no exige generar todo el programa ni convertir cada línea en output del transpiler.

La intención es más estricta y útil: si VSlices puede representar un Domain Type, Feature, Invariant u otro concepto, sus instancias dentro del propio tooling deberían ser candidatas a expresarse mediante `.vsir` y mantenerse usando los mismos mecanismos ofrecidos a otros proyectos.

De esa forma, Tooling puede actuar como dogfooding target, corpus de conformance y fuente continua de evidencia sobre límites reales de VSIR.

## Continuidad documental

Las conversaciones ayudan a explorar decisiones, pero no deben ser necesarias para reconstruir el estado aceptado.

Cuando cambie materialmente una frontera de responsabilidad, una semántica de comando, una regla de autoridad o la dirección de la preview, el cambio debe quedar visible en el artefacto documental más cercano.

Esto es especialmente importante para trabajo AI-assisted cross-repo: un chat futuro debe poder analizar un consumidor, `vslices/tooling` y `vslices/ruleset` desde evidencia actual sin recrear la arquitectura desde memoria conversacional.

## Límites

VSlices Tooling entrega mecanismos. No debe absorber por comodidad conocimiento que corresponde a VSIR, al ruleset, al proyecto consumidor o al tooling autoritativo del target.

Las nuevas abstracciones deberían emerger de casos concretos y no de anticipar todos los futuros targets o formas de lowering.
