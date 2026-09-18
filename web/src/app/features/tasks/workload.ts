import type { TaskItem } from './task-create-modal.component';
import { dayFrom, dateOfDay, type Day } from './gantt';

/**
 * El reparto de horas por persona y semana.
 *
 * Igual que los cálculos del Gantt, van sueltos y como funciones puras: una suma mal repartida
 * no da error, sale un número plausible, y alguien decide una contratación con él.
 */

/** Estado en el que una tarea deja de contar como carga. Lo fija el backend. */
const COMPLETED_STATUS = 'Done';

/** Los días que se consideran laborables al repartir. Sábado y domingo no cuentan. */
function isWorkday(day: Day): boolean {
  const weekday = dateOfDay(day).getUTCDay();
  return weekday !== 0 && weekday !== 6;
}

/** El lunes de la semana a la que pertenece un día. Es la clave con la que se agrupa. */
export function mondayOf(day: Day): Day {
  const weekday = dateOfDay(day).getUTCDay();
  // getUTCDay: 0 es domingo. El lunes de la semana del domingo es seis días antes, no uno.
  const sinceMonday = weekday === 0 ? 6 : weekday - 1;
  return day - sinceMonday;
}

export interface WorkloadCell {
  /** El lunes de la semana. */
  week: Day;
  hours: number;
}

export interface WorkloadRow {
  /** El identificador del responsable, o `null` para las tareas sin asignar. */
  userId: string | null;
  weeks: WorkloadCell[];
  total: number;
}

export interface Workload {
  rows: WorkloadRow[];
  /** Los lunes de todas las semanas que aparecen, en orden. */
  weeks: Day[];
  /** La celda más alta de toda la tabla, para escalar las barras. */
  max: number;
  /**
   * Cuántas tareas se han quedado fuera por no tener fecha límite. No se reparten en ninguna
   * semana —no hay dónde— pero se cuentan aparte: una carga que esconde trabajo pendiente es
   * exactamente el error que hace decir «vamos bien» antes de un retraso.
   */
  withoutDueDate: number;
}

/**
 * Reparte las horas estimadas de cada tarea entre los días laborables que ocupa.
 *
 * **Repartir a partes iguales es una suposición, y se dice en pantalla.** No hay dato de cuánto
 * se dedica cada día, así que la alternativa sería cargarlo todo en el vencimiento, que
 * concentraría picos falsos, o no ofrecer la vista. Repartir es lo que hace cualquier
 * herramienta del ramo y lo que menos se aleja de la realidad.
 *
 * Una tarea sin fecha de inicio carga todo en su vencimiento: es lo único que se sabe de ella.
 * Las completadas no cuentan; ya no son carga futura.
 */
export function workloadOf(tasks: readonly TaskItem[]): Workload {
  const byUser = new Map<string | null, Map<Day, number>>();
  const weeks = new Set<Day>();
  let withoutDueDate = 0;

  for (const task of tasks) {
    if (task.status === COMPLETED_STATUS) continue;

    const due = dayFrom(task.dueDate);
    if (due === null) {
      withoutDueDate++;
      continue;
    }

    const start = dayFrom(task.startDate);
    const firstDay = start !== null && start <= due ? start : due;

    const days: Day[] = [];
    for (let day = firstDay; day <= due; day++) {
      if (isWorkday(day)) days.push(day);
    }

    // Un tramo entero en fin de semana no puede descartarse: son horas comprometidas. Se
    // cargan en el vencimiento, que es donde se notará que hay que hacerlas.
    if (!days.length) days.push(due);

    const perDay = (task.estimatedHours ?? 0) / days.length;

    // Una tarea puede tener varios responsables. Las horas se cuentan **enteras para cada uno**
    // y no divididas: dos personas en una tarea de ocho horas es que las dos tienen ocho horas
    // de trabajo por delante, no cuatro. Dividirlas haría que una tabla de carga dijera que hay
    // hueco donde no lo hay.
    const assignees: (string | null)[] = task.assignees?.length
      ? [...task.assignees]
      : [task.assigneeId || null];

    for (const user of assignees) {
      const mine = byUser.get(user) ?? new Map<Day, number>();
      byUser.set(user, mine);

      for (const day of days) {
        const week = mondayOf(day);
        weeks.add(week);
        mine.set(week, (mine.get(week) ?? 0) + perDay);
      }
    }
  }

  const sortedWeeks = [...weeks].sort((a, b) => a - b);
  let max = 0;

  const rows: WorkloadRow[] = [...byUser.entries()]
    .map(([userId, byWeek]) => {
      const cells = sortedWeeks.map(week => {
        const hours = round(byWeek.get(week) ?? 0);
        if (hours > max) max = hours;
        return { week, hours };
      });

      return {
        userId,
        weeks: cells,
        total: round(cells.reduce((sum, c) => sum + c.hours, 0)),
      };
    })
    // Quien más carga acumula, primero: es la fila que se busca al abrir la vista.
    .sort((a, b) => b.total - a.total);

  return { rows, weeks: sortedWeeks, max, withoutDueDate };
}

/** Media hora es la unidad más fina que tiene sentido enseñar en una tabla de carga. */
function round(hours: number): number {
  return Math.round(hours * 2) / 2;
}
