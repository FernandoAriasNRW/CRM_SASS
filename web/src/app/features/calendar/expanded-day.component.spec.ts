import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ExpandedDayComponent } from './expanded-day.component';
import type { DailyAgenda, AgendaItem } from './calendar.service';

/**
 * El día desplegado: sus horas, y lo que va arriba porque no tiene hora.
 *
 * Se comprueba sobre todo <b>dónde cae cada cosa</b>. Una tarea que vence no ocurre a una hora
 * concreta, y colocarla en una inventada haría creer que sí; y un día que empieza a las 00:00
 * obliga a desplazarse ocho franjas vacías para ver la primera reunión.
 */
describe('DiaDesplegadoComponent', () => {
  let fixture: ComponentFixture<ExpandedDayComponent>;
  let component: ExpandedDayComponent;

  const calendarEvent = (id: string, hour: string, cancelled = false): AgendaItem => ({
    type: 'Event', id, title: `Evento ${id}`, detail: null,
    time: hour, endTime: null, isCancelled: cancelled
  });

  const agenda = (partial: Partial<DailyAgenda> = {}): DailyAgenda => ({
    day: '2026-09-08',
    events: [],
    tasksDue: [],
    ticketsOpened: [],
    projectsEnding: [],
    ...partial
  });

  const render = (a: DailyAgenda) => {
    fixture.componentRef.setInput('agenda', a);
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ExpandedDayComponent] }).compileComponents();

    fixture = TestBed.createComponent(ExpandedDayComponent);
    component = fixture.componentInstance;
    render(agenda());
  });

  it('un día vacío sigue teniendo horas donde pulsar', () => {
    const slots = component.slots();

    expect(slots[0].hour).toBe(8);
    expect(slots[slots.length - 1].hour).toBe(19);
  });

  /**
   * Con algo antes de las 8 o después de las 19, la franja se estira. Si no, el evento existiría
   * en los datos y no habría dónde pintarlo.
   */
  it('se estira para que quepa lo que cae fuera del horario normal', () => {
    render(agenda({ events: [calendarEvent('a', '2026-09-08T06:30:00'), calendarEvent('b', '2026-09-08T22:00:00')] }));

    const hours = component.slots().map(f => f.hour);
    expect(hours[0]).toBe(6);
    expect(hours[hours.length - 1]).toBe(22);
  });

  it('coloca cada evento en su hora', () => {
    render(agenda({ events: [calendarEvent('a', '2026-09-08T09:15:00'), calendarEvent('b', '2026-09-08T09:45:00')] }));

    const atNine = component.slots().find(f => f.hour === 9)!;
    expect(atNine.events.map(e => e.id)).toEqual(['a', 'b']);
  });

  /**
   * Lo que vence ese día va arriba, no repartido por horas. Es la diferencia entre «esto pasa a
   * las 10» y «esto hay que tenerlo hecho hoy».
   */
  it('lo que no tiene hora va aparte', () => {
    const task: AgendaItem = {
      type: 'Task', id: 't1', title: 'Migrar la base', detail: 'En curso',
      time: null, endTime: null, isCancelled: false
    };

    render(agenda({ tasksDue: [task], events: [calendarEvent('a', '2026-09-08T09:00:00')] }));

    expect(component.untimed().map(c => c.id)).toEqual(['t1']);
    expect(component.slots().flatMap(f => f.events).map(e => e.id)).toEqual(['a']);
  });

  it('el resumen cuenta sólo lo que hay', () => {
    expect(component.summary()).toBe('Nada en el calendario este día');

    render(agenda({ events: [calendarEvent('a', '2026-09-08T09:00:00')] }));
    expect(component.summary()).toBe('1 evento');

    render(agenda({ events: [calendarEvent('a', '2026-09-08T09:00:00'), calendarEvent('b', '2026-09-08T10:00:00')] }));
    expect(component.summary()).toBe('2 eventos');
  });

  /** Un evento anulado se sigue viendo, tachado: es todo el sentido de separarlo de la papelera. */
  it('pinta tachado lo anulado, sin quitarlo', () => {
    render(agenda({ events: [calendarEvent('a', '2026-09-08T09:00:00', true)] }));

    const button = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLElement>
    ).find(b => b.textContent?.includes('Evento a'))!;

    expect(button).withContext('un evento anulado no puede desaparecer del día').toBeTruthy();
    expect(button.className).toContain('line-through');
  });
});
