---
id: context.vslices-tooling
type: context-document
status: active
scope: project
level: L1
related:
 - continuity.iteration
---

<!--
Status: draft, active, resolved, superseded, or archived.
Scope: stage, iteration, service, product, bounded-context, project, organization.
Level: L0, L1.
-->

# Contexto de VSlices Tooling

## Escenario

VSlices Tooling es un proyecto de software que apoya las tareas repetibles dentro de la suite VSlices.

En esta iteración, el foco inicial está facilitar la generacion documental para VSlices Docs Standard, teniendolo desde una base de templates minimos, que den cabida al multilenguaje y sean escalables a la evolución del producto.

## Por qué importa

VSlices Docs Standard está empezando a definir tipos documentales, niveles de detalle, convenciones editoriales y relaciones entre documentos.

Mantener todo esto manualmente puede generar duplicación, divergencia y pérdida de continuidad entre la intención documental y los documentos reales.

El tooling importa porque puede ayudar a preservar consistencia sin reemplazar el criterio de diseño.

## Alcance

Este contexto abarca el inicio de VSlices Tooling como proyecto de software.

En esta iteración, el alcance se limita a entender cómo apoyar la generación documental de forma progresiva.

Incluye:

* tooling documental
* documentos Markdown
* tipos documentales
* niveles de detalle
* convenciones editoriales
* relación con VSlices Docs Standard
* relación futura con VSlices Framework

No incluye todavía:

* generación completa de todos los documentos
* soporte multilenguaje completo
* validación profunda de relaciones
* integración completa con MkDocs
* generación de código de aplicación

## Líneas de trabajo

Las líneas de trabajo iniciales son:

* generación de documentos [en proceso]
* tooling oficial para VSlices [excluido]

## Áreas y actores

Las áreas involucradas son:

* VSlices Docs Standard

## Situación actual

Las plantillas y documentos de VSlices Docs Standard se mantienen principalmente como Markdown escrito manualmente.

Ya existen patrones repetidos entre documentos, como tipos documentales, niveles L0 y L1, secciones comunes, preguntas guía y front matter.

El tooling todavía no existe como proyecto estable. Esta iteración busca iniciar su primer slice desde una fricción real observada.

## Puntos de dolor u oportunidades

El dolor principal es que mantener plantillas documentales consistentes de forma manual puede generar duplicación, errores editoriales y pérdida de continuidad.

Oportunidades iniciales:

* reducir divergencia entre plantillas similares
* preservar estructura documental común
* hacer explícitos idioma, tipo documental y nivel de detalle
* preparar una base pequeña para generación futura
* validar tooling desde un caso real antes de formalizarlo como producto

## Límites

Este contexto empieza en la necesidad de apoyar documentación estructurada dentro de VSlices.

Termina antes de definir una arquitectura completa de CLI, validación, generación multilenguaje o integración con código.

Los límites todavía no están completamente claros entre:

* VSlices Tooling como proyecto propio
* VSlices Framework como lugar futuro para CLI o tooling
* VSlices Docs Standard como fuente de estructuras documentales
* VSlices Method como guía para decidir cuándo documentar

## Decisiones iniciales

| Pregunta | Decisión |
| --- | --- |
| ¿VSlices Tooling vivirá como proyecto propio o como parte de VSlices Framework? | VSlices Tooling vivirá como proyecto propio, pero evolucionará de la mano de VSlices Framework y VSlices Docs Standard. |
| ¿El primer slice debe generar documentos desde YAML u otra fuente estructurada? | Por ahora, solo YAML. Otras fuentes estructuradas pueden considerarse en iteraciones futuras. |
| ¿Qué parte pertenece a Docs Standard y qué parte pertenece al tooling? | VSlices Docs Standard define documentos, diagramas y caminos de continuidad. VSlices Tooling entrega herramientas para generarlos y conectarlos con otros proyectos. |
| ¿Cuánto debe validar el tooling en esta primera iteración? | Debe validar que puede generar un documento L0 en español. |
| ¿Qué documento debe ser el primer caso generado? | El Documento de contexto. |
| ¿Cómo se representarán idioma, tipo documental y nivel de detalle sin sobrediseñar? | Cada tipo documental tendrá su propio template YAML. |
