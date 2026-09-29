import { Injectable, computed, inject, signal } from '@angular/core';

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
  canManage?: boolean;
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

  private loading = false;

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
    if ((this.loaded() && !force) || this.loading) return;

    this.loading = true;
    this.api.get<TagItem[]>(`/tags?language=${this.language}`).subscribe({
      next: tags => {
        // Sólo una lista. Si llega otra cosa —una API simulada que contesta `{items: []}`, un
        // proxy que devuelve una página de error—, recorrerla en `groups` revienta la detección
        // de cambios y deja a medio pintar la ficha entera, no sólo las etiquetas.
        this.tags.set(Array.isArray(tags) ? tags : []);
        this.loaded.set(true);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  /** Las etiquetas de esos ids que existen, en el orden de los ids. Un id que ya no existe se omite. */
  resolve(ids: readonly string[]): TagItem[] {
    const byId = new Map(this.tags().map(tag => [tag.id, tag]));
    return ids.map(id => byId.get(id)).filter((tag): tag is TagItem => tag !== undefined);
  }
}
