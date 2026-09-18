/**
 * Los estados de un proyecto: la clave del servidor y el nombre que se lee.
 *
 * Mismo motivo que en tareas y tickets: la clave está en inglés porque la define el servidor
 * —«Planned», «On Hold»— y se enseñaba tal cual, así que en la versión española salían esas dos
 * palabras sueltas en medio de todo lo demás.
 *
 * La clave no se traduce nunca: es lo que viaja en `?status=` y en el `PATCH`. El nombre sí.
 */
export interface ProjectStatusOption {
  readonly key: string;
  readonly label: string;
}

export const PROJECT_STATUSES: readonly ProjectStatusOption[] = [
  { key: 'Planned', label: $localize`Planificado` },
  { key: 'In Progress', label: $localize`En progreso` },
  { key: 'On Hold', label: $localize`En pausa` },
  { key: 'Done', label: $localize`Completado` }
];

/** El nombre legible, o la clave si llega una desconocida: un hueco no diría nada. */
export function projectStatusLabel(status: string): string {
  return PROJECT_STATUSES.find(e => e.key === status)?.label ?? status;
}
