import { barrasDe, diaDesde, esFinDeSemana, fechaDelDia, flechasDe, hoyComoDia, marcasDelEje, rangoDe } from './gantt';
import type { TaskItem } from './task-create-modal.component';

/**
 * Los cálculos del Gantt.
 *
 * Se prueban aparte y a fondo porque una fecha corrida un día **no da ningún error**: pinta una
 * barra un poco desplazada y nadie lo nota hasta que alguien planifica según ella.
 */
describe('gantt', () => {
  const tarea = (parcial: Partial<TaskItem>): TaskItem => ({
    id: 't', title: 'Tarea', description: '', status: 'To Do', priority: 'Normal',
    estimatedHours: 0, dueDate: '', projectId: 'p', assigneeId: '',
    ...parcial,
  } as TaskItem);

  describe('diaDesde', () => {
    it('lee una fecha suelta', () => {
      expect(diaDesde('1970-01-01')).toBe(0);
      expect(diaDesde('1970-01-02')).toBe(1);
    });

    it('lee una fecha con hora, que es como la manda parte de la API', () => {
      expect(diaDesde('2026-08-15T00:00:00')).toBe(diaDesde('2026-08-15')!);
    });

    /**
     * `new Date('2026-08-15')` es medianoche UTC: al oeste de Greenwich, pedirle el día local
     * devuelve el 14. Ese error de un día no se ve al programarlo y sale sólo para media Europa
     * y toda América.
     */
    it('no se corre un día por el huso horario', () => {
      const dia = diaDesde('2026-08-15')!;

      expect(fechaDelDia(dia).getUTCDate()).toBe(15);
      expect(fechaDelDia(dia).getUTCMonth()).toBe(7);
    });

    it('cuenta bien un cambio de mes', () => {
      expect(diaDesde('2026-09-01')! - diaDesde('2026-08-31')!).toBe(1);
    });

    it('cuenta bien un año bisiesto', () => {
      expect(diaDesde('2028-03-01')! - diaDesde('2028-02-28')!).toBe(2);
    });

    it('un valor ausente o con mala pinta no revienta', () => {
      expect(diaDesde(null)).toBeNull();
      expect(diaDesde(undefined)).toBeNull();
      expect(diaDesde('')).toBeNull();
      expect(diaDesde('mañana')).toBeNull();
    });
  });

  describe('rangoDe', () => {
    const hoy = diaDesde('2026-08-13')!;

    it('sin tareas con fechas no hay nada que pintar', () => {
      expect(rangoDe([], hoy)).toBeNull();
      expect(rangoDe([tarea({ dueDate: '' })], hoy)).toBeNull();
    });

    it('abarca de la fecha más temprana a la más tardía', () => {
      const rango = rangoDe([
        tarea({ dueDate: '2026-08-20', startDate: '2026-08-18' }),
        tarea({ dueDate: '2026-08-25' }),
      ], hoy)!;

      expect(rango.primerDia).toBe(diaDesde('2026-08-13')!);
      expect(rango.ultimoDia).toBe(diaDesde('2026-08-25')!);
    });

    /** Un diagrama que empieza el mes que viene no deja ver dónde está uno. */
    it('siempre incluye el día de hoy, aunque todo esté en el futuro', () => {
      const rango = rangoDe([tarea({ dueDate: '2026-12-01' })], hoy)!;

      expect(rango.primerDia).toBe(hoy);
    });

    it('y también si todo está en el pasado', () => {
      const rango = rangoDe([tarea({ dueDate: '2026-01-05' })], hoy)!;

      expect(rango.ultimoDia).toBe(hoy);
    });

    it('cuenta los dos extremos', () => {
      const rango = rangoDe([tarea({ dueDate: '2026-08-13' })], hoy)!;

      expect(rango.dias).toBe(1);
    });
  });

  describe('barrasDe', () => {
    const hoy = diaDesde('2026-08-13')!;

    it('una tarea con inicio y vencimiento ocupa los días entre ambos, incluidos', () => {
      const t = tarea({ dueDate: '2026-08-20', startDate: '2026-08-18' });
      const rango = rangoDe([t], hoy)!;

      const [barra] = barrasDe([t], rango);

      expect(barra.duracion).toBe(3);
      expect(barra.esHito).toBeFalse();
      expect(barra.desplazamiento).toBe(diaDesde('2026-08-18')! - rango.primerDia);
    });

    it('empezar y vencer el mismo día dura un día, no cero', () => {
      const t = tarea({ dueDate: '2026-08-20', startDate: '2026-08-20' });
      const rango = rangoDe([t], hoy)!;

      expect(barrasDe([t], rango)[0].duracion).toBe(1);
    });

    /** Inventarle un principio es exactamente lo que se decidió no hacer. */
    it('sin fecha de inicio sale un hito en el vencimiento', () => {
      const t = tarea({ dueDate: '2026-08-20' });
      const rango = rangoDe([t], hoy)!;

      const [barra] = barrasDe([t], rango);

      expect(barra.esHito).toBeTrue();
      expect(barra.duracion).toBe(1);
      expect(barra.desplazamiento).toBe(diaDesde('2026-08-20')! - rango.primerDia);
    });

    it('sin vencimiento no se pinta: no hay dónde ponerla', () => {
      const conFecha = tarea({ id: 'a', dueDate: '2026-08-20' });
      const sinFecha = tarea({ id: 'b', dueDate: '' });
      const rango = rangoDe([conFecha, sinFecha], hoy)!;

      expect(barrasDe([conFecha, sinFecha], rango).map(b => b.tarea.id)).toEqual(['a']);
    });

    /**
     * El dominio rechaza un inicio posterior al vencimiento, pero un dato viejo no puede dejar
     * la pantalla con una barra de longitud negativa.
     */
    it('un inicio posterior al vencimiento se trata como si no lo hubiera', () => {
      const t = tarea({ dueDate: '2026-08-20', startDate: '2026-09-30' });
      const rango = rangoDe([t], hoy)!;

      const [barra] = barrasDe([t], rango);

      expect(barra.esHito).toBeTrue();
      expect(barra.duracion).toBe(1);
    });

    it('marca las bloqueadas con lo que ya cuenta el servidor', () => {
      const bloqueada = tarea({ id: 'a', dueDate: '2026-08-20', blockedByCount: 2 });
      const libre = tarea({ id: 'b', dueDate: '2026-08-21', blockedByCount: 0 });
      const rango = rangoDe([bloqueada, libre], hoy)!;

      const barras = barrasDe([bloqueada, libre], rango);

      expect(barras[0].bloqueada).toBeTrue();
      expect(barras[1].bloqueada).toBeFalse();
    });
  });

  describe('marcasDelEje', () => {
    it('marca el principio y cada primero de mes', () => {
      const rango = rangoDe([
        tarea({ dueDate: '2026-10-05', startDate: '2026-08-28' }),
      ], diaDesde('2026-08-28')!)!;

      const dias = marcasDelEje(rango).map(m => fechaDelDia(m.dia).getUTCDate());

      expect(dias).toEqual([28, 1, 1]);
    });
  });

  describe('esFinDeSemana', () => {
    it('reconoce sábado y domingo', () => {
      // 2026-08-15 es sábado y el 16, domingo.
      expect(esFinDeSemana(diaDesde('2026-08-15')!)).toBeTrue();
      expect(esFinDeSemana(diaDesde('2026-08-16')!)).toBeTrue();
      expect(esFinDeSemana(diaDesde('2026-08-17')!)).toBeFalse();
    });
  });

  describe('flechasDe', () => {
    const hoy = diaDesde('2026-08-10')!;

    /** `a` empieza el 18 y vence el 20; `b` empieza el 22 y vence el 24. */
    const primera = tarea({ id: 'a', dueDate: '2026-08-20', startDate: '2026-08-18' });
    const segunda = tarea({ id: 'b', dueDate: '2026-08-24', startDate: '2026-08-22' });

    function barras(tareas = [primera, segunda]) {
      return barrasDe(tareas, rangoDe(tareas, hoy)!);
    }

    it('une la barra que bloquea con la bloqueada', () => {
      const [flecha] = flechasDe([{ taskId: 'b', dependsOnTaskId: 'a' }], barras());

      expect(flecha.desdeFila).toBe(0);
      expect(flecha.hastaFila).toBe(1);
      expect(flecha.incumplida).toBeFalse();
    });

    /**
     * Es lo que un Gantt tiene que gritar: el plan es imposible tal cual está, porque lo que
     * bloquea todavía no ha terminado cuando lo bloqueado ya tendría que haber empezado.
     */
    it('marca como incumplida la que va hacia atrás en el tiempo', () => {
      const [flecha] = flechasDe([{ taskId: 'a', dependsOnTaskId: 'b' }], barras());

      expect(flecha.incumplida).toBeTrue();
    });

    it('encadenar justo el día siguiente no se considera incumplido', () => {
      const antes = tarea({ id: 'a', dueDate: '2026-08-20', startDate: '2026-08-18' });
      const despues = tarea({ id: 'b', dueDate: '2026-08-25', startDate: '2026-08-21' });

      const [flecha] = flechasDe(
        [{ taskId: 'b', dependsOnTaskId: 'a' }], barras([antes, despues]));

      expect(flecha.incumplida).toBeFalse();
    });

    /** Una flecha que sale del diagrama y no llega a nada confunde más que no dibujarla. */
    it('descarta las que apuntan a una tarea que no se está pintando', () => {
      const flechas = flechasDe([
        { taskId: 'b', dependsOnTaskId: 'fantasma' },
        { taskId: 'fantasma', dependsOnTaskId: 'a' },
      ], barras());

      expect(flechas).toEqual([]);
    });

    it('sin dependencias no hay flechas', () => {
      expect(flechasDe([], barras())).toEqual([]);
    });

    it('una tarea puede tener varias flechas', () => {
      const tercera = tarea({ id: 'c', dueDate: '2026-08-28', startDate: '2026-08-26' });

      const flechas = flechasDe([
        { taskId: 'c', dependsOnTaskId: 'a' },
        { taskId: 'c', dependsOnTaskId: 'b' },
      ], barras([primera, segunda, tercera]));

      expect(flechas.length).toBe(2);
      expect(flechas.map(f => f.hastaFila)).toEqual([2, 2]);
    });
  });

  describe('hoyComoDia', () => {
    it('usa el día local y no el UTC, que es el que ve quien mira la pantalla', () => {
      const nochevieja = new Date(2026, 11, 31, 23, 30);

      expect(hoyComoDia(nochevieja)).toBe(diaDesde('2026-12-31')!);
    });
  });
});
