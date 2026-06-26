---
document:
  type: context
  scope: process
  target: document-generation
  level: L0
  language: es

metadata:
  status: active
  relates: 
    - type: complements
      target: process.structure.document-generation
    - type: referenced-by
      target: continuity.iteration
    - type: owned-by
      target: context.vslices-tooling
    - type: explained-by 
      target: vocabulary.vslices-tooling

tooling:
  kind: document
  template:
    name: context.document
    version: 0.1.0
---

<!--
Status: draft, active, resolved, superseded, or archived.
Scope: stage or iteration; service, product or project; bounded-context or organization; flow-step, flow, process or work-line.
Level: L0, L1.
-->

# Contexto del proceso de Generación Documental

## Escenario

Estamos observando un proceso que existe dentro de VSlices Docs Standard.


## Situación actual

Actualmente los documentos se crean y ajustan de forma manual.

Las estructuras nacen desde conversaciones, observaciones, iteraciones reales y necesidades detectadas mientras se trabaja en VSlices Docs Standard.


## Puntos de dolor

* repetición manual de estructuras similares
* riesgo de cambios inconsistentes entre documentos
* confusión entre tipo documental, scope y nivel de detalle
* dificultad para decidir cuándo un documento debe nacer como L0 o L1
* posible mezcla entre Support Note y documentos L0

## Frontera contextual

Este proceso se observa dentro del trabajo de generar y mantener documentos de VSlices.

Cuando la conversación pasa a definir qué entra o no entra en un documento/template, esa responsabilidad pertenece a un documento distinto.
