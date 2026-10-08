import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/api.service';

/** Un documento adjunto a una tarea. El título lo da Docs al pedirlo, no se guarda en la tarea. */
export interface AttachedDocument {
  documentId: string;
  title: string;
  documentUpdatedAtUtc: string;
  attachedById: string;
  attachedAtUtc: string;
}

/** Una tarea que tiene adjunto un documento, tal como se enseña desde el documento. */
export interface TaskWithDocument {
  taskId: string;
  projectId: string;
  title: string;
  status: string;
}

/**
 * Los documentos adjuntos a las tareas, desde los dos lados: los de una tarea y las tareas de un
 * documento. Los dos componentes que lo usan —la ficha de la tarea y el documento— hablan con los
 * mismos endpoints, y así no hay dos formas de escribir la misma ruta.
 */
@Injectable({ providedIn: 'root' })
export class AttachedDocumentsService {
  private readonly api = inject(ApiService);

  forTask(taskId: string): Observable<AttachedDocument[]> {
    return this.api.get<AttachedDocument[]>(`/tasks/${taskId}/documents`);
  }

  attach(taskId: string, documentId: string): Observable<AttachedDocument> {
    return this.api.post<AttachedDocument>(`/tasks/${taskId}/documents`, { documentId });
  }

  detach(taskId: string, documentId: string): Observable<void> {
    return this.api.delete<void>(`/tasks/${taskId}/documents/${documentId}`);
  }

  tasksWithDocument(documentId: string): Observable<TaskWithDocument[]> {
    return this.api.get<TaskWithDocument[]>(`/tasks/with-document/${documentId}`);
  }
}
