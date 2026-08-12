import { Injectable, inject } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface DocumentDto {
  id: string;
  title: string;
  description: string;
  type: number; // 1: List, 2: Wiki, 3: MeetingNote, 4: Template
  ownerId: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateDocumentRequest {
  title: string;
  description: string;
  type: number;
  teamId?: string | null;
  projectId?: string | null;
  initialContent?: string | null;
}

export interface PageDto {
  id: string;
  documentId: string;
  parentPageId?: string;
  title: string;
  content: string;
  order: number;
  subPages?: PageDto[];
}

export interface CreatePageRequest {
  parentPageId?: string;
  title: string;
}

export interface UpdatePageRequest {
  title: string;
  content: string;
}

export interface SaveAsTemplateRequest {
  customTitle?: string;
  description?: string;
}

export interface CreateFromTemplateRequest {
  templateKey?: string;
  templateDocumentId?: string;
  customTitle?: string;
}

export interface ImportDocumentRequest {
  title: string;
  content: string;
  type?: number;
}

@Injectable({
  providedIn: 'root'
})
export class DocsService {
  private api = inject(ApiService);
  private endpoint = '/docs';

  getDocuments(): Observable<DocumentDto[]> {
    return this.api.get<DocumentDto[]>(this.endpoint);
  }

  createDocument(req: CreateDocumentRequest): Observable<string> {
    return this.api.post<string>(this.endpoint, req).pipe(map(idLimpio));
  }

  getPages(documentId: string): Observable<PageDto[]> {
    return this.api.get<PageDto[]>(`${this.endpoint}/${documentId}/pages`);
  }

  createPage(documentId: string, req: CreatePageRequest): Observable<string> {
    return this.api.post<string>(`${this.endpoint}/${documentId}/pages`, req);
  }

  updatePage(pageId: string, req: UpdatePageRequest): Observable<void> {
    return this.api.put<void>(`${this.endpoint}/pages/${pageId}`, req);
  }

  deleteDocument(documentId: string): Observable<void> {
    return this.api.delete<void>(`${this.endpoint}/${documentId}`);
  }

  deletePage(documentId: string, pageId: string): Observable<void> {
    return this.api.delete<void>(`${this.endpoint}/pages/${pageId}`);
  }

  saveAsTemplate(documentId: string, req: SaveAsTemplateRequest): Observable<string> {
    return this.api.post<string>(`${this.endpoint}/${documentId}/save-as-template`, req);
  }

  createFromTemplate(req: CreateFromTemplateRequest): Observable<string> {
    return this.api.post<string>(`${this.endpoint}/from-template`, req).pipe(map(idLimpio));
  }

  importDocument(req: ImportDocumentRequest): Observable<string> {
    return this.api.post<string>(`${this.endpoint}/import`, req).pipe(map(idLimpio));
  }
}

/**
 * Normaliza el identificador que devuelve la API al crear un documento.
 *
 * Llega como cadena entrecomillada —`"abc-123"` con las comillas dentro del valor— y a
 * veces envuelto en un objeto. Cada punto de creación repetía la misma limpieza; ahora se
 * hace una vez, donde entra el dato, y quien lo consume recibe un id ya utilizable.
 *
 * Lo correcto sería que la API devolviera JSON bien formado. Mientras no lo haga, este es
 * el único sitio que hay que cambiar.
 */
function idLimpio(valor: unknown): string {
  if (typeof valor === 'string') {
    return valor.replace(/["']/g, '');
  }
  const obj = valor as { value?: string; id?: string } | null;
  return obj?.value ?? obj?.id ?? String(valor);
}
