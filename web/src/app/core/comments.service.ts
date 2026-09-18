import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

/**
 * Estas llamadas explican su propio error junto al cuadro de escritura, así que el interceptor
 * no debe levantar además el suyo.
 */
const SIN_AVISO = { sinAviso: true };

/**
 * Sobre qué se puede comentar. Lo fija el backend.
 *
 * `Anotacion` es un comentario en línea dentro de un documento: lo comentado es el trozo de texto
 * señalado, no el documento entero, porque un documento tiene muchas conversaciones a la vez
 * pegadas a sitios distintos.
 */
export const COMMENTABLE_ENTITIES = ['Tarea', 'Ticket', 'Proyecto', 'Anotacion'] as const;
export type CommentableEntity = (typeof COMMENTABLE_ENTITIES)[number];

export interface Comment {
  id: string;
  authorId: string;
  text: string;
  createdAtUtc: string;
  /** Cuándo se editó, o nulo si nunca se tocó. Se enseña: un hilo que cambia sin decirlo no se
   * puede leer con confianza. */
  editedAtUtc: string | null;
  /** El comentario al que responde, si es una respuesta. Un solo nivel. */
  replyToId: string | null;
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

  thread(entityType: CommentableEntity, entityId: string): Observable<Comment[]> {
    return this.api.get<Comment[]>(`/comments/${entityType}/${entityId}`);
  }

  comment(
    entityType: CommentableEntity, entityId: string, text: string, replyToId?: string,
  ): Observable<Comment> {
    return this.api.post<Comment>(
      `/comments/${entityType}/${entityId}`, { text: text, replyToId: replyToId ?? null }, SIN_AVISO);
  }

  edit(id: string, text: string): Observable<void> {
    return this.api.put<void>(`/comments/${id}`, { text: text }, SIN_AVISO);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/comments/${id}`, SIN_AVISO);
  }
}
