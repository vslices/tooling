---
id: continuity.iteration
type: continuity-document
status: draft
level: L0
related:
    - context.tooling
scope: iteration
---

<!--

Status: draft, active, resolved, superseded, or archived.
Level: L0, L1
Scope: stage, iteration, project

-->


# Continuidad de iteración para tooling documental

## Contexto de continuidad

Dado que el tooling de VSlices está recién empezando, uno de los puntos importantes es mantener la raíz del proyecto de software de la fricción nacida al momento de definir las plantillas de VSlices Docs Standard.

Es importante mencionar que estamos trabajando esto en medio de un proceso de documentación de la suite, por lo que mantener en línea el porqué estamos haciendo esto es vital para no salir de lo que haremos.


## Continuidad en riesgo

La continuidad entre la intención documental definida por VSlices Docs Standard y los documentos Markdown mantenidos o generados para proyectos reales.


## Camino principal

Se empleará el camino de "Proyecto de software" para preservar esta continuidad, ya que se encarga de definir el contexto, scope y componentes de una solución de software desde el punto de vista de un analista de software.

A finales de esta iteración, esperamos elaborar sobre este camino de continuidad.

```mermaid
flowchart LR
    A{{VSlices Tooling<br/><small>Contexto</small>}}

    A --> AB[Aborda] --> E1[[VSlices Docs Standard<br/><small>Contexto</small>]]

    A --> INC[Abarca] --> L1{{Generación de documentación<br/><small>Caso de uso</small>}}

    A --> EM[Emplea] --> S1>No aplican servicios o productos]
    
    A --> CAP[Requiere] --> C1>No aplican capacidades]

    A --> INF[Influenciado por] --> D1>No aplican decisiones]

    click E1 "Ir a introducción de VSlices Docs Standard" "https://"

```

## Caminos secundarios

### Contexto de dominio

Se empleará el camino de "Contexto de dominio" para dar sentido y eliminar ambigüedades de las terminologías empleadas en este sistema, junto con contemplar posibles delimitaciones de contexto futuras.

```mermaid
flowchart LR
    A[[VSlices Docs Standard<br/><small>Contexto</small>]]

    A --> L1[[Definiciones de términos generales]]
    A --> L2{{Definiciones de términos especificos}}

    A --> R>No aplican definiciones de reglas]

    A --> B[Expresa comportamientos]
    
    B --> U1{{Generación de documentos<br/><small>Caso de uso</small>}}

    A --> C>No hay capacidades originales]

    A --> S>No hay servicios o productos<br/>en este contexto]
    
    A --> D[Delimitado por]
    D --> D1[[Introducción de<br/>VSlices Docs Standard]]

    click A "Ir a introducción de VSlices Docs Standard" "https://"
    click L1 "Ir a glosarios de VSlices" "https://"
    click D1 "Ir a introducción de VSlices Standard" "https://"

```

## Revisiones de continuidad

| Stage           | Resultado | Cambio en continuidad | Referencia |
| --------------- | --------- | --------------------- | ---------- |
| Understanding   | Pendiente | Pendiente             | Pendiente  |
| Contextualizing | Pendiente | Pendiente             | Pendiente  |
| Planning        | Pendiente | Pendiente             | Pendiente  |
| Building        | Pendiente | Pendiente             | Pendiente  |
| Validating      | Pendiente | Pendiente             | Pendiente  |

