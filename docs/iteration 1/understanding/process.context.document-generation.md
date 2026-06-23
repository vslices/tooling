---
id: process.context.document-generation
type: context-document
status: draft
scope: process
level: L0
related:
  - process.context.document-generation
  - continuity.iteration
  - context.vslices-tooling
  - vocabulary.vslices-tooling
---

<!--
Status: draft, active, resolved, superseded, or archived.
Scope: stage or iteration; service, product or project; bounded-context or organization; flow-step, flow, process or work-line.
Level: L0, L1.
-->

# Contexto de la generación documental de VSlices a día 23-06-2026

## Escenario

Estamos observando la generación documental dentro de VSlices.

En este momento, la generación documental no existe como tooling automatizado. Ocurre principalmente como trabajo manual al crear, ajustar y mantener documentos Markdown para VSlices Docs Standard, VSlices Method y aprendizajes provenientes de Alive Lab.

## Por qué importa

Importa porque VSlices está empezando a consolidar tipos documentales, niveles de detalle, convenciones de nombres, scopes, preguntas guía y relaciones entre documentos.

Si este trabajo sigue creciendo solo de forma manual, aumenta el riesgo de duplicación, divergencia editorial y pérdida de continuidad entre la intención documental y los documentos reales.

## Situación actual

Actualmente los documentos se crean y ajustan manualmente.

Las estructuras nacen desde conversaciones, observaciones, iteraciones reales y necesidades detectadas mientras se trabaja en VSlices Docs Standard.

Ya existen conceptos que se repiten y empiezan a estabilizarse:

* documentos de contexto
* documentos de continuidad
* documentos de estructura
* documentos de caso de uso
* niveles L0 y L1
* scopes documentales
* estrategias de naming
* front matter
* preguntas guía

## Puntos de dolor u oportunidades

El dolor principal es mantener consistencia documental sin introducir demasiada ceremonia.

Puntos de dolor:

* repetición manual de estructuras similares
* riesgo de cambios inconsistentes entre documentos
* confusión entre tipo documental, scope y nivel de detalle
* dificultad para decidir cuándo un documento debe nacer como L0 o L1
* posible mezcla entre Support Note y documentos L0
* riesgo de saltar a tooling antes de entender bien el trabajo

Oportunidades:

* estabilizar una estructura documental mínima
* identificar qué partes podrían generarse desde una fuente estructurada
* preservar convenciones sin reemplazar criterio humano
* validar un primer slice pequeño antes de crear tooling general

## Límites

Este contexto empieza en el trabajo actual de generar y mantener documentos de VSlices de forma manual.

Termina antes de definir cómo se implementará el tooling.

Incluye:

* el estado actual de la generación documental
* fricciones del proceso manual
* conceptos documentales emergentes
* oportunidades para tooling futuro

No incluye todavía:

* decisión sobre YAML u otro formato
* diseño del CLI
* generación automática completa
* validación profunda de documentos
* integración con MkDocs
* generación multilenguaje

## Preguntas abiertas

* ¿Qué parte de la generación documental debe seguir siendo manual?
* ¿Qué parte se puede apoyar con tooling sin perder intención documental?
* ¿Cuál es el primer documento que conviene generar?
* ¿Cómo representar tipo documental, scope y nivel de detalle?
* ¿Qué reglas editoriales deben estabilizarse antes de automatizar?
* ¿Cuándo corresponde crear un documento L0, L1 o una Support Note?
