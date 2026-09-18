import type { TaskItem } from './task-create-modal.component';

/**
 * Los cálculos del diagrama de Gantt, sin Angular ni DOM.
 *
 * Están aquí sueltos y no dentro del componente porque equivocarse en una fecha sale caro y en
 * silencio: una barra corrida un día no da error, sólo miente. Como funciones puras se pueden
 * probar exhaustivamente —cambios de mes, años bisiestos, husos horarios— sin montar nada.
 */

/** Un día expresado como número de días desde el 1 de enero de 1970. */
export type Day = number;

const MILLISECONDS_PER_DAY = 86_400_000;

/**
 * Convierte una fecha del servidor en número de día.
 *
 * **Se toman los diez primeros caracteres y se construye en UTC.** El servidor manda
 * `2026-08-15` o `2026-08-15T00:00:00`, y `new Date('2026-08-15')` se interpreta como medianoche
 * UTC: en cualquier huso al oeste de Greenwich, pedirle el día local devuelve el 14. Ese error
 * de un día no se ve al programarlo y sale en producción sólo para media Europa y toda América.
 */
export function dayFrom(date: string | null | undefined): Day | null {
  if (!date) return null;

  const parts = date.slice(0, 10).split('-');
  if (parts.length !== 3) return null;

  const [year, month, day] = parts.map(Number);
  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) return null;

  const timestamp = Date.UTC(year, month - 1, day);
  if (Number.isNaN(timestamp)) return null;

  return Math.floor(timestamp / MILLISECONDS_PER_DAY);
}

/** El día de hoy en la misma escala, tomado del reloj local y no del UTC. */
export function todayAsDay(now = new Date()): Day {
  return Math.floor(
    Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()) / MILLISECONDS_PER_DAY);
}

/** Vuelve a una fecha para poder pintar la escala. */
export function dateOfDay(day: Day): Date {
  return new Date(day * MILLISECONDS_PER_DAY);
}

export interface Range {
  firstDay: Day;
  lastDay: Day;
  /** Cuántos días ocupa el rango, contando los dos extremos. */
  days: number;
}

/**
 * El tramo de calendario que hay que dibujar.
 *
 * Incluye siempre el día de hoy: un diagrama que empieza el mes que viene no deja ver dónde
 * está uno. Devuelve `null` si no hay ninguna tarea con fechas, y entonces no hay nada que
 * pintar —mejor eso que un eje vacío que parece un fallo—.
 */
export function rangeOf(tasks: readonly TaskItem[], today: Day = todayAsDay()): Range | null {
  const days: Day[] = [];

  for (const task of tasks) {
    const due = dayFrom(task.dueDate);
    const start = dayFrom(task.startDate);

    if (due !== null) days.push(due);
    if (start !== null) days.push(start);
  }

  if (!days.length) return null;

  days.push(today);

  const firstDay = Math.min(...days);
  const lastDay = Math.max(...days);

  return { firstDay, lastDay, days: lastDay - firstDay + 1 };
}

export interface Bar {
  task: TaskItem;
  /** Cuántos días desde el principio del rango empieza la barra. */
  offset: number;
  /** Cuántos días ocupa. Nunca menos de uno: una barra de cero días no se vería. */
  duration: number;
  /**
   * Una tarea sin fecha de inicio. Se dibuja como un hito en su vencimiento en lugar de
   * inventarle un principio, que es lo que haría cualquier duración por defecto.
   */
  isMilestone: boolean;
  /** Si algo la bloquea. Lo cuenta el servidor en la propia consulta de tareas. */
  isBlocked: boolean;
}

/**
 * Coloca cada tarea en el rango.
 *
 * Las tareas sin vencimiento se quedan fuera: no hay dónde ponerlas, y colocarlas al principio
 * o al final sería afirmar algo que nadie ha dicho.
 */
export function barsOf(tasks: readonly TaskItem[], range: Range): Bar[] {
  const bars: Bar[] = [];

  for (const task of tasks) {
    const due = dayFrom(task.dueDate);
    if (due === null) continue;

    const start = dayFrom(task.startDate);

    // Un inicio posterior al vencimiento lo rechaza el dominio, pero un dato viejo o una
    // respuesta a medias no pueden dejar la pantalla con una barra de longitud negativa.
    const validStart = start !== null && start <= due ? start : null;

    bars.push({
      task,
      offset: (validStart ?? due) - range.firstDay,
      duration: validStart === null ? 1 : due - validStart + 1,
      isMilestone: validStart === null,
      isBlocked: (task.blockedByCount ?? 0) > 0,
    });
  }

  return bars;
}

/** Las marcas del eje: el primer día de cada mes que toca el rango, y el primero del todo. */
export function axisTicks(range: Range): { day: Day; label: string }[] {
  const ticks: { day: Day; label: string }[] = [];

  for (let day = range.firstDay; day <= range.lastDay; day++) {
    const date = dateOfDay(day);

    if (day === range.firstDay || date.getUTCDate() === 1) {
      ticks.push({
        day,
        label: date.toLocaleDateString(undefined, {
          month: 'short', year: 'numeric', timeZone: 'UTC',
        }),
      });
    }
  }

  return ticks;
}

/** Una dependencia: `taskId` está bloqueada por `dependsOnTaskId`. */
export interface DependencyEdge {
  taskId: string;
  dependsOnTaskId: string;
}

/**
 * Una flecha ya resuelta a coordenadas de la rejilla, en «días» y «filas».
 *
 * Las coordenadas van en unidades del diagrama y no en píxeles: la plantilla multiplica por el
 * ancho de día y el alto de fila, así que cambiar el zoom no obliga a tocar estos cálculos.
 */
export interface Arrow {
  fromDay: number;
  fromRow: number;
  toDay: number;
  toRow: number;
  /**
   * La dependencia se incumple: la tarea que bloquea termina después de que empiece la
   * bloqueada. Es lo que un Gantt tiene que gritar, porque el plan es imposible tal cual está.
   */
  isViolated: boolean;
}

/**
 * Convierte las dependencias en flechas entre barras.
 *
 * Sólo se dibujan las que unen dos barras visibles: una flecha que sale del diagrama y no llega
 * a ninguna parte confunde más que la falta de flecha. Y sólo hacia adelante en el tiempo —lo
 * demás lo marca `incumplida`—, porque el sentido es «esto antes que esto otro».
 */
export function arrowsOf(
  edges: readonly DependencyEdge[],
  bars: readonly Bar[],
): Arrow[] {
  const byTask = new Map<string, { bar: Bar; row: number }>();
  bars.forEach((bar, row) => byTask.set(bar.task.id, { bar, row }));

  const arrows: Arrow[] = [];

  for (const edge of edges) {
    const isBlocked = byTask.get(edge.taskId);
    const blocker = byTask.get(edge.dependsOnTaskId);

    if (!isBlocked || !blocker) continue;

    const blockerEnd = blocker.bar.offset + blocker.bar.duration;
    const blockedStart = isBlocked.bar.offset;

    arrows.push({
      fromDay: blockerEnd,
      fromRow: blocker.row,
      toDay: blockedStart,
      toRow: isBlocked.row,
      isViolated: blockerEnd > blockedStart,
    });
  }

  return arrows;
}

/** Si un día cae en sábado o domingo, para sombrearlo. */
export function isWeekend(day: Day): boolean {
  const weekday = dateOfDay(day).getUTCDay();
  return weekday === 0 || weekday === 6;
}
