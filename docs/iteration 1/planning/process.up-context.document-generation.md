---
id: process.context.document-generation
type: context-document
status: draft
scope: process
level: L0
related:
  - context.vslices-tooling
  - continuity.iteration
  - process.structure.document-generation
---

<!--
Status: draft, active, resolved, superseded, or archived.
Scope: stage, iteration, service, product, bounded-context, project, organization.
Level: L0, L1.
-->

# Contexto del Proceso de Generación de documentos

## Escenario

Estamos observando el proceso de generación de documentos dentro de VSlices Docs Standard.

Este proceso nace desde la necesidad de crear y mantener documentos Markdown consistentes a partir de tipos documentales, niveles de detalle, scopes, convenciones editoriales y preguntas guía definidas por VSlices.

En esta iteración, la generación documental se entiende como parte de una línea técnica de VSlices Docs Standard, no todavía como un producto independiente.

## Por qué importa

Importa porque VSlices Docs Standard está empezando a estabilizar documentos, plantillas, niveles y convenciones que pueden repetirse en varios contextos.

Si cada documento se mantiene únicamente de forma manual, aumenta el riesgo de duplicación, divergencia editorial y pérdida de continuidad entre la intención documental y los documentos reales.

Este proceso importa porque puede permitir que una fuente estructurada mínima produzca documentos Markdown consistentes, sin reemplazar el criterio humano necesario para diseñar y revisar documentación.

## Situación actual

Actualmente los documentos se crean y ajustan principalmente de forma manual.

La estructura de cada documento se escribe directamente en Markdown, incluyendo front matter, secciones, comentarios guía y convenciones editoriales.

Durante la iteración se observó que este proceso puede apoyarse mediante templates estructurados, por ejemplo un archivo como `context-document.template.yaml`, desde el cual se pueda generar una base Markdown para un tipo documental, idioma y nivel específico.

La generación propuesta todavía es pequeña: no busca resolver todo el sistema documental, sino validar si un template estructurado puede producir un documento útil, consistente y revisable.

## Puntos de dolor u oportunidades

El dolor principal es que mantener estructuras documentales similares de forma manual puede generar inconsistencias.

Puntos de dolor:

* repetición manual de front matter, secciones y preguntas guía
* riesgo de diferencias accidentales entre documentos del mismo tipo
* confusión entre tipo documental, scope y nivel de detalle
* dificultad para mantener versiones equivalentes entre idiomas
* riesgo de convertir tooling en producto antes de validar el primer caso
* dificultad para recordar qué segmentos aplican a L0 o L1

Oportunidades:

* generar documentos base desde templates estructurados
* preservar convenciones editoriales de VSlices Docs Standard
* hacer explícitos tipo documental, idioma y nivel
* reducir trabajo repetitivo
* validar un primer slice antes de diseñar una CLI más amplia
* mantener el resultado como Markdown editable y revisable

## Límites

Este contexto empieza en la necesidad de generar documentos Markdown base desde una definición documental estructurada.

Termina antes de definir una arquitectura completa de tooling.

Incluye:

* generación documental como proceso
* uso de templates estructurados
* selección de tipo documental
* selección de idioma
* selección de nivel documental
* generación de Markdown base

No incluye todavía:

* generación completa de todos los tipos documentales
* soporte multilenguaje avanzado
* validación profunda de relaciones entre documentos
* integración completa con MkDocs
* publicación automática
* arquitectura final de CLI
* generación de código de aplicación


## Decisiones iniciales

| Pregunta | Decisión |
| --- | --- |
| ¿Cuál será el primer tipo documental soportado? | Documento de contexto. |
| ¿El primer template será `context-document.template.yaml`? | Sí. |
| ¿Cómo se representarán segmentos aplicables a L0 y L1? | Como segmentos dentro del template. |
| ¿Cómo se seleccionará el idioma? | Al generar el documento, mediante `-L es`, `-L en`, `--Language es` o `--Language en`. |
| ¿Qué parte del documento debe generarse? | El esqueleto del documento con placeholders para edición humana. |
| ¿Cuándo podría evolucionar hacia tooling público? | En una iteración posterior, una vez validado que el enfoque funciona bien. |

