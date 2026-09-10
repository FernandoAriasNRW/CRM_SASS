import { Component, computed, input, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import * as lucide from '@ng-icons/lucide';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import type { PlantillaDisponible } from './plantillas';

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
  selector: 'app-plantillas-drawer',
  standalone: true,
  imports: [NgIcon, DrawerComponent],
  providers: [provideIcons(lucide as unknown as Record<string, string>)],
  template: `
    <app-drawer
      [isOpen]="abierto()"
      [title]="titulo"
      [subtitle]="subtitulo()"
      size="xl"
      [showFooter]="false"
      (closed)="cerrar.emit()">

      <div drawer-icon class="w-9 h-9 rounded-lg bg-primary-subtle text-primary-subtle-fg flex items-center justify-center">
        <ng-icon name="lucideWand2" class="w-4 h-4" aria-hidden="true" />
      </div>

      <div class="space-y-5">
        <label class="relative block">
          <span class="sr-only" i18n>Buscar plantilla</span>
          <ng-icon name="lucideSearch"
                   class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground"
                   aria-hidden="true" />
          <input type="search" [value]="busqueda()" (input)="alBuscar($event)"
                 i18n-placeholder placeholder="Buscar plantilla"
                 class="w-full h-10 pl-9 pr-3 rounded-lg border border-input bg-background text-sm
                        focus:outline-none focus:ring-2 focus:ring-ring" />
        </label>

        @for (grupo of grupos(); track grupo.titulo) {
          <section>
            <h3 class="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
              {{ grupo.titulo }}
            </h3>

            <!-- Botones nativos y no divs con (click): son acciones, así que reciben foco y
                 responden a Enter sin añadir nada. -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              @for (plantilla of grupo.plantillas; track plantilla.clave) {
                <button type="button" (click)="elegir.emit(plantilla)"
                        class="text-left border {{ plantilla.borde }} rounded-xl p-3.5 bg-card transition-all
                               hover:shadow-md focus:outline-none focus:ring-2 focus:ring-ring">
                  <div class="flex items-center gap-2.5 mb-1.5">
                    <div class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 {{ plantilla.iconoFondo }}">
                      <ng-icon [name]="plantilla.icono" class="w-4 h-4" aria-hidden="true" />
                    </div>
                    <h4 class="text-sm font-semibold text-foreground truncate">{{ plantilla.titulo }}</h4>
                  </div>
                  <p class="text-xs text-muted-foreground line-clamp-2">{{ plantilla.descripcion }}</p>
                  @if (plantilla.veces > 0) {
                    <p class="text-[11px] text-muted-foreground mt-2">{{ vecesUsada(plantilla.veces) }}</p>
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
export class PlantillasDrawerComponent {
  readonly abierto = input.required<boolean>();
  readonly plantillas = input.required<readonly PlantillaDisponible[]>();

  readonly cerrar = output<void>();
  readonly elegir = output<PlantillaDisponible>();

  protected readonly titulo = $localize`Plantillas`;
  protected readonly busqueda = signal('');

  protected readonly coincidentes = computed(() => {
    const texto = this.busqueda().trim().toLowerCase();
    if (!texto) return this.plantillas();

    return this.plantillas().filter(p =>
      p.titulo.toLowerCase().includes(texto) || p.descripcion.toLowerCase().includes(texto));
  });

  /**
   * Se agrupan en el cajón aunque la galería las mezcle.
   *
   * Fuera importa cuál se usa más; aquí, con la lista entera delante, importa de dónde sale cada
   * una: una plantilla del equipo se puede editar y borrar, y una del sistema no.
   */
  protected readonly grupos = computed(() => {
    const todas = this.coincidentes();
    const propias = todas.filter(p => p.esPropia);
    const sistema = todas.filter(p => !p.esPropia);

    return [
      { titulo: $localize`Del sistema`, plantillas: sistema },
      { titulo: $localize`Mis plantillas`, plantillas: propias }
    ].filter(g => g.plantillas.length > 0);
  });

  protected subtitulo(): string {
    const total = this.plantillas().length;
    return $localize`${total} plantillas disponibles`;
  }

  protected vecesUsada(veces: number): string {
    return veces === 1 ? $localize`Usada 1 vez` : $localize`Usada ${veces} veces`;
  }

  protected alBuscar(evento: Event): void {
    this.busqueda.set((evento.target as HTMLInputElement).value);
  }
}
