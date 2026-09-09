/**
 * Los estados de un proyecto: la clave del servidor y el nombre que se lee.
 *
 * Mismo motivo que en tareas y tickets: la clave está en inglés porque la define el servidor
 * —«Planned», «On Hold»— y se enseñaba tal cual, así que en la versión española salían esas dos
 * palabras sueltas en medio de todo lo demás.
 *
 * La clave no se traduce nunca: es lo que viaja en `?status=` y en el `PATCH`. El nombre sí.
 */
export interface EstadoDeProyecto {
  readonly clave: string;
  readonly etiqueta: string;
}

export const ESTADOS_DE_PROYECTO: readonly EstadoDeProyecto[] = [
  { clave: 'Planned', etiqueta: $localize`Planificado` },
  { clave: 'In Progress', etiqueta: $localize`En progreso` },
  { clave: 'On Hold', etiqueta: $localize`En pausa` },
  { clave: 'Done', etiqueta: $localize`Completado` }
];

/** El nombre legible, o la clave si llega una desconocida: un hueco no diría nada. */
export function nombreDelEstadoDeProyecto(estado: string): string {
  return ESTADOS_DE_PROYECTO.find(e => e.clave === estado)?.etiqueta ?? estado;
}
