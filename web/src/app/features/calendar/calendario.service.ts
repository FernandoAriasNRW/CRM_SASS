import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';

/** Un evento del calendario, tal y como lo devuelve la API. */
export interface EventoDelCalendario {
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
  canceladoEnUtc: string | null;
  motivoDeCancelacion: string | null;
}

/** Una cosa que cae en un día, venga del módulo que venga. */
export interface CosaDelDia {
  tipo: 'Evento' | 'Tarea' | 'Ticket' | 'Proyecto';
  id: string;
  titulo: string;
  detalle: string | null;
  hora: string | null;
  horaFin: string | null;
  anulado: boolean;
}

export interface AgendaDeUnDia {
  dia: string;
  eventos: CosaDelDia[];
  tareasQueVencen: CosaDelDia[];
  ticketsDelDia: CosaDelDia[];
  proyectosQueTerminan: CosaDelDia[];
}

/** Lo que hace falta para crear o modificar un evento. */
export interface DatosDelEvento {
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
export class CalendarioService {
  private readonly api = inject(ApiService);

  /**
   * Los eventos entre dos fechas.
   *
   * `pageSize` alto a propósito: un mes cabe de sobra, y pedirlo por páginas dejaría días a
   * medias sin que nada lo indicara —el hueco parecería un día libre—.
   */
  async eventosEntre(desde: Date, hasta: Date): Promise<EventoDelCalendario[]> {
    const respuesta = await firstValueFrom(
      this.api.get<Paginado<EventoDelCalendario>>('/calendar/events', {
        startDate: desde.toISOString(),
        endDate: hasta.toISOString(),
        pageSize: 500
      }));

    return respuesta?.items ?? [];
  }

  agenda(dia: Date): Promise<AgendaDeUnDia> {
    return firstValueFrom(this.api.get<AgendaDeUnDia>(`/calendar/agenda/${aFechaIso(dia)}`));
  }

  crear(datos: DatosDelEvento): Promise<EventoDelCalendario> {
    return firstValueFrom(this.api.post<EventoDelCalendario>('/calendar/events', datos));
  }

  modificar(id: string, datos: Partial<DatosDelEvento>): Promise<EventoDelCalendario> {
    return firstValueFrom(this.api.patch<EventoDelCalendario>(`/calendar/events/${id}`, datos));
  }

  /** Anula el evento: se queda en el calendario, tachado. */
  anular(id: string, motivo: string | null): Promise<EventoDelCalendario> {
    return firstValueFrom(this.api.post<EventoDelCalendario>(`/calendar/events/${id}/anular`, { motivo }));
  }

  reactivar(id: string): Promise<EventoDelCalendario> {
    return firstValueFrom(this.api.post<EventoDelCalendario>(`/calendar/events/${id}/reactivar`, {}));
  }

  /**
   * Fija los enlaces del evento. Se mandan los tres siempre: un nulo quita el enlace, y sin
   * mandarlos todos no habría forma de distinguir «quítalo» de «no lo toques».
   */
  enlazar(id: string, enlaces: { projectId: string | null; taskId: string | null; ticketId: string | null }) {
    return firstValueFrom(this.api.put<EventoDelCalendario>(`/calendar/events/${id}/enlaces`, enlaces));
  }

  /** A la papelera. Recuperable. */
  aLaPapelera(id: string): Promise<void> {
    return firstValueFrom(this.api.delete<void>(`/calendar/events/${id}`));
  }

  async papelera(): Promise<EventoDelCalendario[]> {
    const respuesta = await firstValueFrom(
      this.api.get<Paginado<EventoDelCalendario>>('/calendar/events/papelera'));

    return respuesta?.items ?? [];
  }

  restaurar(id: string): Promise<EventoDelCalendario> {
    return firstValueFrom(this.api.post<EventoDelCalendario>(`/calendar/events/${id}/restaurar`, {}));
  }
}

/**
 * La fecha en «aaaa-mm-dd», <b>en local</b>.
 *
 * No sirve `toISOString().slice(0,10)`: eso pasa por UTC, y para quien está en un huso al oeste
 * el 8 a las 20:00 es el 9 en UTC. La agenda del día saldría cambiada justo por la tarde, que es
 * cuando se mira.
 */
export function aFechaIso(fecha: Date): string {
  const mes = `${fecha.getMonth() + 1}`.padStart(2, '0');
  const dia = `${fecha.getDate()}`.padStart(2, '0');
  return `${fecha.getFullYear()}-${mes}-${dia}`;
}

/**
 * Lo que espera un `<input type="datetime-local">`: «aaaa-mm-ddThh:mm», en local y sin zona.
 *
 * Mismo motivo que arriba, y con más consecuencia: darle un valor en UTC hace que el formulario
 * enseñe una hora distinta de la que el usuario acaba de elegir.
 */
export function aFechaHoraLocal(fecha: Date): string {
  const hora = `${fecha.getHours()}`.padStart(2, '0');
  const minuto = `${fecha.getMinutes()}`.padStart(2, '0');
  return `${aFechaIso(fecha)}T${hora}:${minuto}`;
}
