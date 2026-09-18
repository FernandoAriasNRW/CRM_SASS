import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';
import type { MentionCandidate } from './extensions/mention';

/** Un documento que menciona algo. Es lo que devuelve el servidor. */
export interface MentioningDocument {
  documentId: string;
  pageId: string;
  documentTitle: string;
  pageTitle: string;
  visibleText: string;
  mentionedAtUtc: string;
}

/**
 * Busca a quién y a qué se puede mencionar, y pregunta quién menciona a quién.
 *
 * **Cada disparador busca en un sitio distinto**: `@` en la lista de personas y `#` en tareas,
 * tickets y proyectos. Se buscan los tres en paralelo y se mezclan, porque quien escribe `#`
 * quiere «lo que sea que se llame así» y no elegir antes el tipo.
 */
@Injectable({ providedIn: 'root' })
export class MentionsService {
  private readonly api = inject(ApiService);

  /**
   * Cuántos candidatos se ofrecen de cada tipo.
   *
   * Corto a propósito: un desplegable de treinta opciones se lee peor que uno de cinco, y quien
   * no encuentre lo suyo escribe dos letras más.
   */
  private static readonly PORTIPO = 5;

  async search(trigger: string, query: string): Promise<MentionCandidate[]> {
    const text = query.trim();

    // Sin nada escrito no se busca: `@` recién tecleado dispararía cuatro consultas por cada
    // pulsación mientras la persona todavía está pensando a quién menciona.
    if (text.length === 0) return [];

    return trigger === '@' ? this.people(text) : this.things(text);
  }

  private async people(text: string): Promise<MentionCandidate[]> {
    // `/users`, no `/auth/users`. La lista de personas del inquilino cuelga de `/users`;
    // `/auth/users` sólo tiene `/me`. Estuvo mal escrito y **el `catch` de más abajo se lo
    // tragaba**: las menciones con `@` no encontraban a nadie nunca, sin dar ningún error. Hay
    // una prueba que fija estas rutas justo por eso.
    const users = await this.request(
      `/users?pageSize=${MentionsService.PORTIPO}&search=${encodeURIComponent(text)}`,
      [] as { id: string; name: string; email: string }[]);

    return users.map(u => ({ id: u.id, etiqueta: u.name, tipo: 'Persona' as const, detail: u.email }));
  }

  private async things(text: string): Promise<MentionCandidate[]> {
    // En paralelo: son tres módulos distintos y esperarlos en fila triplicaría lo que tarda el
    // desplegable en aparecer, que es justo lo que hace que se deje de usar.
    const [tasks, tickets, projects] = await Promise.all([
      this.fetchList('/tasks', text),
      this.fetchList('/tickets', text),
      this.fetchList('/projects', text)
    ]);

    return [
      ...tasks.map(t => ({ id: t.id, etiqueta: t.title, tipo: 'Tarea' as const, detail: t.detail })),
      ...tickets.map(t => ({ id: t.id, etiqueta: t.title, tipo: 'Ticket' as const, detail: t.detail })),
      ...projects.map(p => ({ id: p.id, etiqueta: p.title, tipo: 'Proyecto' as const, detail: p.detail }))
    ];
  }

  /**
   * Pide una lista ya filtrada por el servidor.
   *
   * **La búsqueda es del servidor y recorre todo el inquilino.** Antes se pedían las primeras
   * cincuenta filas y se filtraban aquí, y eso se degrada en silencio: con miles de tareas, lo que
   * se busca puede no estar entre esas cincuenta y el desplegable sale vacío para algo que sí
   * existe. El fallo sólo aparece cuando el cliente crece, que es cuando nadie lo relaciona con
   * esto.
   *
   * Tampoco hace falta normalizar acentos ni mayúsculas: la base de datos usa una colación
   * insensible a las dos cosas, así que «diseno» encuentra «Diseño» sin que el cliente toque nada.
   * Comprobado contra los datos reales antes de quitar el código que lo hacía a mano.
   */
  private async fetchList(route: string, text: string) {
    const response = await this.request(
      `${route}?pageSize=${MentionsService.PORTIPO}&search=${encodeURIComponent(text)}`,
      {} as { items?: unknown[] });

    const items = (response.items ?? []) as Record<string, unknown>[];

    return items
      .map(i => ({
        id: String(i['id'] ?? ''),
        title: String(i['title'] ?? i['name'] ?? ''),
        detail: String(i['status'] ?? i['priority'] ?? '')
      }))
      .filter(i => i.id);
  }

  /**
   * Pide sin avisar de los errores: un desplegable que no encuentra nada no es algo que anunciar
   * con un aviso flotante encima del editor.
   *
   * **Pero tragarse el error escondió un fallo real:** la ruta de personas estaba mal escrita
   * —`/auth/users` en vez de `/users`— y el 404 desaparecía aquí, así que las menciones con `@` no
   * encontraban a nadie y no había ni un síntoma. Se sigue devolviendo vacío, porque reventar el
   * desplegable sería peor, pero **queda en la consola**: un fallo silencioso al menos deja rastro
   * para quien vaya a mirar.
   *
   * El valor vacío lo pone quien llama, y no es un detalle: devolver siempre `{}` hacía que un
   * fallo de red rompiera el desplegable igual —`{}.slice` no existe—, sólo que unas líneas más
   * abajo y con otro error encima.
   */
  private request<T>(route: string, empty: T): Promise<T> {
    return firstValueFrom(this.api.get<T>(route, undefined, { sinAviso: true }))
      .catch((error) => {
        console.warn(`No se pudo buscar en ${route}`, error);
        return empty;
      });
  }

  /**
   * Qué documentos mencionan algo.
   *
   * Es la vuelta del diferencial, y la razón de que las menciones se guarden en una tabla: leer el
   * documento no contesta esta pregunta, porque habría que abrir todos.
   */
  mentioningDocuments(tipo: string, entidadId: string) {
    return this.api.get<MentioningDocument[]>(`/docs/mentions/${tipo}/${entidadId}`);
  }
}
