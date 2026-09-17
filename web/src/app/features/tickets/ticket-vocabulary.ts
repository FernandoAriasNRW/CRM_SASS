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
export interface TicketStatusOption {
  /** La clave exacta que viaja al servidor. */
  readonly key: string;
  readonly label: string;
  readonly badge: BadgeVariant;
}

export const TICKET_STATUSES: readonly TicketStatusOption[] = [
  { key: 'Open', label: $localize`Abierto`, badge: 'secondary' },
  { key: 'InProgress', label: $localize`En progreso`, badge: 'default' },
  { key: 'PendingInfo', label: $localize`Esperando información`, badge: 'warning' },
  { key: 'Resolved', label: $localize`Resuelto`, badge: 'success' },
  { key: 'Closed', label: $localize`Cerrado`, badge: 'outline' }
];

export interface TicketPriorityOption {
  readonly key: string;
  readonly label: string;
}

/**
 * De mayor a menor.
 *
 * «Critical» es como se llama en el servidor. El alta ofrecía «Urgent», que no existe: el
 * comando lo recibía, no casaba con ningún valor y el ticket se creaba con la prioridad por
 * defecto sin decir nada.
 */
export const TICKET_PRIORITIES: readonly TicketPriorityOption[] = [
  { key: 'Critical', label: $localize`Crítica` },
  { key: 'High', label: $localize`Alta` },
  { key: 'Medium', label: $localize`Media` },
  { key: 'Low', label: $localize`Baja` }
];

/** Cómo se pinta la insignia de un estado. Desconocido cae en «outline», no en un hueco. */
export function statusBadge(status: string): BadgeVariant {
  return TICKET_STATUSES.find(s => s.key === status)?.badge ?? 'outline';
}

/**
 * El nombre legible de un estado.
 *
 * Si llega uno que no se conoce se devuelve tal cual en vez de quedarse en blanco: enseñar la
 * clave cruda es feo, pero dice qué pasa; un hueco no.
 */
export function statusLabel(status: string): string {
  return TICKET_STATUSES.find(s => s.key === status)?.label ?? status;
}

export function priorityLabel(priority: string): string {
  return TICKET_PRIORITIES.find(p => p.key === priority)?.label ?? priority;
}
