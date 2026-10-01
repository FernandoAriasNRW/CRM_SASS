import { Component, OnInit, computed, inject, input } from '@angular/core';

import { TagsService } from '../services/tags.service';

/**
 * Las etiquetas de un elemento, sólo para verlas: un punto de su color y el nombre. Para tarjetas
 * y celdas de tabla, donde el selector completo (`app-tag-field`) no cabe.
 *
 * Un id que ya no existe no se pinta: una etiqueta borrada se suelta de todo, pero una lista que
 * llegó antes del borrado puede traerlo todavía.
 */
@Component({
  selector: 'app-tag-chips',
  standalone: true,
  template: `
    <span class="inline-flex flex-wrap gap-1">
      @for (tag of tags(); track tag.id) {
        <span class="text-[11px] px-1.5 py-0.5 rounded-full border border-border bg-muted/40 inline-flex items-center gap-1" data-testid="tag-chip">
          <span class="w-1.5 h-1.5 rounded-full shrink-0" [style.background-color]="tag.colorHex" aria-hidden="true"></span>
          {{ tag.name }}
        </span>
      } @empty {
        @if (showEmpty()) {
          <span class="text-[12px] text-muted-foreground" i18n>Sin etiquetas</span>
        }
      }
    </span>
  `,
})
export class TagChipsComponent implements OnInit {
  private readonly tagsService = inject(TagsService);

  readonly tagIds = input<readonly string[] | null | undefined>([]);
  /** Si se dice «Sin etiquetas» cuando no tiene ninguna, o no se pinta nada. */
  readonly showEmpty = input(false);

  readonly tags = computed(() => this.tagsService.resolve(this.tagIds() ?? []));

  ngOnInit(): void {
    this.tagsService.load();
  }
}
