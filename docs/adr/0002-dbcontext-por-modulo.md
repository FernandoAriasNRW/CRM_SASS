# ADR-0002: Un `DbContext` por módulo sobre una base de datos compartida

**Estado:** Aceptado
**Fecha:** 2026-08-11

## Contexto

El [ADR-0001](0001-monolito-modular.md) establece fronteras entre módulos que el compilador
impone. Pero un monolito modular se degrada por la base de datos antes que por el código:
basta un `Include` que cruce contextos o un `JOIN` entre tablas de dos módulos para que la
frontera deje de existir en la práctica, aunque los proyectos sigan separados.

## Decisión

Cada módulo declara su propio `DbContext` con únicamente sus entidades. Los 13 apuntan a la
**misma base de datos física**, pero ninguno conoce las tablas de otro.

Las consultas que necesitan datos de varios módulos se resuelven en la capa de aplicación
componiendo llamadas, o contra los modelos de lectura de `Reporting`, que se alimentan por
eventos de integración.

## Opciones consideradas

**A: Un `CrmDbContext` único.** Más simple y permite `JOIN` entre módulos en una sola
consulta. Se descarta precisamente por eso: hace trivial acoplar módulos y la frontera se
pierde sin que nadie tome la decisión de perderla.

**B: `DbContext` por módulo, base compartida (elegida).** Impide el acoplamiento accidental
a nivel de datos manteniendo una sola base que operar y respaldar. Las transacciones que
cruzan módulos dejan de ser atómicas, y eso es deliberado: obliga a modelar la consistencia
eventual de forma explícita (ver [ADR-0003](0003-outbox.md)).

**C: `DbContext` y base por módulo.** El aislamiento más fuerte, pero multiplica el coste
operativo sin que hoy haya ningún módulo que lo justifique. Es la evolución natural si uno
de ellos necesita escalar por separado.

## Consecuencias

**Más fácil:** razonar sobre un módulo aislado; extraerlo más adelante.

**Más difícil:** las consultas que cruzan módulos. No hay transacción atómica entre dos
contextos, así que toda operación que abarque varios necesita el patrón Outbox.

**A revisar:** si las composiciones en capa de aplicación se vuelven un problema de
rendimiento, la respuesta es ampliar los modelos de lectura de `Reporting`, no fusionar
contextos.

## Acciones

1. [x] 13 `DbContext` independientes
2. [x] Base común de aislamiento multi-tenant ([ADR-0004](0004-aislamiento-multi-tenant.md))
3. [ ] Prueba de arquitectura que falle si un `DbContext` registra entidades de otro módulo
