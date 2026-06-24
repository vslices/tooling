---
id: bounded-context.consistency.artifact-generation
type: consistency-document
status: active
scope: bounded-context
level: L0
related:
  - context.vslices-tooling
  - process.context.document-generation
  - process.structure.document-generation
  - flow.use-case.generate-document-from-template
---

<!--
Status: draft, active, resolved, superseded, or archived.
Scope: entity, aggregate-root, bounded-context.
Level: L0.
-->

# Consistencia del Bounded Context de Generación de artefactos

## Elemento de dominio

El bounded context de generación de artefactos describe el lenguaje, las reglas y las responsabilidades necesarias para generar esqueletos de documentos (y más a futuro) desde templates estructurados.

Este contexto no representa todo VSlices Docs Standard. Representa la parte del dominio donde una definición documental estructurada puede transformarse en un artifact que posteriormente es editado por humanos.

## Propósito

Este bounded context existe para proteger la continuidad entre:

* la intención documental definida por VSlices Docs Standard
* los templates usados como fuente de generación
* los parámetros entregados al CLI
* los documentos Markdown generados
* la edición humana posterior

Su propósito principal es asegurar que la generación de artefactos sea consistente sin reemplazar el criterio humano.

## Responsabilidad

Este bounded context es responsable de contener y definir las reglas que apliquen a la generación y validación de artefactos.

Debe proteger decisiones y reglas relacionadas con:

* resolución del template según tipo y target
* generación de artefactos
* respuesta explícita ante errores esperados de generación
* impedir generación de artefactos invalidos o imposibles de usar

## Consistencia

* Un artefacto generado, debe provenir de un template válido.
* Un template válido debe tener una estructura mínima viable.
* El target define qué tipo de artefacto se genera.
* El tipo define qué "categoria de artefacto" debe buscarse.
* El tooling genera artefactos base, no contenido final.
* El contenido específico del artefacto debe ser completado por una persona.
* El tooling no valida calidad editorial profunda en esta iteración.


## Fuera de alcance

Este bounded context no debe resolver:

* calidad editorial del contenido generado
* corrección semántica de los artefactos
* publicación automática
* traducción automática
* definición completa de todos los tipos documentales de VSlices Docs Standard

La responsabilidad de diseñar templates correctos pertenece al estándar documental y al criterio humano que los mantiene.

## Decisiones de consistencia

| Pregunta | Decisión |
| --- | --- |
| ¿Qué estructura mínima exacta hará que un template sea válido sin imponer semántica documental excesiva? | La estructura mínima válida para templates documentales será `version`, `name`, `front-matter` y `segments`. |
| ¿Qué códigos internos usaremos para los errores esperados? | Los códigos internos comenzarán desde 1, siendo este errores de consistencia y crecerán secuencialmente a medida que se necesiten nuevos errores. |
| ¿Qué invariantes pertenecen al bounded context completo y cuáles pertenecen específicamente al aggregate root `DocumentTemplate`? | Las invariantes específicas del template pertenecen a `DocumentTemplate`. El bounded context conserva reglas de consistencia generales sobre generación documental. |
| ¿Cuándo este bounded context debería ampliarse para soportar targets distintos de `doc`? | El contexto ya permite extensión futura, pero posibilidad no implica obligación. Nuevos targets solo se agregarán cuando exista una necesidad validada. |
| ¿Cuándo la validación de templates debe pasar desde validación mínima a diagnóstico detallado mediante `vslices validate`? | `vslices generate` y `vslices validate` usan las mismas invariantes de consistencia de `DocumentTemplate`. La diferencia está en la intención del comando: `generate` valida para construir, `validate` valida para diagnosticar. |
