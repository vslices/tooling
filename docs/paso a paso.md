# Iteración: Slice-First para tooling documental

## 1. Definir el contexto inicial

### ¿Cuál es el dolor?

> Mantener plantillas documentales consistentes de forma manual empieza a generar duplicación, riesgo editorial y pérdida de continuidad.

### ¿Dónde estamos trabajando?

Estamos en tooling interno/estructural para sostener continuidad documental.

### ¿Qué situación concreta queremos mejorar?

> Hoy las plantillas viven como Markdown escrito manualmente. Queremos explorar si una fuente estructurada mínima puede generar esos Markdown de manera consistente.

### ¿Qué debemos preservar prioritariamente?

Dado el dolor y el contexto, lo prioritario es preservar:

* intención documental
* estructura mínima y extendida de cada documento
* preguntas guía
* convenciones editoriales
* nombres de secciones
* diferencias entre tipos documentales
* posibilidad de evolución progresiva
* trazabilidad entre Method, Docs Standard y artifacts reales

!!! note "¡Cuidado! El contexto afecta"

    Dado que el equipo y el contexto es pequeño, no es necesario generar un documento de contexto de empresa o proyecto; pero en equipos más grandes, es más probable que se deban generar un documento, para centralizar la verdad

Por ejemplo, para si donde trabajas dan servicios de desarrollo a una empresa externa, seria conveniente tener un context.[organization-name] y un context.[project-name], ya que personas nevas no tienen este contexto

En cambio, si eres interno de una empresa y trabajas en un proyecto de la misma, puedes solo generar un context.[project-name]

## 2. Definir continuidad inicial y modalidad

Aquí todavía seguimos antes de Understanding formal.

### ¿Qué caminos de continuidad parecen más relevantes?

### Camino principal

Por ahora: **Proyecto de software**, porque el trabajo apunta a convertir una necesidad real en una pieza técnica implementable.

### Caminos secundarios

**Contexto de dominio**, porque importa el lenguaje de documentos, templates, profiles, sections, stages y continuity paths debe quedar claro.

### Modalidad de trabajo

La modalidad de trabajo escogida es Slice-first, ya que nos permitirá sanar el dolor rapido y poder continuidad con la documentación de VSlices Docs Standard.

!!! note "¡Cuidado! El contexto afecta"

    Dado que el equipo y el contexto es pequeño, no es necesario generar un documento de contexto de iteración; pero en equipos más grandes, es más probable que se deba generar un documento, para centralizar la verdad


## 3. Esbozar caminos de continuidad

[Ver doucmento de continuidad del stage de understanding](./iteration%201/understanding/continuity.stage.md)

!!! risk "Cuidado con el alcance"

    Considera que el documento de continuidad evoluciona junto con las mismas iteraciones y stages de cada una, en esta versión debes especificar unicamente lo minimo para poder orientarte en base a lo que sabe sin indagar a fondo, es algo así como un tanteo de terreno

## 4. Llevar a cabo Understanding

    Dolor visible:
      En el proceso de documentación de VSlices, se deben generar las plantillas de Docs Standard de forma manual

    Dolor profundo:
      Se debe mantener la coherencia entre tipo de lenguaje, idioma y nivel de detalle de forma manual

    Riesgo:
      Que cada documento termine evolucionando distinto, aunque comparta una intención común, variando según tipo, lenguaje y nivel de detalle
  
    Lenguage inicial: 
      - [Vocabulario de dominio de VSlices Tooling](./vocabulary.vslices-tooling.md)

    
    

   - Reconocer actores, artefactos, procesos, fricciones y conocimiento implícito.
   - Generar documentación necesaria para preservar entendimiento.

   Cierre del stage:

   - Actualizar y/o extender caminos de continuidad según lo descubierto.

### Observación critica

Durante Understanding apareció una observación crítica sobre la estructura documental mínima necesaria para explicar software desde VSlices.

Para evitar que `Process Document`, `Flow Document` y `Work Line Document` se conviertan en tipos documentales separados con responsabilidades solapadas, se propone observar una tríada documental base:

