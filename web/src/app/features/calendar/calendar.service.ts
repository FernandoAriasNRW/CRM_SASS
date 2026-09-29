import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';

/** Un evento del calendario, tal y como lo devuelve la API. */
export interface CalendarEvent {
  id: string;
  title: string;
  description: string | null;
  type: string;
  startTime: string;
  endTime: string;
  location: string | null;
  isAllDay: boolean;
  projectId: string | null;
  taskId: string | null;
  ticketId: string | null;
  /** Si está anulado. Sigue en el calendario, tachado. */
  cancelledAtUtc: string | null;
  cancellationReason: string | null;
}

/** Una cosa que cae en un día, venga del módulo que venga. */
export interface AgendaItem {
  type: 'Event' | 'Task' | 'Ticket' | 'Project';
  id: string;
  title: string;
  detail: string | null;
  time: string | null;
  endTime: string | null;
  isCancelled: boolean;
}

export interface DailyAgenda {
  day: string;
  events: AgendaItem[];
  tasksDue: AgendaItem[];
  ticketsOpened: AgendaItem[];
  projectsEnding: AgendaItem[];
}

/** Lo que hace falta para crear o modificar un evento. */
export interface CalendarEventInput {
  title: string;
  description?: string | null;
  type: string;
  startTime: string;
  endTime: string;
  location?: string | null;
  isAllDay?: boolean;
  projectId?: string | null;
  taskId?: string | null;
  ticketId?: string | null;
}

/** Lo que devuelve una lista paginada de la API. */
interface Paginado<T> {
  items: T[];
  totalCount: number;
}

/**
 * Todo lo que la pantalla del calendario le pide al servidor.
 *
 * <b>Existe porque la pantalla hablaba un idioma que la API no entiende.</b> Pedía
 * <code>?from=&to=</code> cuando el servidor lee <code>startDate</code> y <code>endDate</code>,
 * esperaba un array donde llega <code>{ items, totalCount }</code>, y mandaba
 * <code>startsAtUtc</code> donde el comando espera <code>startTime</code>. Los tres desajustes
 * caían en el mismo <code>error: () =&gt; {}</code>, así que <b>el calendario salía vacío
 * siempre</b> y crear un evento no hacía nada, sin un solo mensaje.
 *
 * Con los nombres en un sitio, el próximo cambio del contrato rompe la compilación en vez de
 * romper la pantalla en silencio.
 */
@Injectable({ providedIn: 'root' })
export class CalendarService {
  private readonly api = inject(ApiService);

  /**
   * Los eventos entre dos fechas.
   *
   * `pageSize` alto a propósito: un mes cabe de sobra, y pedirlo por páginas dejaría días a
   * medias sin que nada lo indicara —el hueco parecería un día libre—.
   */
  async eventsBetween(from: Date, to: Date): Promise<CalendarEvent[]> {
    const response = await firstValueFrom(
      this.api.get<Paginado<CalendarEvent>>('/calendar/events', {
        startDate: from.toISOString(),
        endDate: to.toISOString(),
        pageSize: 500
      }));

    return response?.items ?? [];
  }

  agenda(day: Date): Promise<DailyAgenda> {
    return firstValueFrom(this.api.get<DailyAgenda>(`/calendar/agenda/${toIsoDate(day)}`));
  }

  create(data: CalendarEventInput): Promise<CalendarEvent> {
    return firstValueFrom(this.api.post<CalendarEvent>('/calendar/events', data));
  }

  update(id: string, data: Partial<CalendarEventInput>): Promise<CalendarEvent> {
    return firstValueFrom(this.api.patch<CalendarEvent>(`/calendar/events/${id}`, data));
  }

  /** Anula el evento: se queda en el calendario, tachado. */
  cancel(id: string, reason: string | null): Promise<CalendarEvent> {
    return firstValueFrom(this.api.post<CalendarEvent>(`/calendar/events/${id}/cancel`, { reason: reason }));
  }

  reactivate(id: string): Promise<CalendarEvent> {
    return firstValueFrom(this.api.post<CalendarEvent>(`/calendar/events/${id}/reactivate`, {}));
  }

  /**
   * Fija los enlaces del evento. Se mandan los tres siempre: un nulo quita el enlace, y sin
   * mandarlos todos no habría forma de distinguir «quítalo» de «no lo toques».
   */
  link(id: string, links: { projectId: string | null; taskId: string | null; ticketId: string | null }) {
    return firstValueFrom(this.api.put<CalendarEvent>(`/calendar/events/${id}/links`, links));
  }

  /** A la papelera. Recuperable. */
  moveToTrash(id: string): Promise<void> {
    return firstValueFrom(this.api.delete<void>(`/calendar/events/${id}`));
  }

  async trash(): Promise<CalendarEvent[]> {
    const response = await firstValueFrom(
      this.api.get<Paginado<CalendarEvent>>('/calendar/events/trash'));

    return response?.items ?? [];
  }

  restore(id: string): Promise<CalendarEvent> {
    return firstValueFrom(this.api.post<CalendarEvent>(`/calendar/events/${id}/restore`, {}));
  }
}

/**
 * La fecha en «aaaa-mm-dd», <b>en local</b>.
 *
 * No sirve `toISOString().slice(0,10)`: eso pasa por UTC, y para quien está en un huso al oeste
 * el 8 a las 20:00 es el 9 en UTC. La agenda del día saldría cambiada justo por la tarde, que es
 * cuando se mira.
 */
export function toIsoDate(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/**
 * Lo que espera un `<input type="datetime-local">`: «aaaa-mm-ddThh:mm», en local y sin zona.
 *
 * Mismo motivo que arriba, y con más consecuencia: darle un valor en UTC hace que el formulario
 * enseñe una hora distinta de la que el usuario acaba de elegir.
 */
export function toLocalDateTime(date: Date): string {
  const hour = `${date.getHours()}`.padStart(2, '0');
  const minute = `${date.getMinutes()}`.padStart(2, '0');
  return `${toIsoDate(date)}T${hour}:${minute}`;
}
