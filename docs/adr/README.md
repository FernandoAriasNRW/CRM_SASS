# Registro de decisiones de arquitectura

Un ADR documenta **por qué** se tomó una decisión, no cómo funciona el código. El código
dice qué hace; el ADR dice qué alternativas se descartaron y a cambio de qué, para que
quien venga después pueda revisar la decisión con el mismo contexto en lugar de deducirlo.

Se escribe un ADR cuando una decisión es difícil de revertir, afecta a varios módulos, o
alguien razonable habría elegido distinto.

| # | Decisión | Estado |
|---|---|---|
| [0001](0001-monolito-modular.md) | Monolito modular en lugar de microservicios | Aceptado |
| [0002](0002-dbcontext-por-modulo.md) | Un `DbContext` por módulo sobre base compartida | Aceptado |
| [0003](0003-outbox.md) | Patrón Outbox para eventos de integración | Aceptado |
| [0004](0004-aislamiento-multi-tenant.md) | Aislamiento multi-tenant por filtro global | Aceptado |
| [0005](0005-estrategia-de-tokens.md) | Separación entre token de acceso y de refresco | Aceptado |

Los cinco documentan decisiones **ya implementadas**. Se escriben ahora porque estaban
tomadas pero no registradas: el razonamiento vivía en la cabeza de quien las tomó, y eso es
lo que se pierde primero.

## Formato

Contexto → Decisión → Opciones consideradas → Compromisos → Consecuencias → Acciones.

Las consecuencias incluyen lo que se vuelve **más difícil**, no sólo lo que mejora. Un ADR
que sólo lista ventajas no está documentando una decisión, está justificándola.
