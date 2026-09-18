import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';
import type { CandidatoDeMencion } from './extensions/mencion';

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
    // `/users`, no `/auth/users`. La lista de personas del inquilino cuelga de `/users`;
    // `/auth/users` sólo tiene `/me`. Estuvo mal escrito y **el `catch` de más abajo se lo
    // tragaba**: las menciones con `@` no encontraban a nadie nunca, sin dar ningún error. Hay
    // una prueba que fija estas rutas justo por eso.
    const usuarios = await this.pedir(
      `/users?pageSize=${MencionesService.PORTIPO}&search=${encodeURIComponent(texto)}`,
      [] as { id: string; name: string; email: string }[]);

    return usuarios.map(u => ({ id: u.id, etiqueta: u.name, tipo: 'Persona' as const, detalle: u.email }));
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
  private async pedirLista(ruta: string, texto: string) {
    const respuesta = await this.pedir(
      `${ruta}?pageSize=${MencionesService.PORTIPO}&search=${encodeURIComponent(texto)}`,
      {} as { items?: unknown[] });

    const items = (respuesta.items ?? []) as Record<string, unknown>[];

    return items
      .map(i => ({
        id: String(i['id'] ?? ''),
        titulo: String(i['title'] ?? i['name'] ?? ''),
        detalle: String(i['status'] ?? i['priority'] ?? '')
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
  private pedir<T>(ruta: string, vacio: T): Promise<T> {
    return firstValueFrom(this.api.get<T>(ruta, undefined, { sinAviso: true }))
      .catch((error) => {
        console.warn(`No se pudo buscar en ${ruta}`, error);
        return vacio;
      });
  }

  /**
   * Qué documentos mencionan algo.
   *
   * Es la vuelta del diferencial, y la razón de que las menciones se guarden en una tabla: leer el
   * documento no contesta esta pregunta, porque habría que abrir todos.
   */
  quienMenciona(tipo: string, entidadId: string) {
    return this.api.get<MentioningDocument[]>(`/docs/mentions/${tipo}/${entidadId}`);
  }
}
