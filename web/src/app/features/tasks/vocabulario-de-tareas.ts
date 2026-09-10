import type { BadgeVariant } from '../../shared/ui/badge.component';

/**
 * Los estados de una tarea: la clave que viaja al servidor y el nombre que se lee.
 *
 * <b>Las claves son las del servidor y están en inglés</b> —«To Do», «In Progress»—, y hasta ahora
 * el cajón de detalle las enseñaba tal cual mientras el tablero pintaba «Por hacer» y «En
 * progreso». La misma tarea decía dos cosas distintas según dónde se mirara, y en la versión
 * inglesa de la aplicación las palabras traducidas convivían con estas sin traducir.
 *
 * La clave no se traduce nunca —es un dato— y el nombre sí. Tenerlos juntos es lo que evita que
 * alguien traduzca la clave por descuido y rompa el guardado.
 */
export interface EstadoDeTarea {
  readonly clave: string;
  readonly etiqueta: string;
  readonly badge: BadgeVariant;
}

export const ESTADOS_DE_TAREA: readonly EstadoDeTarea[] = [
  { clave: 'To Do', etiqueta: $localize`Por hacer`, badge: 'secondary' },
  { clave: 'In Progress', etiqueta: $localize`En progreso`, badge: 'default' },
  { clave: 'In Review', etiqueta: $localize`En revisión`, badge: 'warning' },
  { clave: 'Done', etiqueta: $localize`Completado`, badge: 'success' }
];

/** El nombre legible, o la clave si llega una que no se conoce: un hueco no diría nada. */
export function nombreDelEstadoDeTarea(estado: string): string {
  return ESTADOS_DE_TAREA.find(e => e.clave === estado)?.etiqueta ?? estado;
}

export function insigniaDelEstadoDeTarea(estado: string): BadgeVariant {
  return ESTADOS_DE_TAREA.find(e => e.clave === estado)?.badge ?? 'outline';
}
