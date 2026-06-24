---
id: flow.use-case.generate-document-from-template
type: use-case-document
status: draft
scope: flow
level: L1
related:
  - process.context.document-generation
  - process.structure.document-generation
  - 0001.vslices-tooling-definition
---

<!--
Status: draft, active, resolved, superseded, or archived.
Scope: flow-step, flow, process, work-line.
Level: L0, L1.
-->

# Generar documento desde template

## Caso de uso

Generar el esqueleto de un documento Markdown desde un template YAML, usando el tipo, idioma, nivel y output especificados por el usuario.

## Contextos y estructuras relacionados

Este caso de uso pertenece al flujo de generación documental de VSlices Tooling.

Está relacionado con:

* `process.up-context.document-generation`, que explica el contexto del proceso de generación documental
* `process.up-structure.document-generation`, que describe la estructura propuesta del proceso
* `0001.vslices-tooling-definition`, que define VSlices Tooling como proyecto propio dentro de la suite VSlices

El comportamiento se apoya en templates YAML definidos por tipo documental.

En esta iteración, se tendrá como caso de pruebas la generación de un Documento de contexto L0 en español desde `context.doc-template.yaml`.

## Consecuencias esperadas

Si el caso de uso se ejecuta correctamente:

* se genera un archivo Markdown en la ruta de salida
* el documento generado contiene front matter base
* el documento generado contiene los segmentos aplicables al nivel solicitado
* el documento generado usa el idioma solicitado
* el documento generado mantiene placeholders y comentarios guía para edición humana
* el usuario puede completar manualmente el contenido específico del documento

## Comportamiento

```mermaid
flowchart TB
  IN((Inicio)) 
  INPUT@{ shape: manual-input, label: "vslices generate -T 'context' -L 'es' -S 'L0' -O '/'"}
  VERIF1@{ shape: diamond, label: "¿Existe template para 'context'?"}
  PROC1@{ shape: procs, label: "Lectura del context.template.yaml" }
  
  VERIF2@{ shape: diamond, label: "¿El template es valido en formato?"}
  VERIF3@{ shape: diamond, label: "¿Existe lenguaje 'es' y<br/> extensión 'L0' en ese template?"}
  PROC2@{ shape: procs, label: "Toma segmentos según<br/>nivel y lenguaje" }
  PROC3@{ shape: procs, label: "Genera documento" }

  FI((FIN))

  EPL[Error permisos de lectura]
  ETN[Error template no existe]
  ECTI[Error contenido de <br/>template insuficiente]
  EPE[Error de permisos de escritura]

  IN--> INPUT --> VERIF1
  VERIF1 -- Sí --> PROC1
  VERIF1 -- No fue posible leer --> EPL --x FI
  VERIF1 -- No --> ETN --x FI

  PROC1 --> VERIF2
  VERIF2 -- Sí --> VERIF3
  VERIF2 -- No --> ECTI --x FI
  
  VERIF3 -- Sí --> PROC2 --> PROC3
  PROC3 -- Exito --> FI
  PROC3 -- Fallo --> EPE --x FI

```

## Validaciones

| Validación                                    | Razón                                                              | Error esperado                |
| --------------------------------------------- | ------------------------------------------------------------------ | ----------------------------- |
| El template solicitado existe.                | El sistema necesita saber qué template debe buscar.                | Tipo documento requerido      |
| El idioma solicitado existe en los segmentos. | El sistema necesita seleccionar los textos correctos del template. | Idioma requerido              |
| El nivel solicitado existe en algún segmento. | El sistema necesita seleccionar los segmentos aplicables.          | Nivel requerido               |
| El template no es valido en sintaxis.         | El sistema necesita tener un documento valido.                     | Error de validez del template |
| El template puede leerse.                     | El sistema necesita acceder al contenido del template.             | Error de acceso al template   |
| La ruta de salida permite escritura.          | El sistema necesita persistir el documento generado.               | Error de escritura en la ruta |

## Reglas de consistencia



* El tooling genera esqueletos documentales, no contenido final.
* El contenido específico del documento debe ser completado por una persona.
* El template define qué se genera.
* El tipo documental determina qué template se busca.
* El idioma determina qué textos del template se usan.
* Para generar un documento, el idioma solicitado debe estar disponible en todos los segmentos requeridos, si no error de formato del yaml
* El nivel determina qué segmentos del template se incluyen.
* Para generar un documento, el nivel solicitado debe producir al menos un segmento.
* El documento generado debe mantenerse editable como Markdown.
* El tooling no valida calidad editorial profunda en esta iteración.
* Si el template es válido, el tooling debe respetar su estructura.

## Errores esperados

| Error                         | Significado                                           | Cuando ocurre                                                                      |
| ----------------------------- | ----------------------------------------------------- | ---------------------------------------------------------------------------------- |
| Tipo document requerido       | No se indicó qué tipo de documento se quiere generar. | El comando no recibe tipo documental.                                              |
| Idioma requerido              | No se indicó en qué idioma generar el documento.      | El comando no recibe idioma.                                                       |
| Nivel requerido               | No se indicó qué nivel documental generar.            | El comando no recibe nivel.                                                        |
| Error de validez del template | El template existe, pero no es valido.                | Se pudo acceder al archivo, pero no siguio las validaciones de dominio.            |
| Error de acceso al template   | El template existe, pero no puede leerse.             | Hay problemas de permisos, ruta o acceso al archivo.                               |
| Error de escritura en la ruta | El documento no puede guardarse.                      | La ruta de salida no existe, no permite escritura o falla al persistir el archivo. |

## Valores de entrada

* Target de generación.
* Tipo documento.
* Lenguaje de generación.
* Nivel de extensión.
* Ruta de salida actual del CLI.

Ejemplo de comando:

```bash
vslices generate doc --Type "context" --Language "es" --Size "L0"
```

Versión corta:

```bash
vslices generate doc -T "context" -L "es" -S "L0"
```

## Valores de salida

* Archivo Markdown generado.
* Nombre final del archivo generado.
* Resultado exitoso de generación.
* Error esperado cuando no se puede generar el documento.

El nombre por defecto del archivo generado será:

```txt
{nombre-documento}.md
```

Si ya existe un archivo con ese nombre, se generará otro agregando `(N)` al final.

## Capacidades relacionadas

* Lectura de archivos.
* Escritura de archivos.

## Decisiones del caso de uso

| Pregunta | Decisión |
| --- | --- |
| ¿Cuál será la estructura final exacta de `context-document.template.yaml`? | Será definida en base a lo que entregue el negocio. El tooling no impone una estructura documental semántica más allá de que el template sea válido. |
| ¿El nombre base del documento vendrá desde el template, desde el tipo documental o desde un argumento futuro? | Por ahora, el nombre base viene por defecto. |
| ¿Los errores esperados se representarán como códigos internos, mensajes humanos o ambos? | Ambos. |
| ¿La ruta de salida podrá configurarse en esta iteración o quedará fija a la ruta actual del CLI? | El documento se genera en la misma ruta de ejecución. |
| ¿El template debe permitir segmentos sin comentarios guía? | Sí. Los segmentos pueden no tener comentarios guía, pero los templates oficiales deberían ser lo más informativos posible. |
| ¿El primer slice debe soportar solo `L0` o aceptar `L1`, `L2` y `L3` si el template los declara? | Soporta los niveles que el template soporte. |

## Preguntas abiertas

- ¿Qué forma exacta tendrá el nombre base generado por defecto?
- ¿Qué códigos internos usaremos para los errores esperados?
- ¿Qué estructura mínima hará que un template sea válido sin imponer semántica documental excesiva?

