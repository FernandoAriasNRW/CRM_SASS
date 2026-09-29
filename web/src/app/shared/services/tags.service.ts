import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { IdiomaService } from '../../core/idioma.service';

/** Una etiqueta de la organización, tal como la devuelve `GET /tags`. */
export interface TagItem {
  id: string;
  /** Ya en el idioma de la pantalla si es predefinida. */
  name: string;
  colorHex: string;
  /** El valor guardado («WorkType»); `categoryLabel` es cómo se muestra. */
  category: string;
  categoryLabel: string;
  builtInKey?: string | null;
  /** Quién la creó; `null` en las que crea el sistema (predefinidas, de equipos y proyectos). */
  createdBy?: string | null;
  /** Si quien mira puede editarla y borrarla. Lo decide el servidor. */
  canManage?: boolean;
}

/** Una categoría de etiquetas, tal como la devuelve `GET /tags/categories`. */
export interface TagCategoryItem {
  /** Sólo las propias de la organización tienen id. */
  id?: string | null;
  /** El valor que se guarda en la etiqueta y se manda al crearla. */
  name: string;
  /** Cómo se muestra, ya en el idioma de la pantalla. */
  label: string;
  isCustom: boolean;
  /** Las de equipos y proyectos: las rellena el sistema y no admiten etiquetas a mano. */
  isAutomatic: boolean;
}

/** Lo que se manda al crear o editar una etiqueta. */
export interface TagInput {
  name: string;
  colorHex?: string | null;
  category: string;
}

/** Las etiquetas de una categoría, para pintarlas agrupadas. */
export interface TagGroup {
  category: string;
  label: string;
  tags: TagItem[];
}

/**
 * Las etiquetas de la organización, cargadas una vez y compartidas por todas las fichas.
 *
 * Se piden en el idioma con el que se compiló la aplicación (`?language=`), no con el
 * `Accept-Language` del navegador: las predefinidas vienen así ya traducidas, y las que creó la
 * organización salen como las llamaron.
 */
@Injectable({ providedIn: 'root' })
export class TagsService {
  private readonly api = inject(ApiService);
  private readonly language = inject(IdiomaService).actual;

  readonly tags = signal<TagItem[]>([]);
  readonly loaded = signal(false);
  readonly categories = signal<TagCategoryItem[]>([]);

  private loading = false;
  private reloadAfter = false;

  /** Por categoría, en el orden en que llegan (predefinidas primero, como las devuelve el servidor). */
  readonly groups = computed<TagGroup[]>(() => {
    const groups = new Map<string, TagGroup>();
    for (const tag of this.tags()) {
      const group = groups.get(tag.category) ?? { category: tag.category, label: tag.categoryLabel, tags: [] };
      group.tags.push(tag);
      groups.set(tag.category, group);
    }
    return [...groups.values()];
  });

  /** Carga la lista si todavía no está. `force` la vuelve a pedir, por si alguien creó una. */
  load(force = false): void {
    if (this.loaded() && !force) return;
    if (this.loading) {
      // Una escritura mientras se cargaba: la lista que está llegando ya puede estar vieja.
      this.reloadAfter ||= force;
      return;
    }

    this.loading = true;
    this.api.get<TagItem[]>(`/tags?language=${this.language}`).subscribe({
      next: tags => {
        // Sólo una lista. Si llega otra cosa —una API simulada que contesta `{items: []}`, un
        // proxy que devuelve una página de error—, recorrerla en `groups` revienta la detección
        // de cambios y deja a medio pintar la ficha entera, no sólo las etiquetas.
        this.tags.set(Array.isArray(tags) ? tags : []);
        this.loaded.set(true);
        this.finishLoading();
      },
      error: () => this.finishLoading(),
    });
  }

  private finishLoading(): void {
    this.loading = false;
    if (this.reloadAfter) {
      this.reloadAfter = false;
      this.load(true);
    }
  }

  loadCategories(): void {
    this.api.get<TagCategoryItem[]>(`/tags/categories?language=${this.language}`).subscribe({
      next: categories => this.categories.set(Array.isArray(categories) ? categories : []),
      // Sin categorías el desplegable del alta sale vacío y no se puede guardar: mejor que
      // enseñar las de una respuesta anterior como si fueran las de ahora.
      error: () => this.categories.set([]),
    });
  }

  /**
   * Las escrituras vuelven a pedir la lista al terminar, y no la retocan a mano: el servidor
   * traduce los nombres, calcula quién puede gestionar cada una y ordena, y repetir eso aquí
   * sería una segunda versión de las reglas que acabaría discrepando. Las fichas de tarea y de
   * ticket comparten esta lista, así que ven el cambio sin recargar.
   */
  create(input: TagInput): Observable<TagItem> {
    return this.api.post<TagItem>('/tags', input, { sinAviso: true }).pipe(tap(() => this.load(true)));
  }

  update(id: string, input: TagInput): Observable<TagItem> {
    return this.api.put<TagItem>(`/tags/${id}`, input, { sinAviso: true }).pipe(tap(() => this.load(true)));
  }

  remove(id: string): Observable<void> {
    return this.api.delete<void>(`/tags/${id}`, { sinAviso: true }).pipe(tap(() => this.load(true)));
  }

  createCategory(name: string): Observable<TagCategoryItem> {
    return this.api.post<TagCategoryItem>('/tags/categories', { name }, { sinAviso: true })
      .pipe(tap(() => this.loadCategories()));
  }

  /** Las etiquetas de esos ids que existen, en el orden de los ids. Un id que ya no existe se omite. */
  resolve(ids: readonly string[]): TagItem[] {
    const byId = new Map(this.tags().map(tag => [tag.id, tag]));
    return ids.map(id => byId.get(id)).filter((tag): tag is TagItem => tag !== undefined);
  }
}
