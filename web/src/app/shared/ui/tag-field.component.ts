import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';

import { TagsService, type TagItem } from '../services/tags.service';

/**
 * Las etiquetas de un elemento: las que tiene, y un selector con todas las de la organización
 * agrupadas por categoría.
 *
 * Lo usan las fichas de tarea y de ticket. Antes cada una tenía su lista fija de claves
 * (`TASK_TAGS`, `TICKET_TAGS`) que no eran las etiquetas del módulo; la de tareas, además, no
 * llegaba a guardarse nunca.
 *
 * No guarda nada: avisa con `tagIdsChange` y quien lo usa decide cómo persistirlo.
 */
@Component({
  selector: 'app-tag-field',
  standalone: true,
  templateUrl: './tag-field.component.html',
})
export class TagFieldComponent implements OnInit {
  private readonly tagsService = inject(TagsService);

  /** Los ids de las etiquetas que tiene el elemento. */
  readonly tagIds = input<readonly string[]>([]);
  readonly tagIdsChange = output<string[]>();

  readonly pickerOpen = signal(false);

  readonly groups = this.tagsService.groups;
  readonly loaded = this.tagsService.loaded;
  readonly selected = computed<TagItem[]>(() => this.tagsService.resolve(this.tagIds()));

  ngOnInit(): void {
    this.tagsService.load();
  }

  isSelected(id: string): boolean {
    return this.tagIds().includes(id);
  }

  toggle(id: string): void {
    const current = this.tagIds();
    this.tagIdsChange.emit(current.includes(id) ? current.filter(t => t !== id) : [...current, id]);
  }

  togglePicker(): void {
    this.pickerOpen.update(open => !open);
  }

  removeLabel(name: string): string {
    return $localize`Quitar la etiqueta ${name}`;
  }
}
