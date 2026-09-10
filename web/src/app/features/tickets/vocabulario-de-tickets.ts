import type { BadgeVariant } from '../../shared/ui/badge.component';

/**
 * Los estados y las prioridades de un ticket, <b>tal y como los nombra el servidor</b>.
 *
 * <b>Llegó a haber tres listas distintas y ninguna coincidía con la de verdad.</b> El tablero
 * usaba <code>Open / InProgress / Resolved / Closed</code>, el cajón de detalle
 * <code>New / In Progress / Resolved / Closed</code> y el alta
 * <code>Low / Medium / High / Urgent</code>. El servidor
 * (<code>TicketStatus</code>, <code>TicketPriority</code>) dice otra cosa: hay
 * <code>PendingInfo</code>, no hay <code>New</code>, y la prioridad más alta se llama
 * <code>Critical</code>, no <code>Urgent</code>.
 *
 * Se notaba: al abrir un ticket, los desplegables de estado y prioridad salían <b>en blanco</b>
 * —«Open» no estaba entre las opciones, y «Low» tampoco porque las claves iban en minúscula—
 * mientras la etiqueta de arriba decía «Open» y «Low». Y un ticket en <code>PendingInfo</code>
 * <b>no aparecía en ninguna columna del tablero</b>: no se perdía, pero no se veía.
 *
 * Por eso hay un solo sitio. Cuando el servidor añada un estado, se añade aquí y las tres
 * pantallas se enteran a la vez.
 */
export interface EstadoDeTicket {
  /** La clave exacta que viaja al servidor. */
  readonly clave: string;
  readonly etiqueta: string;
  readonly badge: BadgeVariant;
}

export const ESTADOS_DE_TICKET: readonly EstadoDeTicket[] = [
  { clave: 'Open', etiqueta: $localize`Abierto`, badge: 'secondary' },
  { clave: 'InProgress', etiqueta: $localize`En progreso`, badge: 'default' },
  { clave: 'PendingInfo', etiqueta: $localize`Esperando información`, badge: 'warning' },
  { clave: 'Resolved', etiqueta: $localize`Resuelto`, badge: 'success' },
  { clave: 'Closed', etiqueta: $localize`Cerrado`, badge: 'outline' }
];

export interface PrioridadDeTicket {
  readonly clave: string;
  readonly etiqueta: string;
}

/**
 * De mayor a menor.
 *
 * «Critical» es como se llama en el servidor. El alta ofrecía «Urgent», que no existe: el
 * comando lo recibía, no casaba con ningún valor y el ticket se creaba con la prioridad por
 * defecto sin decir nada.
 */
export const PRIORIDADES_DE_TICKET: readonly PrioridadDeTicket[] = [
  { clave: 'Critical', etiqueta: $localize`Crítica` },
  { clave: 'High', etiqueta: $localize`Alta` },
  { clave: 'Medium', etiqueta: $localize`Media` },
  { clave: 'Low', etiqueta: $localize`Baja` }
];

/** Cómo se pinta la insignia de un estado. Desconocido cae en «outline», no en un hueco. */
export function insigniaDelEstado(estado: string): BadgeVariant {
  return ESTADOS_DE_TICKET.find(e => e.clave === estado)?.badge ?? 'outline';
}

/**
 * El nombre legible de un estado.
 *
 * Si llega uno que no se conoce se devuelve tal cual en vez de quedarse en blanco: enseñar la
 * clave cruda es feo, pero dice qué pasa; un hueco no.
 */
export function nombreDelEstado(estado: string): string {
  return ESTADOS_DE_TICKET.find(e => e.clave === estado)?.etiqueta ?? estado;
}

export function nombreDeLaPrioridad(prioridad: string): string {
  return PRIORIDADES_DE_TICKET.find(p => p.clave === prioridad)?.etiqueta ?? prioridad;
}
