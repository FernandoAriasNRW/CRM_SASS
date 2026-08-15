import type { TaskItem } from './task-create-modal.component';
import { diaDesde, fechaDelDia, type Dia } from './gantt';

/**
 * El reparto de horas por persona y semana.
 *
 * Igual que los cálculos del Gantt, van sueltos y como funciones puras: una suma mal repartida
 * no da error, sale un número plausible, y alguien decide una contratación con él.
 */

/** Estado en el que una tarea deja de contar como carga. Lo fija el backend. */
const ESTADO_COMPLETADO = 'Done';

/** Los días que se consideran laborables al repartir. Sábado y domingo no cuentan. */
function esLaborable(dia: Dia): boolean {
  const diaDeLaSemana = fechaDelDia(dia).getUTCDay();
  return diaDeLaSemana !== 0 && diaDeLaSemana !== 6;
}

/** El lunes de la semana a la que pertenece un día. Es la clave con la que se agrupa. */
export function lunesDe(dia: Dia): Dia {
  const diaDeLaSemana = fechaDelDia(dia).getUTCDay();
  // getUTCDay: 0 es domingo. El lunes de la semana del domingo es seis días antes, no uno.
  const desdeElLunes = diaDeLaSemana === 0 ? 6 : diaDeLaSemana - 1;
  return dia - desdeElLunes;
}

export interface CeldaDeCarga {
  /** El lunes de la semana. */
  semana: Dia;
  horas: number;
}

export interface FilaDeCarga {
  /** El identificador del responsable, o `null` para las tareas sin asignar. */
  personaId: string | null;
  semanas: CeldaDeCarga[];
  total: number;
}

export interface Carga {
  filas: FilaDeCarga[];
  /** Los lunes de todas las semanas que aparecen, en orden. */
  semanas: Dia[];
  /** La celda más alta de toda la tabla, para escalar las barras. */
  maximo: number;
  /**
   * Cuántas tareas se han quedado fuera por no tener fecha límite. No se reparten en ninguna
   * semana —no hay dónde— pero se cuentan aparte: una carga que esconde trabajo pendiente es
   * exactamente el error que hace decir «vamos bien» antes de un retraso.
   */
  sinFecha: number;
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
export function cargaDe(tareas: readonly TaskItem[]): Carga {
  const porPersona = new Map<string | null, Map<Dia, number>>();
  const semanas = new Set<Dia>();
  let sinFecha = 0;

  for (const tarea of tareas) {
    if (tarea.status === ESTADO_COMPLETADO) continue;

    const vence = diaDesde(tarea.dueDate);
    if (vence === null) {
      sinFecha++;
      continue;
    }

    const empieza = diaDesde(tarea.startDate);
    const inicio = empieza !== null && empieza <= vence ? empieza : vence;

    const dias: Dia[] = [];
    for (let dia = inicio; dia <= vence; dia++) {
      if (esLaborable(dia)) dias.push(dia);
    }

    // Un tramo entero en fin de semana no puede descartarse: son horas comprometidas. Se
    // cargan en el vencimiento, que es donde se notará que hay que hacerlas.
    if (!dias.length) dias.push(vence);

    const porDia = (tarea.estimatedHours ?? 0) / dias.length;

    // Una tarea puede tener varios responsables. Las horas se cuentan **enteras para cada uno**
    // y no divididas: dos personas en una tarea de ocho horas es que las dos tienen ocho horas
    // de trabajo por delante, no cuatro. Dividirlas haría que una tabla de carga dijera que hay
    // hueco donde no lo hay.
    const responsables: (string | null)[] = tarea.assignees?.length
      ? [...tarea.assignees]
      : [tarea.assigneeId || null];

    for (const persona of responsables) {
      const suyas = porPersona.get(persona) ?? new Map<Dia, number>();
      porPersona.set(persona, suyas);

      for (const dia of dias) {
        const semana = lunesDe(dia);
        semanas.add(semana);
        suyas.set(semana, (suyas.get(semana) ?? 0) + porDia);
      }
    }
  }

  const semanasOrdenadas = [...semanas].sort((a, b) => a - b);
  let maximo = 0;

  const filas: FilaDeCarga[] = [...porPersona.entries()]
    .map(([personaId, porSemana]) => {
      const celdas = semanasOrdenadas.map(semana => {
        const horas = redondear(porSemana.get(semana) ?? 0);
        if (horas > maximo) maximo = horas;
        return { semana, horas };
      });

      return {
        personaId,
        semanas: celdas,
        total: redondear(celdas.reduce((suma, c) => suma + c.horas, 0)),
      };
    })
    // Quien más carga acumula, primero: es la fila que se busca al abrir la vista.
    .sort((a, b) => b.total - a.total);

  return { filas, semanas: semanasOrdenadas, maximo, sinFecha };
}

/** Media hora es la unidad más fina que tiene sentido enseñar en una tabla de carga. */
function redondear(horas: number): number {
  return Math.round(horas * 2) / 2;
}
