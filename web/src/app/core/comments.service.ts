import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

/**
 * Estas llamadas explican su propio error junto al cuadro de escritura, así que el interceptor
 * no debe levantar además el suyo.
 */
const SIN_AVISO = { sinAviso: true };

/** Sobre qué se puede comentar. Lo fija el backend. */
export const ENTIDADES_COMENTABLES = ['Tarea', 'Ticket', 'Proyecto'] as const;
export type EntidadComentable = (typeof ENTIDADES_COMENTABLES)[number];

export interface Comentario {
  id: string;
  autorId: string;
  texto: string;
  creadoUtc: string;
  /** Cuándo se editó, o nulo si nunca se tocó. Se enseña: un hilo que cambia sin decirlo no se
   * puede leer con confianza. */
  editadoUtc: string | null;
  /** El comentario al que responde, si es una respuesta. Un solo nivel. */
  respondeAId: string | null;
}

/**
 * Comentarios de tareas, tickets y proyectos.
 *
 * **Una sola familia de rutas para las tres entidades.** Comentar es la misma operación en los
 * tres sitios; tres servicios serían tres sitios donde arreglar el mismo fallo.
 */
@Injectable({ providedIn: 'root' })
export class CommentsService {
  private readonly api = inject(ApiService);

  hilo(entidad: EntidadComentable, entityId: string): Observable<Comentario[]> {
    return this.api.get<Comentario[]>(`/comments/${entidad}/${entityId}`);
  }

  comentar(
    entidad: EntidadComentable, entityId: string, texto: string, respondeAId?: string,
  ): Observable<Comentario> {
    return this.api.post<Comentario>(
      `/comments/${entidad}/${entityId}`, { texto, respondeAId: respondeAId ?? null }, SIN_AVISO);
  }

  editar(id: string, texto: string): Observable<void> {
    return this.api.put<void>(`/comments/${id}`, { texto }, SIN_AVISO);
  }

  borrar(id: string): Observable<void> {
    return this.api.delete<void>(`/comments/${id}`, SIN_AVISO);
  }
}
