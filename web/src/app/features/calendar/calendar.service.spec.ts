import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { CalendarService, toLocalDateTime, toIsoDate } from './calendar.service';

/**
 * Estas pruebas fijan <b>el contrato</b> con el servidor, que es exactamente lo que estaba roto.
 *
 * La pantalla pedía <code>?from=&to=</code> donde la API lee <code>startDate</code> y
 * <code>endDate</code>, esperaba un array donde llega <code>{ items }</code>, y mandaba
 * <code>startsAtUtc</code> donde el comando espera <code>startTime</code>. Los tres fallos caían en
 * un <code>catch</code> vacío: el calendario salía vacío siempre y crear un evento no hacía nada,
 * sin un solo mensaje. Por eso se comprueban los nombres literales y no «que se llamó a la API».
 */
describe('CalendarioService', () => {
  let service: CalendarService;
  let api: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['get', 'post', 'put', 'patch', 'delete']);
    api.get.and.returnValue(of({ items: [], totalCount: 0 }) as any);
    api.post.and.returnValue(of({}) as any);
    api.put.and.returnValue(of({}) as any);
    api.patch.and.returnValue(of({}) as any);
    api.delete.and.returnValue(of(undefined) as any);

    TestBed.configureTestingModule({
      providers: [CalendarService, { provide: ApiService, useValue: api }]
    });

    service = TestBed.inject(CalendarService);
  });

  it('pide el rango con los nombres que el servidor entiende', async () => {
    await service.eventsBetween(new Date('2026-09-01T00:00:00Z'), new Date('2026-09-30T23:59:59Z'));

    const [route, params] = api.get.calls.mostRecent().args;
    expect(route).toBe('/calendar/events');
    expect(Object.keys(params as object).sort()).toEqual(['endDate', 'pageSize', 'startDate']);
  });

  /**
   * La respuesta viene envuelta. Tratarla como un array hacía que `filter` reventara, y el error
   * se perdía: el mes salía vacío tuviera lo que tuviera.
   */
  it('saca los eventos de dentro de «items»', async () => {
    api.get.and.returnValue(of({ items: [{ id: 'e1', title: 'Reunión' }], totalCount: 1 }) as any);

    const events = await service.eventsBetween(new Date(), new Date());
    expect(events.length).toBe(1);
    expect(events[0].title).toBe('Reunión');
  });

  it('devuelve una lista vacía si la respuesta no trae «items»', async () => {
    api.get.and.returnValue(of({} as any));
    await expectAsync(service.eventsBetween(new Date(), new Date())).toBeResolvedTo([]);
  });

  it('anula por su propia ruta, que no es la de borrar', async () => {
    await service.cancel('e1', 'El cliente lo aplaza');

    expect(api.post).toHaveBeenCalledWith('/calendar/events/e1/cancel', { reason: 'El cliente lo aplaza' });
    expect(api.delete).not.toHaveBeenCalled();
  });

  /**
   * Anular y tirar a la papelera son dos cosas, y la diferencia se nota aquí: si volvieran a ser
   * la misma llamada, un evento anulado desaparecería del calendario otra vez.
   */
  it('la papelera es DELETE y tiene vuelta', async () => {
    await service.moveToTrash('e1');
    expect(api.delete).toHaveBeenCalledWith('/calendar/events/e1');

    await service.restore('e1');
    expect(api.post).toHaveBeenCalledWith('/calendar/events/e1/restore', {});
  });

  it('los enlaces van los tres juntos, para que un nulo signifique «quítalo»', async () => {
    await service.link('e1', { projectId: null, taskId: 't1', ticketId: null });

    expect(api.put).toHaveBeenCalledWith('/calendar/events/e1/links',
      { projectId: null, taskId: 't1', ticketId: null });
  });
});

/**
 * Las fechas se formatean en local a mano y no con `toISOString`.
 *
 * `toISOString().slice(0,10)` pasa por UTC: en un huso al oeste, el 8 a las 20:00 es el 9 en UTC.
 * La agenda del día saldría cambiada justo por la tarde, que es cuando se mira.
 */
describe('Fechas del calendario', () => {
  it('aFechaIso da el día local, no el de UTC', () => {
    // 23:30 del 8 en local. En cualquier huso al oeste de Greenwich esto ya es día 9 en UTC.
    const date = new Date(2026, 8, 8, 23, 30);
    expect(toIsoDate(date)).toBe('2026-09-08');
  });

  it('aFechaIso rellena con ceros', () => {
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05');
  });

  it('aFechaHoraLocal da lo que espera un datetime-local', () => {
    expect(toLocalDateTime(new Date(2026, 8, 8, 9, 5))).toBe('2026-09-08T09:05');
  });
});
