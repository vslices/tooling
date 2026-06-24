---
id: aggregate-root.consistency.document-template
type: domain-document
status: active
scope: aggregate-root
level: L0
related:
  - bounded-context.consistency.artifact-generation
  - flow.use-case.generate-document-from-template
---

<!--
Status: draft, active, resolved, superseded, or archived.
Scope: entity, aggregate-root, bounded-context.
Level: L0.
-->

# Consistencia del Agregado DocumentTemplate

## Elemento de dominio

`DocumentTemplate` representa una plantilla documental cargada desde un archivo YAML.

Su responsabilidad es describir la estructura mínima necesaria para generar un esqueleto Markdown editable.

En esta iteración, el agregado se usa para validar templates documentales como `context.doc-template.yaml`.

## Propósito

`DocumentTemplate` existe para proteger la consistencia interna de una plantilla antes de usarla para generar un documento.

Su propósito no es decidir si el contenido documental está bien escrito, sino asegurar que la plantilla tiene la información mínima necesaria para producir Markdown útil.

## Responsabilidad

`DocumentTemplate` debe impedir que el sistema genere documentos desde plantillas incompletas, vacías o inconsistentes.

Las decisiones que deben pasar por este agregado son:

* aceptar o rechazar una plantilla como válida
* responder con errores esperados cuando una plantilla no puede usarse

## Consistencia

* La plantilla debe ser coherente con el esquema "Template"
* Cada objeto en "front-matters" debe ser coherente con el esquema de FrontMatter
* Cada objeto en "segments" debe ser coherente con el esquema de Segment.
* Los objetos en "segments" son unicos según el par "language" y "title"
* Los objetos en "front-matters" son unicos según el par "language" y "title"

## Fuera de alcance

`DocumentTemplate` no debe resolver:

* búsqueda de archivos 
* resolución del template usando `{type}.{target}-template.yaml`
* permisos de lectura del archivo
* permisos de escritura del documento generado
* nombre final del archivo generado
* calidad editorial del contenido
* traducción automática
* publicación en MkDocs
* diagnóstico detallado de validación para el usuario
* validación de relaciones entre documentos

Estas responsabilidades pertenecen al caso de uso, al bounded context o a comandos específicos como `vslices validate`.

## Decisiones de consistencia

| Pregunta | Decisión |
| --- | --- |
| ¿Qué códigos internos usaremos para los errores esperados de `DocumentTemplate`? | Los errores de consistencia que hayan deben usarse el codigo 1, al ser errores de consistencia. |
| ¿La versión `1` será la única soportada en esta iteración? | Sí. |
| ¿`comments` debe ser obligatorio o puede ser opcional? | Puede ser opcional. |
| ¿`front-matter` debe ser obligatorio para todos los templates documentales? | Oficialmente será requerido porque entrega metadata útil para análisis posterior de otros productos, como Surreal Atlas. Sin embargo, técnicamente será opcional en esta iteración. |
| ¿Un template puede tener segmentos sin comentarios guía? | Sí. |
| ¿La validación de idioma debe aceptar solo `es` y `en` o cualquier código compatible? | Debe aceptar cualquier texto con formato válido, aunque `es` y `en` sean los idiomas oficialmente soportados por los templates de VSlices. |

## Otros

### Estructura mínima de template documental

```yaml
version: 1
generator-version: 1
name: <struct-name>

front-matters:
  - language: <lang>
    text: <column-text>
    value: <column-value>
    defaults:
      - <default-value>

segments:
  - language: <lang>
    type: <header-type>
    title: <segment-title>
    comments: <segment-comments>
    applies-to:
      - <level>
```

### Campos

Esquema "Segmento":

| Campo        | Obligatorio | Tipo           | Formato o restricción                                                           |
| ------------ | ----------- | -------------- | ------------------------------------------------------------------------------- |
| language     | Sí          | Texto          | Código de idioma.                                                               |
| type         | Sí          | Texto          | Selección entre `H1`, `H2`, `H3`, `H4`, `H5` y `H6`.                            |
| title        | Sí          | Texto          | Máximo 128 caracteres.                                                          |
| comments     | No          | Texto          | Máximo 1024 caracteres.                                                         |
| applies-to   | Sí          | Lista de texto | Maximo 4 caracteres cada elemento, al menos un nivel soportado, unicos entre sí |


Esquema "FrontMatter"

| Campo     | Obligatorio | Tipo           | Formato o restricción                               |
| --------- | ----------- | -------------- | --------------------------------------------------- |
| language  | Sí          | Texto          | Código de idioma.                                   |
| text      | Sí          | Texto          | Máximo 128 caracteres.                              |
| value     | Sí          | Texto          | Máximo 128 caracteres.                              |
| defaults  | No          | Lista de Texto | Maximo 32 caracteres cada elemento, unicos entre sí |

Esquema "Template"

| Campo         | Obligatorio | Tipo              | Formato o restricción  |
| ------------- | ----------- | ----------------- | ---------------------  |
| version       | Sí          | Texto             | Formato "vX.Y.Z.W".    |
| min-generator | Sí          | Texto             | Formato "vX.Y.Z.W".    |
| name          | Sí          | Texto             | Máximo 128 caracteres. |
| front-matters | No          | Lista FrontMatter | N/A                    |
| segments      | Sí          | Lista Segmento    | Al menos un segmento.  |
