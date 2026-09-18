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
export interface TaskStatusOption {
  readonly key: string;
  readonly label: string;
  readonly badge: BadgeVariant;
}

export const TASK_STATUSES: readonly TaskStatusOption[] = [
  { key: 'To Do', label: $localize`Por hacer`, badge: 'secondary' },
  { key: 'In Progress', label: $localize`En progreso`, badge: 'default' },
  { key: 'In Review', label: $localize`En revisión`, badge: 'warning' },
  { key: 'Done', label: $localize`Completado`, badge: 'success' }
];

/** El nombre legible, o la clave si llega una que no se conoce: un hueco no diría nada. */
export function taskStatusLabel(status: string): string {
  return TASK_STATUSES.find(e => e.key === status)?.label ?? status;
}

export function taskStatusBadge(status: string): BadgeVariant {
  return TASK_STATUSES.find(e => e.key === status)?.badge ?? 'outline';
}
