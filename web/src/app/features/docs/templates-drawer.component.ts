import { Component, computed, input, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import * as lucide from '@ng-icons/lucide';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import type { AvailableTemplate } from './templates';

/**
 * Todas las plantillas disponibles, en un cajón.
 *
 * Sustituye al modal «Apply a Template», que enseñaba lo mismo en dos rejillas separadas y en un
 * cuadro centrado. Los formularios y las listas largas del resto de los módulos son cajones, y
 * uno abierto deja ver detrás en qué documento se estaba.
 *
 * Como el modal al que sustituye, <b>no crea nada</b>: sólo dice qué se ha elegido. Crear implica
 * recargar el listado y abrir el documento nuevo, y eso es del padre.
 */
@Component({
  selector: 'app-templates-drawer',
  standalone: true,
  imports: [NgIcon, DrawerComponent],
  providers: [provideIcons(lucide as unknown as Record<string, string>)],
  template: `
    <app-drawer
      [isOpen]="isOpen()"
      [title]="title"
      [subtitle]="subtitle()"
      size="xl"
      [showFooter]="false"
      (closed)="closed.emit()">

      <div drawer-icon class="w-9 h-9 rounded-lg bg-primary-subtle text-primary-subtle-fg flex items-center justify-center">
        <ng-icon name="lucideWand2" class="w-4 h-4" aria-hidden="true" />
      </div>

      <div class="space-y-5">
        <label class="relative block">
          <span class="sr-only" i18n>Buscar plantilla</span>
          <ng-icon name="lucideSearch"
                   class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground"
                   aria-hidden="true" />
          <input type="search" [value]="searchText()" (input)="onSearch($event)"
                 i18n-placeholder placeholder="Buscar plantilla"
                 class="w-full h-10 pl-9 pr-3 rounded-lg border border-input bg-background text-sm
                        focus:outline-none focus:ring-2 focus:ring-ring" />
        </label>

        @for (group of groups(); track group.title) {
          <section>
            <h3 class="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
              {{ group.title }}
            </h3>

            <!-- Botones nativos y no divs con (click): son acciones, así que reciben foco y
                 responden a Enter sin añadir nada. -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              @for (template of group.templates; track template.key) {
                <button type="button" (click)="choose.emit(template)"
                        class="text-left border {{ template.border }} rounded-xl p-3.5 bg-card transition-all
                               hover:shadow-md focus:outline-none focus:ring-2 focus:ring-ring">
                  <div class="flex items-center gap-2.5 mb-1.5">
                    <div class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 {{ template.iconBackground }}">
                      <ng-icon [name]="template.icon" class="w-4 h-4" aria-hidden="true" />
                    </div>
                    <h4 class="text-sm font-semibold text-foreground truncate">{{ template.title }}</h4>
                  </div>
                  <p class="text-xs text-muted-foreground line-clamp-2">{{ template.description }}</p>
                  @if (template.count > 0) {
                    <p class="text-[11px] text-muted-foreground mt-2">{{ usageLabel(template.count) }}</p>
                  }
                </button>
              }
            </div>
          </section>
        } @empty {
          <p class="text-sm text-muted-foreground py-8 text-center" i18n>
            Ninguna plantilla coincide con la búsqueda.
          </p>
        }
      </div>
    </app-drawer>
  `,
})
export class TemplatesDrawerComponent {
  readonly isOpen = input.required<boolean>();
  readonly templates = input.required<readonly AvailableTemplate[]>();

  readonly closed = output<void>();
  readonly choose = output<AvailableTemplate>();

  protected readonly title = $localize`Plantillas`;
  protected readonly searchText = signal('');

  protected readonly matching = computed(() => {
    const text = this.searchText().trim().toLowerCase();
    if (!text) return this.templates();

    return this.templates().filter(p =>
      p.title.toLowerCase().includes(text) || p.description.toLowerCase().includes(text));
  });

  /**
   * Se agrupan en el cajón aunque la galería las mezcle.
   *
   * Fuera importa cuál se usa más; aquí, con la lista entera delante, importa de dónde sale cada
   * una: una plantilla del equipo se puede editar y borrar, y una del sistema no.
   */
  protected readonly groups = computed(() => {
    const all = this.matching();
    const custom = all.filter(p => p.isCustom);
    const builtIn = all.filter(p => !p.isCustom);

    return [
      { title: $localize`Del sistema`, templates: builtIn },
      { title: $localize`Mis plantillas`, templates: custom }
    ].filter(g => g.templates.length > 0);
  });

  protected subtitle(): string {
    const total = this.templates().length;
    return $localize`${total} plantillas disponibles`;
  }

  protected usageLabel(count: number): string {
    return count === 1 ? $localize`Usada 1 vez` : $localize`Usada ${count} veces`;
  }

  protected onSearch(event: Event): void {
    this.searchText.set((event.target as HTMLInputElement).value);
  }
}
