import { barsOf, dayFrom, isWeekend, dateOfDay, arrowsOf, todayAsDay, axisTicks, rangeOf } from './gantt';
import type { TaskItem } from './task-create-modal.component';

/**
 * Los cálculos del Gantt.
 *
 * Se prueban aparte y a fondo porque una fecha corrida un día **no da ningún error**: pinta una
 * barra un poco desplazada y nadie lo nota hasta que alguien planifica según ella.
 */
describe('gantt', () => {
  const task = (overrides: Partial<TaskItem>): TaskItem => ({
    id: 't', title: 'Tarea', description: '', status: 'To Do', priority: 'Normal',
    estimatedHours: 0, dueDate: '', projectId: 'p', assigneeId: '',
    ...overrides,
  } as TaskItem);

  describe('dayFrom', () => {
    it('reads a plain date', () => {
      expect(dayFrom('1970-01-01')).toBe(0);
      expect(dayFrom('1970-01-02')).toBe(1);
    });

    it('reads a date with time, as part of the API sends it', () => {
      expect(dayFrom('2026-08-15T00:00:00')).toBe(dayFrom('2026-08-15')!);
    });

    /**
     * `new Date('2026-08-15')` es medianoche UTC: al oeste de Greenwich, pedirle el día local
     * devuelve el 14. Ese error de un día no se ve al programarlo y sale sólo para media Europa
     * y toda América.
     */
    it('does not shift a day because of the time zone', () => {
      const day = dayFrom('2026-08-15')!;

      expect(dateOfDay(day).getUTCDate()).toBe(15);
      expect(dateOfDay(day).getUTCMonth()).toBe(7);
    });

    it('counts a month change correctly', () => {
      expect(dayFrom('2026-09-01')! - dayFrom('2026-08-31')!).toBe(1);
    });

    it('counts a leap year correctly', () => {
      expect(dayFrom('2028-03-01')! - dayFrom('2028-02-28')!).toBe(2);
    });

    it('a missing or malformed value does not crash', () => {
      expect(dayFrom(null)).toBeNull();
      expect(dayFrom(undefined)).toBeNull();
      expect(dayFrom('')).toBeNull();
      expect(dayFrom('mañana')).toBeNull();
    });
  });

  describe('rangeOf', () => {
    const today = dayFrom('2026-08-13')!;

    it('without dated tasks there is nothing to draw', () => {
      expect(rangeOf([], today)).toBeNull();
      expect(rangeOf([task({ dueDate: '' })], today)).toBeNull();
    });

    it('spans from the earliest to the latest date', () => {
      const range = rangeOf([
        task({ dueDate: '2026-08-20', startDate: '2026-08-18' }),
        task({ dueDate: '2026-08-25' }),
      ], today)!;

      expect(range.firstDay).toBe(dayFrom('2026-08-13')!);
      expect(range.lastDay).toBe(dayFrom('2026-08-25')!);
    });

    /** Un diagrama que empieza el mes que viene no deja ver dónde está uno. */
    it('always includes today, even if everything is in the future', () => {
      const range = rangeOf([task({ dueDate: '2026-12-01' })], today)!;

      expect(range.firstDay).toBe(today);
    });

    it('and also if everything is in the past', () => {
      const range = rangeOf([task({ dueDate: '2026-01-05' })], today)!;

      expect(range.lastDay).toBe(today);
    });

    it('counts both ends', () => {
      const range = rangeOf([task({ dueDate: '2026-08-13' })], today)!;

      expect(range.days).toBe(1);
    });
  });

  describe('barsOf', () => {
    const today = dayFrom('2026-08-13')!;

    it('a task with start and due date spans the days between, inclusive', () => {
      const t = task({ dueDate: '2026-08-20', startDate: '2026-08-18' });
      const range = rangeOf([t], today)!;

      const [bar] = barsOf([t], range);

      expect(bar.duration).toBe(3);
      expect(bar.isMilestone).toBeFalse();
      expect(bar.offset).toBe(dayFrom('2026-08-18')! - range.firstDay);
    });

    it('starting and ending on the same day lasts one day, not zero', () => {
      const t = task({ dueDate: '2026-08-20', startDate: '2026-08-20' });
      const range = rangeOf([t], today)!;

      expect(barsOf([t], range)[0].duration).toBe(1);
    });

    /** Inventarle un principio es exactamente lo que se decidió no hacer. */
    it('without a start date a milestone shows on the due date', () => {
      const t = task({ dueDate: '2026-08-20' });
      const range = rangeOf([t], today)!;

      const [bar] = barsOf([t], range);

      expect(bar.isMilestone).toBeTrue();
      expect(bar.duration).toBe(1);
      expect(bar.offset).toBe(dayFrom('2026-08-20')! - range.firstDay);
    });

    it('without a due date it is not drawn: there is nowhere to put it', () => {
      const withDueDate = task({ id: 'a', dueDate: '2026-08-20' });
      const withoutDueDate = task({ id: 'b', dueDate: '' });
      const range = rangeOf([withDueDate, withoutDueDate], today)!;

      expect(barsOf([withDueDate, withoutDueDate], range).map(b => b.task.id)).toEqual(['a']);
    });

    /**
     * El dominio rechaza un inicio posterior al vencimiento, pero un dato viejo no puede dejar
     * la pantalla con una barra de longitud negativa.
     */
    it('a start after the due date is treated as missing', () => {
      const t = task({ dueDate: '2026-08-20', startDate: '2026-09-30' });
      const range = rangeOf([t], today)!;

      const [bar] = barsOf([t], range);

      expect(bar.isMilestone).toBeTrue();
      expect(bar.duration).toBe(1);
    });

    it('marks blocked tasks with what the server already counts', () => {
      const blocked = task({ id: 'a', dueDate: '2026-08-20', blockedByCount: 2 });
      const unblocked = task({ id: 'b', dueDate: '2026-08-21', blockedByCount: 0 });
      const range = rangeOf([blocked, unblocked], today)!;

      const bars = barsOf([blocked, unblocked], range);

      expect(bars[0].isBlocked).toBeTrue();
      expect(bars[1].isBlocked).toBeFalse();
    });
  });

  describe('axisTicks', () => {
    it('marks the start and every first of the month', () => {
      const range = rangeOf([
        task({ dueDate: '2026-10-05', startDate: '2026-08-28' }),
      ], dayFrom('2026-08-28')!)!;

      const days = axisTicks(range).map(m => dateOfDay(m.day).getUTCDate());

      expect(days).toEqual([28, 1, 1]);
    });
  });

  describe('isWeekend', () => {
    it('recognises Saturday and Sunday', () => {
      // 2026-08-15 es sábado y el 16, domingo.
      expect(isWeekend(dayFrom('2026-08-15')!)).toBeTrue();
      expect(isWeekend(dayFrom('2026-08-16')!)).toBeTrue();
      expect(isWeekend(dayFrom('2026-08-17')!)).toBeFalse();
    });
  });

  describe('arrowsOf', () => {
    const today = dayFrom('2026-08-10')!;

    /** `a` empieza el 18 y vence el 20; `b` empieza el 22 y vence el 24. */
    const first = task({ id: 'a', dueDate: '2026-08-20', startDate: '2026-08-18' });
    const second = task({ id: 'b', dueDate: '2026-08-24', startDate: '2026-08-22' });

    function bars(tasks = [first, second]) {
      return barsOf(tasks, rangeOf(tasks, today)!);
    }

    it('connects the blocking bar with the blocked one', () => {
      const [arrow] = arrowsOf([{ taskId: 'b', dependsOnTaskId: 'a' }], bars());

      expect(arrow.fromRow).toBe(0);
      expect(arrow.toRow).toBe(1);
      expect(arrow.isViolated).toBeFalse();
    });

    /**
     * Es lo que un Gantt tiene que gritar: el plan es imposible tal cual está, porque lo que
     * bloquea todavía no ha terminado cuando lo bloqueado ya tendría que haber empezado.
     */
    it('marks as broken the one going back in time', () => {
      const [arrow] = arrowsOf([{ taskId: 'a', dependsOnTaskId: 'b' }], bars());

      expect(arrow.isViolated).toBeTrue();
    });

    it('chaining on the very next day is not considered broken', () => {
      const before = task({ id: 'a', dueDate: '2026-08-20', startDate: '2026-08-18' });
      const after = task({ id: 'b', dueDate: '2026-08-25', startDate: '2026-08-21' });

      const [arrow] = arrowsOf(
        [{ taskId: 'b', dependsOnTaskId: 'a' }], bars([before, after]));

      expect(arrow.isViolated).toBeFalse();
    });

    /** Una flecha que sale del diagrama y no llega a nada confunde más que no dibujarla. */
    it('discards those pointing to a task that is not drawn', () => {
      const arrows = arrowsOf([
        { taskId: 'b', dependsOnTaskId: 'fantasma' },
        { taskId: 'fantasma', dependsOnTaskId: 'a' },
      ], bars());

      expect(arrows).toEqual([]);
    });

    it('without dependencies there are no arrows', () => {
      expect(arrowsOf([], bars())).toEqual([]);
    });

    it('a task can have several arrows', () => {
      const third = task({ id: 'c', dueDate: '2026-08-28', startDate: '2026-08-26' });

      const arrows = arrowsOf([
        { taskId: 'c', dependsOnTaskId: 'a' },
        { taskId: 'c', dependsOnTaskId: 'b' },
      ], bars([first, second, third]));

      expect(arrows.length).toBe(2);
      expect(arrows.map(f => f.toRow)).toEqual([2, 2]);
    });
  });

  describe('todayAsDay', () => {
    it('uses the local day, not UTC, which is what the viewer sees', () => {
      const newYearsEve = new Date(2026, 11, 31, 23, 30);

      expect(todayAsDay(newYearsEve)).toBe(dayFrom('2026-12-31')!);
    });
  });
});
