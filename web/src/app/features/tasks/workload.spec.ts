import { workloadOf, mondayOf } from './workload';
import { dayFrom, dateOfDay } from './gantt';
import type { TaskItem } from './task-create-modal.component';

/**
 * El reparto de carga.
 *
 * Se prueba a fondo porque una suma mal repartida **no da error**: sale un número plausible, y
 * con él alguien decide si contrata, si aplaza o si le pide más a quien ya no puede.
 */
describe('workload', () => {
  const task = (overrides: Partial<TaskItem>): TaskItem => ({
    id: 't', title: 'Tarea', description: '', status: 'To Do', priority: 'Normal',
    estimatedHours: 0, dueDate: '', projectId: 'p', assigneeId: '',
    ...overrides,
  } as TaskItem);

  describe('mondayOf', () => {
    it('a Wednesday maps to its Monday', () => {
      // 2026-08-19 es miércoles; su lunes es el 17.
      expect(mondayOf(dayFrom('2026-08-19')!)).toBe(dayFrom('2026-08-17')!);
    });

    it('a Monday is its own Monday', () => {
      expect(mondayOf(dayFrom('2026-08-17')!)).toBe(dayFrom('2026-08-17')!);
    });

    /** `getUTCDay` numera el domingo como 0: sin cuidado, el domingo salta a la semana siguiente. */
    it('a Sunday belongs to the week that ends, not the one that starts', () => {
      expect(mondayOf(dayFrom('2026-08-23')!)).toBe(dayFrom('2026-08-17')!);
    });
  });

  describe('workloadOf', () => {
    it('without tasks there is nothing to split', () => {
      expect(workloadOf([]).rows).toEqual([]);
    });

    it('a one-day task loads all its hours on its week', () => {
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 8, dueDate: '2026-08-19',
      })]);

      expect(workload.rows.length).toBe(1);
      expect(workload.rows[0].userId).toBe('ana');
      expect(workload.rows[0].total).toBe(8);
    });

    it('splits across the working days it spans', () => {
      // Del martes 18 al jueves 20: tres días laborables, ocho horas.
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 9, startDate: '2026-08-18', dueDate: '2026-08-20',
      })]);

      expect(workload.rows[0].total).toBe(9);
      expect(workload.rows[0].weeks.length).toBe(1);
    });

    it('a task spanning two weeks splits between both', () => {
      // Del jueves 20 al martes 25.
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 8, startDate: '2026-08-20', dueDate: '2026-08-25',
      })]);

      expect(workload.weeks.length).toBe(2);
      expect(workload.rows[0].weeks.every(c => c.hours > 0)).toBeTrue();
      expect(workload.rows[0].total).toBe(8);
    });

    /** El fin de semana no es tiempo de trabajo, así que no diluye la carga de los días útiles. */
    it('weekends get no hours', () => {
      // Del viernes 21 al lunes 24: dos días laborables, no cuatro.
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 10, startDate: '2026-08-21', dueDate: '2026-08-24',
      })]);

      const fridayWeek = workload.rows[0].weeks.find(c => c.week === mondayOf(dayFrom('2026-08-21')!))!;

      expect(fridayWeek.hours).toBe(5);
    });

    it('a task entirely on a weekend loads on its due date', () => {
      const workload = workloadOf([task({
        assigneeId: 'ana', estimatedHours: 4, startDate: '2026-08-22', dueDate: '2026-08-23',
      })]);

      expect(workload.rows[0].total).toBe(4);
    });

    it('completed tasks do not count: they are no longer future workload', () => {
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
    it('those without a due date are counted apart', () => {
      const workload = workloadOf([
        task({ assigneeId: 'ana', estimatedHours: 8, dueDate: '' }),
        task({ assigneeId: 'ana', estimatedHours: 3, dueDate: '2026-08-19' }),
      ]);

      expect(workload.withoutDueDate).toBe(1);
      expect(workload.rows[0].total).toBe(3);
    });

    it('those without an assignee go to their own row', () => {
      const workload = workloadOf([task({ assigneeId: '', estimatedHours: 5, dueDate: '2026-08-19' })]);

      expect(workload.rows[0].userId).toBeNull();
    });

    /**
     * Dos personas en una tarea de ocho horas es que las dos tienen ocho horas por delante, no
     * cuatro. Dividirlas haría que la tabla dijera que hay hueco donde no lo hay.
     */
    it('a task with several assignees counts fully for each', () => {
      const workload = workloadOf([task({
        assigneeId: 'ana', assignees: ['ana', 'luis'], estimatedHours: 8, dueDate: '2026-08-19',
      })]);

      expect(workload.rows.length).toBe(2);
      expect(workload.rows.every(f => f.total === 8)).toBeTrue();
    });

    it('sorts by whoever has the most, which is the row people look for', () => {
      const workload = workloadOf([
        task({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        task({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-19' }),
      ]);

      expect(workload.rows.map(f => f.userId)).toEqual(['luis', 'ana']);
    });

    it('the max is the highest cell, so the bars can be scaled', () => {
      const workload = workloadOf([
        task({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        task({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-19' }),
      ]);

      expect(workload.max).toBe(9);
    });

    it('everyone has a cell per week, even if they do not work that week', () => {
      const workload = workloadOf([
        task({ id: 'a', assigneeId: 'ana', estimatedHours: 2, dueDate: '2026-08-19' }),
        task({ id: 'b', assigneeId: 'luis', estimatedHours: 9, dueDate: '2026-08-26' }),
      ]);

      expect(workload.weeks.length).toBe(2);
      expect(workload.rows.every(f => f.weeks.length === 2)).toBeTrue();
    });

    it('weeks come in order', () => {
      const workload = workloadOf([
        task({ id: 'a', assigneeId: 'ana', estimatedHours: 1, dueDate: '2026-09-02' }),
        task({ id: 'b', assigneeId: 'ana', estimatedHours: 1, dueDate: '2026-08-19' }),
      ]);

      expect(workload.weeks).toEqual([...workload.weeks].sort((x, y) => x - y));
      expect(dateOfDay(workload.weeks[0]).getUTCMonth()).toBe(7);
    });

    it('a task without estimated hours invents no workload', () => {
      const workload = workloadOf([task({ assigneeId: 'ana', estimatedHours: 0, dueDate: '2026-08-19' })]);

      expect(workload.rows[0].total).toBe(0);
      expect(workload.max).toBe(0);
    });
  });
});