- **Documento de contexto**: explica qué es algo y dónde está.
- **Documento de estructura**: explica cómo es algo y qué hace.
- **Documento de caso de uso**: explica cómo lo implementaremos como comportamiento del sistema.

Estos documentos pueden relacionarse en cascada:

```txt
Contexto
  contiene N estructuras

Estructura
  contiene N casos de uso
````

También pueden ser recursivos:

```txt
Contexto
  contiene N contextos

Estructura
  contiene N estructuras
```

Esta observación permite explicar conceptos con detalle progresivo sin crear un tipo documental distinto para cada profundidad del trabajo.

Una línea de trabajo, un proceso y un flujo pueden entenderse como distintos niveles o enfoques de una misma familia documental: **estructura**.

La regla inicial queda así:

> Contexto explica qué es y dónde está.
> Estructura explica cómo es y qué hace.
> Caso de uso explica cómo lo implementaremos.

Esta observación todavía no formaliza una decisión final del Docs Standard, pero debe preservarse porque afecta directamente el lenguaje documental, el modelado de templates y el futuro tooling.


### Observación: estrategias de nombre documental

Durante Understanding apareció la necesidad de permitir más de una estrategia de naming para documentos.

Una convención única puede volver algunos nombres poco claros. Por eso, los nombres de archivo deberían preservar intención documental y claridad de lectura.

Se observan tres estrategias útiles:

| Patrón | Uso | Ejemplo |
| --- | --- | --- |
| `[doc-type].[doc-scope].md` | Cuando el scope es más importante que un nombre específico. | `context.iteration.md` |
| `[doc-type].[name].md` | Cuando el nombre del concepto es más claro que el scope. | `context.vslices-tooling.md` |
| `[doc-scope].[doc-type].[name].md` | Cuando el scope agrupa varios documentos relacionados del mismo concepto. | `work-line.structure.buy-line.md` |

La regla inicial es:

> El nombre del archivo debe optimizar la claridad documental, no imponer una fórmula única.

Para iteraciones simples se prefiere `[doc-type].[name].md`.  
Para documentos metodológicos de ciclo puede usarse `[doc-type].[doc-scope].md`.  
Para jerarquías documentales más grandes puede usarse `[doc-scope].[doc-type].[name].md`.

### Observación crítica: VSlices Tooling como línea de trabajo

Durante Understanding se observó que VSlices Tooling todavía no debe tratarse como producto independiente.

En esta iteración, VSlices Tooling aparece principalmente como una línea de trabajo de VSlices Docs Standard, nacida desde la necesidad de preservar continuidad entre intención documental, plantillas, niveles documentales, scopes y documentos Markdown reales.

Esto no descarta que VSlices Tooling pueda evolucionar más adelante hacia un proyecto, producto o componente de VSlices Framework. Sin embargo, para esta iteración, tratarlo como línea de trabajo reduce sobreingeniería y mantiene el foco en el dolor actual.

## 5. Llevar a cabo Contextualizing

   Actividades:

   - Ubicar el problema dentro de su contexto.
   - Relacionar dominio, producto, proyecto, usuarios, sistemas o procesos involucrados.
   - Generar documentación necesaria para preservar contexto.

   Cierre del stage:

   - Actualizar ý/o extender caminos de continuidad según lo contextualizado.

### Observación: Understanding no debe cerrar la estructura completa

Understanding puede identificar conceptos, fricciones y relaciones candidatas, pero no necesita cerrar diagramas completos de continuidad, estructura o relación entre artifacts.

Cuando el equipo empieza a ordenar caminos, scopes, jerarquías y relaciones documentales, probablemente ya entró en Contextualizing.

La regla inicial queda así:

> Understanding descubre el material.  
> Contextualizing ordena el material.

## 6. Llevar a cabo Planning

   Actividades:

   - Definir el slice de trabajo.
   - Separar problema actual de problemas futuros.
   - Decidir alcance, límites, riesgos, alternativas y criterios de avance.
   - Generar documentación necesaria para preservar intención de diseño.

   Cierre del stage:

   - Actualizar y/o extender caminos de continuidad según el plan.


    