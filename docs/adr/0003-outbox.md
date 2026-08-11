# ADR-0003: Patrón Outbox para los eventos de integración

**Estado:** Aceptado
**Fecha:** 2026-08-11

## Contexto

El [ADR-0002](0002-dbcontext-por-modulo.md) deja los módulos sin transacción común: crear una
tarea en `WorkItems` y notificar en `Notifications` son dos escrituras que no se pueden
confirmar juntas.

Publicar el evento directamente desde el handler abre dos fallos simétricos e igual de
malos:

- Publicar **antes** de confirmar: si la transacción se deshace, se ha notificado algo que
  nunca ocurrió.
- Publicar **después** de confirmar: si el proceso muere en medio, el cambio existe pero
  nadie se entera. Nada lo detecta y nada lo reintenta.

## Decisión

El evento se escribe en la tabla `outbox_messages` **dentro de la misma transacción** que el
cambio de negocio. Un worker (`OutboxDispatcherWorker`) los lee y los despacha después.

Los eventos de dominio quedan dentro del módulo; sólo los de integración
(`BuildingBlocks.Contracts/IntegrationEvents`) pasan por el Outbox.

## Opciones consideradas

**A: Publicación directa desde el handler.** Sin infraestructura adicional, pero con los dos
modos de fallo descritos. Se descarta.

**B: Outbox transaccional (elegida).** El evento y el cambio comparten atomicidad porque
comparten transacción. Cuesta una tabla, un worker y aceptar entrega *al menos una vez*, lo
que obliga a que los consumidores sean idempotentes.

**C: Cola externa con confirmación en dos fases.** Garantía equivalente a costa de un broker
con soporte de 2PC y de una complejidad operativa que no se justifica en un solo despliegue.

## Consecuencias

**Más fácil:** ningún evento se pierde por una caída; el reintento es natural; queda traza
en tabla de lo que se publicó.

**Más difícil:** la entrega es *al menos una vez*, así que **todo consumidor debe ser
idempotente**. Aparece latencia entre el cambio y su efecto: la interfaz no puede asumir que
las consecuencias son inmediatas. La tabla crece y necesita purga.

**A revisar:** cuando haya varios despliegues compitiendo por la misma tabla habrá que
revisar la toma del lote; y hará falta una política de purga antes de que el volumen sea un
problema.

## Acciones

1. [x] Tabla `outbox_messages` escrita en la transacción de negocio
2. [x] `OutboxDispatcherWorker`
3. [ ] Purga programada de mensajes despachados
4. [ ] Reintentos con retroceso exponencial y cola de fallidos
5. [ ] Documentar la idempotencia exigida a cada consumidor
