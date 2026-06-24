---
id: aggregate-root.consistency.document-template
type: domain-document
status: active
scope: aggregate-root
level: L0
related:
  - bounded-context.consistency.document-generation
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
* exponer los datos necesarios para construir Markdown
* responder con errores esperados cuando una plantilla no puede usarse

## Consistencia

* La plantilla debe tener una versión.
* La plantilla debe tener nombre.
* La plantilla debe tener al menos un segmento.
* Cada fron
* Cada segmento debe tener idioma.
* Cada segmento debe tener tipo de encabezado.
* Cada segmento debe tener título.
* Cada segmento no debe repetir el mismo título en un idioma
* Cada segmento debe declarar al menos un nivel aplicable.
* Cada nivel declarado debe estar dentro de los niveles soportados.

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
name: <struct-name>

front-matter:
  - language: <lang>
    text: <column-text>
    value: <column-value>

segments:
  - language: <lang>
    type: <header-type>
    title: <segment-title>
    comments: <segment-comments>
    applies-to:
      - <level>
```

### Campos

Esquema "Segmento de texto":

| Campo        | Obligatorio | Tipo           | Formato o restricción                                |
| ------------ | ----------- | -------------- | ---------------------------------------------------- |
| language     | Sí          | Texto          | Código de idioma.                                    |
| type         | Sí          | Texto          | Selección entre `H1`, `H2`, `H3`, `H4`, `H5` y `H6`. |
| title        | Sí          | Texto          | Máximo 128 caracteres.                               |
| comments     | No          | Texto          | Máximo 1024 caracteres.                              |
| applies-to   | Sí          | Lista de texto | con al menos un nivel soportado.                     |


Esquema "Segmento FrontMatter"

| Campo     | Obligatorio | Tipo  | Formato o restricción  |
| --------- | ----------- | ----- | ---------------------- |
| language  | Sí          | Texto | Código de idioma.      |
| text      | Sí          | Texto | Máximo 128 caracteres. |
| value     | Sí          | Texto | Máximo 128 caracteres. |

Esquema base: Template

| Campo         | Obligatorio | Tipo                          | Formato o restricción                                   |
| ------------- | ----------- | ----------------------------- | ------------------------------------------------------- |
| version       | Sí          | Numero                        | Versión soportada por el generador.                     |
| name          | Sí          | Texto                         | Abierto, máximo 128 caracteres.                         |
| front-matters | No          | Lista de Segmento FrontMatter | Metadata del documento                                  |
| segments      | Sí          | Lista de Segmento de texto    | Código de idioma. Oficialmente se soportan `es` y `en`. |
