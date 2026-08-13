import type { TaskItem } from './task-create-modal.component';

/**
 * Los cálculos del diagrama de Gantt, sin Angular ni DOM.
 *
 * Están aquí sueltos y no dentro del componente porque equivocarse en una fecha sale caro y en
 * silencio: una barra corrida un día no da error, sólo miente. Como funciones puras se pueden
 * probar exhaustivamente —cambios de mes, años bisiestos, husos horarios— sin montar nada.
 */

/** Un día expresado como número de días desde el 1 de enero de 1970. */
export type Dia = number;

const MILISEGUNDOS_POR_DIA = 86_400_000;

/**
 * Convierte una fecha del servidor en número de día.
 *
 * **Se toman los diez primeros caracteres y se construye en UTC.** El servidor manda
 * `2026-08-15` o `2026-08-15T00:00:00`, y `new Date('2026-08-15')` se interpreta como medianoche
 * UTC: en cualquier huso al oeste de Greenwich, pedirle el día local devuelve el 14. Ese error
 * de un día no se ve al programarlo y sale en producción sólo para media Europa y toda América.
 */
export function diaDesde(fecha: string | null | undefined): Dia | null {
  if (!fecha) return null;

  const partes = fecha.slice(0, 10).split('-');
  if (partes.length !== 3) return null;

  const [anio, mes, dia] = partes.map(Number);
  if (!Number.isFinite(anio) || !Number.isFinite(mes) || !Number.isFinite(dia)) return null;

  const marca = Date.UTC(anio, mes - 1, dia);
  if (Number.isNaN(marca)) return null;

  return Math.floor(marca / MILISEGUNDOS_POR_DIA);
}

/** El día de hoy en la misma escala, tomado del reloj local y no del UTC. */
export function hoyComoDia(ahora = new Date()): Dia {
  return Math.floor(
    Date.UTC(ahora.getFullYear(), ahora.getMonth(), ahora.getDate()) / MILISEGUNDOS_POR_DIA);
}

/** Vuelve a una fecha para poder pintar la escala. */
export function fechaDelDia(dia: Dia): Date {
  return new Date(dia * MILISEGUNDOS_POR_DIA);
}

export interface Rango {
  primerDia: Dia;
  ultimoDia: Dia;
  /** Cuántos días ocupa el rango, contando los dos extremos. */
  dias: number;
}

/**
 * El tramo de calendario que hay que dibujar.
 *
 * Incluye siempre el día de hoy: un diagrama que empieza el mes que viene no deja ver dónde
 * está uno. Devuelve `null` si no hay ninguna tarea con fechas, y entonces no hay nada que
 * pintar —mejor eso que un eje vacío que parece un fallo—.
 */
export function rangoDe(tareas: readonly TaskItem[], hoy: Dia = hoyComoDia()): Rango | null {
  const dias: Dia[] = [];

  for (const tarea of tareas) {
    const vence = diaDesde(tarea.dueDate);
    const empieza = diaDesde(tarea.startDate);

    if (vence !== null) dias.push(vence);
    if (empieza !== null) dias.push(empieza);
  }

  if (!dias.length) return null;

  dias.push(hoy);

  const primerDia = Math.min(...dias);
  const ultimoDia = Math.max(...dias);

  return { primerDia, ultimoDia, dias: ultimoDia - primerDia + 1 };
}

export interface Barra {
  tarea: TaskItem;
  /** Cuántos días desde el principio del rango empieza la barra. */
  desplazamiento: number;
  /** Cuántos días ocupa. Nunca menos de uno: una barra de cero días no se vería. */
  duracion: number;
  /**
   * Una tarea sin fecha de inicio. Se dibuja como un hito en su vencimiento en lugar de
   * inventarle un principio, que es lo que haría cualquier duración por defecto.
   */
  esHito: boolean;
  /** Si algo la bloquea. Lo cuenta el servidor en la propia consulta de tareas. */
  bloqueada: boolean;
}

/**
 * Coloca cada tarea en el rango.
 *
 * Las tareas sin vencimiento se quedan fuera: no hay dónde ponerlas, y colocarlas al principio
 * o al final sería afirmar algo que nadie ha dicho.
 */
export function barrasDe(tareas: readonly TaskItem[], rango: Rango): Barra[] {
  const barras: Barra[] = [];

  for (const tarea of tareas) {
    const vence = diaDesde(tarea.dueDate);
    if (vence === null) continue;

    const empieza = diaDesde(tarea.startDate);

    // Un inicio posterior al vencimiento lo rechaza el dominio, pero un dato viejo o una
    // respuesta a medias no pueden dejar la pantalla con una barra de longitud negativa.
    const inicioValido = empieza !== null && empieza <= vence ? empieza : null;

    barras.push({
      tarea,
      desplazamiento: (inicioValido ?? vence) - rango.primerDia,
      duracion: inicioValido === null ? 1 : vence - inicioValido + 1,
      esHito: inicioValido === null,
      bloqueada: (tarea.blockedByCount ?? 0) > 0,
    });
  }

  return barras;
}

/** Las marcas del eje: el primer día de cada mes que toca el rango, y el primero del todo. */
export function marcasDelEje(rango: Rango): { dia: Dia; etiqueta: string }[] {
  const marcas: { dia: Dia; etiqueta: string }[] = [];

  for (let dia = rango.primerDia; dia <= rango.ultimoDia; dia++) {
    const fecha = fechaDelDia(dia);

    if (dia === rango.primerDia || fecha.getUTCDate() === 1) {
      marcas.push({
        dia,
        etiqueta: fecha.toLocaleDateString(undefined, {
          month: 'short', year: 'numeric', timeZone: 'UTC',
        }),
      });
    }
  }

  return marcas;
}

/** Si un día cae en sábado o domingo, para sombrearlo. */
export function esFinDeSemana(dia: Dia): boolean {
  const diaDeLaSemana = fechaDelDia(dia).getUTCDay();
  return diaDeLaSemana === 0 || diaDeLaSemana === 6;
}
