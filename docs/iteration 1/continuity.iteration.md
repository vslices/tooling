---
id: continuity.iteration
type: continuity-document
status: draft
level: L0
scope: iteration
related:
    - context.vslices-tooling
    - process.context.document-generation
    - process.structure.document-generation
---

<!--

Status: draft, active, resolved, superseded, or archived.
Level: L0, L1
Scope: stage, iteration, project

-->


# Continuidad de iteración para tooling documental

## Contexto de continuidad

Dado que el tooling de VSlices está recién empezando, uno de los puntos importantes es mantener la raíz de la línea de trabajo técnica nacida desde la fricción documental al momento de definir las plantillas de VSlices Docs Standard.

Es importante mencionar que estamos trabajando esto en medio de un proceso de documentación de la suite, por lo que mantener en línea el porqué estamos haciendo esto es vital para no salir de lo que haremos.


## Continuidad en riesgo

La continuidad entre la intención documental definida por VSlices Docs Standard y los documentos Markdown mantenidos o generados para proyectos reales.


## Camino principal

Se empleará el camino de "Proyecto de software" para preservar esta continuidad, porque la iteración está convirtiendo una fricción documental observada en una línea de trabajo técnica (no de negocio) dentro de VSlices Docs Standard.

A finales de esta iteración, esperamos entender si esta línea de trabajo debe mantenerse dentro de Docs Standard, evolucionar hacia tooling propio o integrarse más adelante con VSlices Framework.

```mermaid
flowchart LR
    A[[VSlices Tooling<br/><small>Contexto</small>]]

    A --> AB[Aborda] --> E1[[VSlices Docs Standard<br/><small>Contexto</small>]]

    A --> INC[Abarca]
    INC --> P1[[<b>Proceso</b><br/>Generación documental<br/><small>Contexto</small>]]
    P1 --> F1><b>Flujo</b><br/>Generación manual<br/><small>Contexto</small>]

    A --> EM[Emplean]
    EM --> U1[[<b>Caso de uso</b><br/>Generación desde template]]
    
    A --> CAP[Requiere]
    CAP --> C1>Lectura de archivos]
    CAP --> C2>Escritura de archivos]

    A --> INF[Influenciado por]
    INF --> D1[[Definición de tooling]]
    INF --> D2[[Definición de comandos]]

    click A "Ir a Contexto de VSlices Tooling" "../context.vslices-tooling.md"
    click E1 "Ir a introducción de VSlices Docs Standard" "https://"
    click P1 "Ir a la Contexto del Proceso de Generación Documental" "understanding/process.context.document-generation.md"
    click D1 "Ir a definición de VSlices Tooling" "../decisions/0001.vslices-tooling-definition.md"
    click D2 "Ir a definición de comandos de VSlices Tooling" "../decisions/0002.vslices-commands-definition.md"
    click U1 "Ir a definición de Caso de uso de Generación desde template" "planning/flow.use-case.template-generation.md"


```

## Caminos secundarios

### Contexto de dominio

Se empleará el camino de "Contexto de dominio" para dar sentido y eliminar ambigüedades de las terminologías empleadas en este sistema, junto con contemplar posibles delimitaciones de contexto futuras.

```mermaid
flowchart LR
    A[[VSlices Docs Standard<br/><small>Contexto</small>]]

    A --> LE[Definiciones de términos] 
    LE --> T1[[Términos generales]]
    LE --> T2[[Términos específicos]]

    A --> ID[Posee definiciones]
    ID --> CD1[[<b>Domain Context</b><br/>Generación de artefactos<br/><small>Consistencia</small>]]
    CD1 --> AR1[[<b>Aggregate Root</b><br/>Plantilla de documento<br/><small>Consistencia</small>]]

    A --> B[Expresa comportamientos]
    
    B -- dentro de --> P1[[<b>Proceso</b><br/>Generación documental<br/><small>Contexto</small>]]
    P1 -- está --> F1><b>Flujo</b><br/>Generación manual<br/><small>Contexto</small>]

    A --> OR[Origina capacidades]
    OR --> C1>Generación documental]

    A --> OW[Posee software]
    OW --> PR1[[VSlces Tooling<br/><small>Producto</small>]]
    
    A --> D[Delimitado por]
    D --> D1[[Introducción de<br/>VSlices Docs Standard]]

    click A "Ir a introducción de VSlices Docs Standard" "https://"
    click T1 "Ir a glosario de VSlices Docs Standard" "https://"
    click T2 "Ir a vocabulario de VSlices Tooling" "../../../vocabulary.vslices-tooling.md"
    click D1 "Ir a introducción de VSlices Standard" "https://"
    click P1 "Ir a Contexto del Proceso de Generación Documental" "understanding/process.context.document-generation.md"
    click PR1 "Ir a definición de VSlices Tooling" "https://"

    click CD1 "Ir a definición de Consistencia de Generación de artefactos" "https://"

```

### Escenario de trabajo

Se empleará el camino de "Escenario de trabajo" para dar sentido al escenario donde nace esta línea técnica, así como delimitar correctamente sus estructuras de trabajo.

```mermaid
flowchart TD
    E1[[<b>Escenario</b><br/>VSlices Docs Standard<br/><small>Contexto</small>]]
    L1[[<b>Línea técnica</b><br/>VSlices Tooling<br/><small>Contexto</small>]]
    P1[[<b>Proceso</b><br/>Generación documental<br/><small>Estructura</small>]]
    F1><b>Flujo</b><br/>Generación manual<br/><small>Estructura</small>]
    
    E1 --> L1
    L1 --> P1
    P1 --> F1 

    click E1 "Ir a introducción de VSlices Docs Standard" "https://"
    click L1 "Ir a Contexto de VSlices Tooling" "../context.vslices-tooling.md"
    click P1 "Ir a Estructura del Proceso de Generación documental" "understanding/process.structure.document-generation.md"

```

## Revisiones de continuidad

| Stage | Resultado | Cambio en continuidad | Referencia |
| --- | --- | --- | --- |
| Start | Aprobado | Creación del mapa inicial | Pendiente | 
| Understanding | Aprobado | Se identificaron conceptos y relaciones candidatas. Los diagramas detallados se moverán a Contextualizing. | Pendiente |
| Contextualizing | Aprobado | VSlices Tooling es una línea técnica dentro de VSlices Docs Standard y de ordenaron las relaciones que en Understanding se dejaron como candidadas | Pendiente |
| Planning | Aprobado | Se generó el proceso nuevo, junto con las definiciónes de dominio de Document Template y Artifact Generation | Pendiente |
| Building | Pendiente | Pendiente | Pendiente |
| Validating | Pendiente | Pendiente | Pendiente |

