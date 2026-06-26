---
id: process.structure.document-generation
type: structure-document
status: active
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
Scope: work-line, process, flow, step.
Level: L0, L1.

-->

# Estructura del Proceso de Generación Documental

## Estructura de trabajo

La generación manual de documentos consiste en crear, ajustar y mantener documentos Markdown a partir de una intención documental definida por VSlices Docs Standard.

Actualmente el trabajo se realiza principalmente de forma manual: se identifica el tipo documental necesario, se escribe o adapta una estructura Markdown, se agregan secciones, preguntas guía, front matter y convenciones editoriales, y luego se revisa si el resultado mantiene coherencia con el resto de la documentación.

## Propósito

Esta estructura existe para preservar documentación útil, consistente y alineada con la intención de VSlices.

Su propósito no es solo producir archivos Markdown, sino mantener continuidad entre:

* el tipo documental
* el nivel de detalle
* la intención editorial
* las preguntas guía
* el contexto donde se usará el documento
* la evolución futura del estándar

## Detallado

1. Se identifica una necesidad documental dentro de VSlices Docs Standard, Method, Framework o Alive Lab.
2. Se decide qué tipo documental parece adecuado para preservar esa información.
3. Se determina el nivel de detalle necesario, normalmente L0 o L1.
4. Se crea o adapta una estructura Markdown existente.
5. Se define el front matter del documento.
6. Se agregan secciones y preguntas guía según la responsabilidad del documento.
7. Se revisa si el documento mantiene la intención documental original.
8. Se ajustan nombres, títulos, secciones y convenciones editoriales.
9. Se relaciona el documento con otros artifacts cuando corresponde.
10. Se mantiene o actualiza manualmente cuando el estándar evoluciona.

## Valores de entrada

* Necesidad documental observada.
* Tipo documental esperado.
* Nivel de detalle requerido.
* Idioma del documento.
* Convenciones editoriales de VSlices Docs Standard.
* Documentos existentes que sirven como referencia.
* Aprendizajes desde proyectos reales o Alive Lab.

## Valores de salida

* Documento Markdown revisable.
* Estructura documental inicial o actualizada.
* Preguntas guía preservadas.
* Front matter definido.
* Relaciones documentales explícitas cuando aplican.
* Posibles observaciones para mejorar Docs Standard o Method.

## Puntos de dolor o riesgos

* La estructura puede duplicarse de forma inconsistente entre documentos similares.
* Las preguntas guía pueden variar accidentalmente entre tipos documentales.
* Las convenciones editoriales pueden olvidarse.
* El nivel de detalle puede confundirse con el tipo documental.
* Una Support Note puede confundirse con un documento L0.
* Los cambios en el estándar requieren actualización manual en varios documentos.
* Puede aparecer sobre-documentación si se crean documentos antes de validar su necesidad.
* El proceso manual puede dificultar mantener continuidad entre Method, Docs Standard y artifacts reales.

## Decisiones iniciales

| Pregunta | Decisión |
| --- | --- |
| ¿Qué parte de este proceso debería mantenerse manual por criterio de diseño? | La generación del contenido específico de cada documento debe mantenerse manual. |
| ¿Qué parte podría apoyarse con tooling sin reemplazar el juicio documental? | La generación de esqueletos documentales, variando por tipo documental, idioma y nivel. |
| ¿Cuál debería ser el primer tipo documental generado? | Documento de contexto. |
| ¿Cómo representar tipo documental, idioma y nivel de detalle sin sobrediseñar? | Mediante un template YAML por tipo documental, con segmentos encargados de definir títulos y contenido. Cada segmento puede variar su escritura según pertenezca a metadata o body. |
| ¿Cuándo una estructura documental debe nacer como L0 y cuándo como L1? | Cada nivel debe responder a una necesidad específica del documento. Para el caso de uso, L0 describe el caso de éxito y L1 agrega respuestas ante condiciones esperadas. |
| ¿Qué reglas editoriales deberían ser verificables por tooling? | Ninguna regla editorial profunda en esta iteración. Si el template es válido, el tooling debe generar el documento correctamente. La responsabilidad de diseñar templates correctos pertenece al estándar documental. |
