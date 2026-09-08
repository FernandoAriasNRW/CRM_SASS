import { cargaDe, lunesDe } from './carga';
import { diaDesde, fechaDelDia } from './gantt';
import type { TaskItem } from './task-create-modal.component';

/**
 * El reparto de carga.
 *
 * Se prueba a fondo porque una suma mal repartida **no da error**: sale un número plausible, y
 * con él alguien decide si contrata, si aplaza o si le pide más a quien ya no puede.
 */
describe('carga', () => {
  const tarea = (parcial: Partial<TaskItem>): TaskItem => ({
    id: 't', title: 'Tarea', description: '', status: 'To Do', priority: 'Normal',
    estimatedHours: 0, dueDate: '', projectId: 'p', assigneeId: '',
    ...parcial,
  } as TaskItem);

  describe('lunesDe', () => {
    it('un miércoles cae en su lunes', () => {
      // 2026-08-19 es miércoles; su lunes es el 17.
      expect(lunesDe(diaDesde('2026-08-19')!)).toBe(diaDesde('2026-08-17')!);
    });

    it('un lunes es su propio lunes', () => {
      expect(lunesDe(diaDesde('2026-08-17')!)).toBe(diaDesde('2026-08-17')!);
    });

    /** `getUTCDay` numera el domingo como 0: sin cuidado, el domingo salta a la semana siguiente. */
    it('un domingo cae en la semana que termina, no en la que empieza', () => {
      expect(lunesDe(diaDesde('2026-08-23')!)).toBe(diaDesde('2026-08-17')!);
    });
  });

  describe('cargaDe', () => {
    it('sin tareas no hay nada que repartir', () => {
      expect(cargaDe([]).filas).toEqual([]);
    });

    it('una tarea de un día carga todas sus horas en su semana', () => {
      const carga = cargaDe([tarea({
        assigneeId: 'ana', estimatedHours: 8, dueDate: '2026-08-19',
      })]);

      expect(carga.filas.length).toBe(1);
      expect(carga.filas[0].personaId).toBe('ana');
      expect(carga.filas[0].total).toBe(8);
    });

    it('reparte entre los días laborables que ocupa', () => {
      // Del martes 18 al jueves 20: tres días laborables, ocho horas.
      const carga = cargaDe([tarea({
        assigneeId: 'ana', estimatedHours: 9, startDate: '2026-08-18', dueDate: '2026-08-20',
      })]);

      expect(carga.filas[0].total).toBe(9);
      expect(carga.filas[0].semanas.length).toBe(1);
    });

    it('una tarea a caballo de dos semanas reparte entre las dos', () => {
      // Del jueves 20 al martes 25.
      const carga = cargaDe([tarea({
        assigneeId: 'ana', estimatedHours: 8, startDate: '2026-08-20', dueDate: '2026-08-25',
      })]);

      expect(carga.semanas.length).toBe(2);
      expect(carga.filas[0].semanas.every(c => c.horas > 0)).toBeTrue();
      expect(carga.filas[0].total).toBe(8);
    });

    /** El fin de semana no es tiempo de trabajo, así que no diluye la carga de los días útiles. */
    it('los fines de semana no reciben horas', () => {
      // Del viernes 21 al lunes 24: dos días laborables, no cuatro.
      const carga = cargaDe([tarea({
        assigneeId: 'ana', estimatedHours: 10, startDate: '2026-08-21', dueDate: '2026-08-24',
      })]);

      const semanaDelViernes = carga.filas[0].semanas.find(c => c.semana === lunesDe(diaDesde('2026-08-21')!))!;

      expect(semanaDelViernes.horas).toBe(5);
    });

    it('una tarea que cae entera en fin de semana carga en su vencimiento', () => {
      const carga = cargaDe([tarea({
        assigneeId: 'ana', estimatedHours: 4, startDate: '2026-08-22', dueDate: '2026-08-23',
      })]);

      expect(carga.filas[0].total).toBe(4);
    });

    it('las completadas no cuentan: ya no son carga futura', () => {
      const carga = cargaDe([
        tarea({ assigneeId: 'ana', estimatedHours: 8, dueDate: '2026-08-19', status: 'Done' }),
        tarea({ assigneeId: 'ana', estimatedHours: 3, dueDate: '2026-08-19' }),
      ]);

      expect(carga.filas[0].total).toBe(3);
    });

    /**
     * Esconder el trabajo sin fecha es el error que hace decir «vamos bien» justo antes de un
     * retraso. No se reparte —no hay dónde— pero se cuenta y se dice.
     */
    it('las que no tienen fecha límite se cuentan aparte', () => {
      const carga = cargaDe([
        tarea({ assigneeId: 'ana', estimatedHours: 8, dueDate: '' }),
        tarea({ assigneeId: 'ana', estimatedHours: 3, dueDate: '2026-08-19' }),
      ]);

      expect(carga.sinFecha).toBe(1);
      expect(carga.filas[0].total).toBe(3);
    });

    it('las que no tienen responsable van a su propia fila', () => {
      const carga = cargaDe([tarea({ assigneeId: '', estimatedHours: 5, dueDate: '2026-08-19' })]);

      expect(carga.filas[0].personaId).toBeNull();
    });

    /**
     * Dos personas en una tarea de ocho horas es que las dos tienen ocho horas por delante, no
     * cuatro. Dividirlas haría que la tabla dijera que hay hueco donde no lo hay.
     */
    it('una tarea con varios responsables cuenta entera para cada uno', () => {
      const carga = cargaDe([tarea({
        assigneeId: 'ana', assignees: ['ana', 'luis'], estimatedHours: 8, dueDate: '2026-08-19',
      })]);

      expect(carga.filas.length).toBe(2);
      expect(carga.filas.every(f => f.total === 8)).toBeTrue();
    });

    it('ordena por quien más acumula, que es la fila que se busca', () => {
      const carga = cargaDe([
        tarea({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        tarea({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-19' }),
      ]);

      expect(carga.filas.map(f => f.personaId)).toEqual(['luis', 'ana']);
    });

    it('el máximo es la celda más alta, para poder escalar las barras', () => {
      const carga = cargaDe([
        tarea({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        tarea({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-19' }),
      ]);

      expect(carga.maximo).toBe(9);
    });

    it('todas las personas tienen una celda por semana, aunque no trabajen esa semana', () => {
      const carga = cargaDe([
        tarea({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        tarea({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-26' }),
      ]);

      expect(carga.semanas.length).toBe(2);
      expect(carga.filas.every(f => f.semanas.length === 2)).toBeTrue();
    });

    it('las semanas salen en orden', () => {
      const carga = cargaDe([
        tarea({ id: 'a', assigneeId: 'ana', estimatedHours: 1, dueDate: '2026-09-02' }),
        tarea({ id: 'b', assigneeId: 'ana', estimatedHours: 1, dueDate: '2026-08-19' }),
      ]);

      expect(carga.semanas).toEqual([...carga.semanas].sort((x, y) => x - y));
      expect(fechaDelDia(carga.semanas[0]).getUTCMonth()).toBe(7);
    });

    it('una tarea sin horas estimadas no inventa carga', () => {
      const carga = cargaDe([tarea({ assigneeId: 'ana', estimatedHours: 0, dueDate: '2026-08-19' })]);

      expect(carga.filas[0].total).toBe(0);
      expect(carga.maximo).toBe(0);
    });
  });
});
