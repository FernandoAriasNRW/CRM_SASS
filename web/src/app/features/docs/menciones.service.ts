import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';
import type { CandidatoDeMencion } from './extensions/mencion';

/** Un documento que menciona algo. Es lo que devuelve el servidor. */
export interface DocumentoQueMenciona {
  documentId: string;
  pageId: string;
  tituloDelDocumento: string;
  tituloDeLaPagina: string;
  textoVisible: string;
  mencionadaUtc: string;
}

/**
 * Busca a quién y a qué se puede mencionar, y pregunta quién menciona a quién.
 *
 * **Cada disparador busca en un sitio distinto**: `@` en la lista de personas y `#` en tareas,
 * tickets y proyectos. Se buscan los tres en paralelo y se mezclan, porque quien escribe `#`
 * quiere «lo que sea que se llame así» y no elegir antes el tipo.
 */
@Injectable({ providedIn: 'root' })
export class MencionesService {
  private readonly api = inject(ApiService);

  /**
   * Cuántos candidatos se ofrecen de cada tipo.
   *
   * Corto a propósito: un desplegable de treinta opciones se lee peor que uno de cinco, y quien
   * no encuentre lo suyo escribe dos letras más.
   */
  private static readonly PORTIPO = 5;

  async buscar(disparador: string, consulta: string): Promise<CandidatoDeMencion[]> {
    const texto = consulta.trim();

    // Sin nada escrito no se busca: `@` recién tecleado dispararía cuatro consultas por cada
    // pulsación mientras la persona todavía está pensando a quién menciona.
    if (texto.length === 0) return [];

    return disparador === '@' ? this.personas(texto) : this.cosas(texto);
  }

  private async personas(texto: string): Promise<CandidatoDeMencion[]> {
    const usuarios = await this.pedir<{ id: string; name: string; email: string }[]>('/auth/users');

    return usuarios
      .filter(u => this.coincide(u.name, texto) || this.coincide(u.email, texto))
      .slice(0, MencionesService.PORTIPO)
      .map(u => ({ id: u.id, etiqueta: u.name, tipo: 'Persona' as const, detalle: u.email }));
  }

  private async cosas(texto: string): Promise<CandidatoDeMencion[]> {
    // En paralelo: son tres módulos distintos y esperarlos en fila triplicaría lo que tarda el
    // desplegable en aparecer, que es justo lo que hace que se deje de usar.
    const [tareas, tickets, proyectos] = await Promise.all([
      this.pedirLista('/tasks', texto),
      this.pedirLista('/tickets', texto),
      this.pedirLista('/projects', texto)
    ]);

    return [
      ...tareas.map(t => ({ id: t.id, etiqueta: t.titulo, tipo: 'Tarea' as const, detalle: t.detalle })),
      ...tickets.map(t => ({ id: t.id, etiqueta: t.titulo, tipo: 'Ticket' as const, detalle: t.detalle })),
      ...proyectos.map(p => ({ id: p.id, etiqueta: p.titulo, tipo: 'Proyecto' as const, detalle: p.detalle }))
    ];
  }

  /**
   * Pide una lista y se queda con lo que coincide.
   *
   * **El filtrado es en el cliente y eso tiene un límite escrito:** se piden las primeras
   * cincuenta y se filtran aquí. Con miles de tareas, lo que se busca puede no estar entre esas
   * cincuenta y el desplegable saldría vacío para algo que sí existe. El arreglo de verdad es un
   * parámetro de búsqueda por texto en la API, que hoy no existe; queda anotado en la auditoría.
   */
  private async pedirLista(ruta: string, texto: string) {
    const respuesta = await this.pedir<{ items?: unknown[] }>(`${ruta}?pageSize=50`);
    const items = (respuesta.items ?? []) as Record<string, unknown>[];

    return items
      .map(i => ({
        id: String(i['id'] ?? ''),
        titulo: String(i['title'] ?? i['name'] ?? ''),
        detalle: String(i['status'] ?? i['priority'] ?? '')
      }))
      .filter(i => i.id && this.coincide(i.titulo, texto))
      .slice(0, MencionesService.PORTIPO);
  }

  /**
   * Compara sin acentos y sin mayúsculas.
   *
   * Buscar «diseno» tiene que encontrar «Diseño»: quien escribe deprisa no pone la tilde, y un
   * buscador que no la encuentra parece que no tiene el dato.
   */
  private coincide(valor: string, texto: string): boolean {
    return this.normalizar(valor).includes(this.normalizar(texto));
  }

  private normalizar(texto: string): string {
    return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
  }

  /** Sin aviso automático: un desplegable que no encuentra nada no es un error que anunciar. */
  private pedir<T>(ruta: string): Promise<T> {
    return firstValueFrom(this.api.get<T>(ruta, undefined, { sinAviso: true }))
      .catch(() => ({} as T));
  }

  /**
   * Qué documentos mencionan algo.
   *
   * Es la vuelta del diferencial, y la razón de que las menciones se guarden en una tabla: leer el
   * documento no contesta esta pregunta, porque habría que abrir todos.
   */
  quienMenciona(tipo: string, entidadId: string) {
    return this.api.get<DocumentoQueMenciona[]>(`/docs/menciones/${tipo}/${entidadId}`);
  }
}
