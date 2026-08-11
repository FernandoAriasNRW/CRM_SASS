# ADR-0001: Monolito modular en lugar de microservicios

**Estado:** Aceptado (documenta una decisión ya implementada)
**Fecha:** 2026-08-11
**Decisores:** Equipo de arquitectura

## Contexto

El producto es una plataforma de work management que compite con ClickUp y Monday.com.
El dominio tiene 13 contextos acotados (Identity, Projects, WorkItems, Ticketing,
Notifications, Calendar, Communication, Docs, Reporting, Tags, Teams, Webhook) y se
espera que crezca: campos personalizados, automatizaciones y vistas están en el
roadmap.

El equipo es pequeño. No hay plataforma de orquestación en producción ni personal
dedicado a operarla.

## Decisión

Un único despliegue (`ApiHost`) que compone 13 módulos, cada uno separado físicamente
en cuatro proyectos: `Domain`, `Application`, `Infrastructure`, `Presentation`.

Cada módulo tiene su propio `DbContext` (ver [ADR-0002](0002-dbcontext-por-modulo.md)) y
se comunica con los demás por eventos de integración, no por referencias directas entre
capas de aplicación.

## Opciones consideradas

### Opción A: Microservicios desde el principio

| Dimensión | Valoración |
|---|---|
| Complejidad | Alta: 13 despliegues, descubrimiento, trazabilidad distribuida |
| Coste | Alto: orquestación y observabilidad desde el día uno |
| Escalabilidad | Excelente por servicio |
| Familiaridad del equipo | Media |

**A favor:** escalado y despliegue independientes; aísla fallos.
**En contra:** el coste operativo recae en un equipo que no tiene con qué asumirlo. Las
fronteras entre contextos aún se están moviendo, y equivocarse en una frontera sale
mucho más caro cuando cada una es un límite de red.

### Opción B: Monolito por capas (sin módulos)

| Dimensión | Valoración |
|---|---|
| Complejidad | Baja al principio |
| Coste | Bajo |
| Escalabilidad | Sólo horizontal, en bloque |
| Familiaridad del equipo | Alta |

**A favor:** lo más rápido de arrancar.
**En contra:** sin fronteras que lo impidan, los contextos se enredan. Extraer un
servicio después deja de ser viable.

### Opción C: Monolito modular (elegida)

| Dimensión | Valoración |
|---|---|
| Complejidad | Media: fronteras explícitas, un solo despliegue |
| Coste | Bajo |
| Escalabilidad | Horizontal en bloque; extraíble por módulo más adelante |
| Familiaridad del equipo | Alta |

**A favor:** las fronteras se respetan porque el compilador las impone —un módulo no
puede referenciar el `Domain` de otro—, y el coste operativo sigue siendo el de un solo
despliegue. Si un módulo necesita escalar por separado, ya está aislado.
**En contra:** no permite escalar ni desplegar por módulo. Nada impide compartir base de
datos salvo la disciplina; de ahí el ADR-0002.

## Análisis de compromisos

El eje real es cuándo pagar el coste de la distribución. Los microservicios lo cobran por
adelantado y a cambio dan independencia; el monolito modular lo aplaza conservando la
opción de comprarla después.

Esa opción sólo se conserva si las fronteras son de verdad. Por eso la separación en
cuatro proyectos por módulo no es ceremonia: es lo que convierte una violación de
frontera en un error de compilación en lugar de en una convención que alguien romperá.

## Consecuencias

**Más fácil:** desplegar, depurar y hacer refactors que cruzan módulos. Las transacciones
dentro de un módulo son locales.

**Más difícil:** escalar un módulo por separado. Un fallo grave tumba todo el proceso.

**A revisar:** si un módulo concreto (previsiblemente Reporting o Notifications) domina el
consumo de recursos, o si el equipo crece hasta el punto de que los despliegues se
estorban entre sí.

## Acciones

1. [x] Separación física en cuatro proyectos por módulo
2. [x] Comunicación entre módulos por eventos de integración
3. [ ] Prueba de arquitectura que falle si un módulo referencia el `Domain` de otro
