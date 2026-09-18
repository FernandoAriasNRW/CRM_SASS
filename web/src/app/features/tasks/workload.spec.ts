import { workloadOf, mondayOf } from './workload';
import { dayFrom, dateOfDay } from './gantt';
import type { TaskItem } from './task-create-modal.component';

/**
 * El reparto de carga.
 *
 * Se prueba a fondo porque una suma mal repartida **no da error**: sale un número plausible, y
 * con él alguien decide si contrata, si aplaza o si le pide más a quien ya no puede.
 */
describe('carga', () => {
  const task = (overrides: Partial<TaskItem>): TaskItem => ({
    id: 't', title: 'Tarea', description: '', status: 'To Do', priority: 'Normal',
    estimatedHours: 0, dueDate: '', projectId: 'p', assigneeId: '',
    ...overrides,
  } as TaskItem);

  describe('lunesDe', () => {
    it('un miércoles cae en su lunes', () => {
      // 2026-08-19 es miércoles; su lunes es el 17.
      expect(mondayOf(dayFrom('2026-08-19')!)).toBe(dayFrom('2026-08-17')!);
    });

    it('un lunes es su propio lunes', () => {
      expect(mondayOf(dayFrom('2026-08-17')!)).toBe(dayFrom('2026-08-17')!);
    });

    /** `getUTCDay` numera el domingo como 0: sin cuidado, el domingo salta a la semana siguiente. */
    it('un domingo cae en la semana que termina, no en la que empieza', () => {
      expect(mondayOf(dayFrom('2026-08-23')!)).toBe(dayFrom('2026-08-17')!);
    });
  });

  describe('cargaDe', () => {
    it('sin tareas no hay nada que repartir', () => {
      expect(workloadOf([]).rows).toEqual([]);
    });

    it('una tarea de un día carga todas sus horas en su semana', () => {
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 8, dueDate: '2026-08-19',
      })]);

      expect(workload.rows.length).toBe(1);
      expect(workload.rows[0].userId).toBe('ana');
      expect(workload.rows[0].total).toBe(8);
    });

    it('reparte entre los días laborables que ocupa', () => {
      // Del martes 18 al jueves 20: tres días laborables, ocho horas.
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 9, startDate: '2026-08-18', dueDate: '2026-08-20',
      })]);

      expect(workload.rows[0].total).toBe(9);
      expect(workload.rows[0].weeks.length).toBe(1);
    });

    it('una tarea a caballo de dos semanas reparte entre las dos', () => {
      // Del jueves 20 al martes 25.
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 8, startDate: '2026-08-20', dueDate: '2026-08-25',
      })]);

      expect(workload.weeks.length).toBe(2);
      expect(workload.rows[0].weeks.every(c => c.hours > 0)).toBeTrue();
      expect(workload.rows[0].total).toBe(8);
    });

    /** El fin de semana no es tiempo de trabajo, así que no diluye la carga de los días útiles. */
    it('los fines de semana no reciben horas', () => {
      // Del viernes 21 al lunes 24: dos días laborables, no cuatro.
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 10, startDate: '2026-08-21', dueDate: '2026-08-24',
      })]);

      const fridayWeek = workload.rows[0].weeks.find(c => c.week === mondayOf(dayFrom('2026-08-21')!))!;

      expect(fridayWeek.hours).toBe(5);
    });

    it('una tarea que cae entera en fin de semana carga en su vencimiento', () => {
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 4, startDate: '2026-08-22', dueDate: '2026-08-23',
      })]);

      expect(workload.rows[0].total).toBe(4);
    });

    it('las completadas no cuentan: ya no son carga futura', () => {
      const workload = workloadOf([
        task({ assigneeId: 'ana', estimatedHours: 8, dueDate: '2026-08-19', status: 'Done' }),
        task({ assigneeId: 'ana', estimatedHours: 3, dueDate: '2026-08-19' }),
      ]);

      expect(workload.rows[0].total).toBe(3);
    });

    /**
     * Esconder el trabajo sin fecha es el error que hace decir «vamos bien» justo antes de un
     * retraso. No se reparte —no hay dónde— pero se cuenta y se dice.
     */
    it('las que no tienen fecha límite se cuentan aparte', () => {
      const workload = workloadOf([
        task({ assigneeId: 'ana', estimatedHours: 8, dueDate: '' }),
        task({ assigneeId: 'ana', estimatedHours: 3, dueDate: '2026-08-19' }),
      ]);

      expect(workload.withoutDueDate).toBe(1);
      expect(workload.rows[0].total).toBe(3);
    });

    it('las que no tienen responsable van a su propia fila', () => {
      const workload = workloadOf([task({ assigneeId: '', estimatedHours: 5, dueDate: '2026-08-19' })]);

      expect(workload.rows[0].userId).toBeNull();
    });

    /**
     * Dos personas en una tarea de ocho horas es que las dos tienen ocho horas por delante, no
     * cuatro. Dividirlas haría que la tabla dijera que hay hueco donde no lo hay.
     */
    it('una tarea con varios responsables cuenta entera para cada uno', () => {
      const workload = workloadOf([task({
        assigneeId: 'ana', assignees: ['ana', 'luis'], estimatedHours: 8, dueDate: '2026-08-19',
      })]);

      expect(workload.rows.length).toBe(2);
      expect(workload.rows.every(f => f.total === 8)).toBeTrue();
    });

    it('ordena por quien más acumula, que es la fila que se busca', () => {
      const workload = workloadOf([
        task({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        task({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-19' }),
      ]);

      expect(workload.rows.map(f => f.userId)).toEqual(['luis', 'ana']);
    });

    it('el máximo es la celda más alta, para poder escalar las barras', () => {
      const workload = workloadOf([
        task({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        task({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-19' }),
      ]);

      expect(workload.max).toBe(9);
    });

    it('todas las personas tienen una celda por semana, aunque no trabajen esa semana', () => {
      const workload = workloadOf([
        task({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        task({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-26' }),
      ]);

      expect(workload.weeks.length).toBe(2);
      expect(workload.rows.every(f => f.weeks.length === 2)).toBeTrue();
    });

    it('las semanas salen en orden', () => {
      const workload = workloadOf([
        task({ id: 'a', assigneeId: 'ana', estimatedHours: 1, dueDate: '2026-09-02' }),
        task({ id: 'b', assigneeId: 'ana', estimatedHours: 1, dueDate: '2026-08-19' }),
      ]);

      expect(workload.weeks).toEqual([...workload.weeks].sort((x, y) => x - y));
      expect(dateOfDay(workload.weeks[0]).getUTCMonth()).toBe(7);
    });

    it('una tarea sin horas estimadas no inventa carga', () => {
      const workload = workloadOf([task({ assigneeId: 'ana', estimatedHours: 0, dueDate: '2026-08-19' })]);

      expect(workload.rows[0].total).toBe(0);
      expect(workload.max).toBe(0);
    });
  });
});
