import { Component, computed, inject, input, model } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArchive, lucideCalendarDays, lucideFileText, lucideFolderKanban, lucideLayoutTemplate,
  lucideList, lucideLock, lucidePenLine, lucidePin, lucidePinOff, lucideShare2, lucideSquareCheck,
  lucideStar, lucideTicket, lucideTrash2, lucideUser, lucideUsers
} from '@ng-icons/lucide';

import { MenuEntry, ModuleVocabulary, vocabularyOf } from './menu-vocabulary';
import { PanelSectionsService } from './panel-sections.service';

/**
 * El panel lateral de navegación: <b>el único submenú de la aplicación</b>.
 *
 * <b>Llegó a haber dos, y no se parecían.</b> La barra lateral sacaba su propio desplegable al
 * pasar el ratón —«Listar todos», «Mis tickets», «Tickets del Team», «Crear / Agregar»— mientras
 * la pantalla enseñaba este panel con otras entradas. Dos listas distintas para lo mismo, una al
 * lado de la otra.
 *
 * Y el otro era además el malo: «del Team» mandaba <code>?filter=team</code>, que el servidor no
 * conoce —devuelve la lista entera—, y «Crear / Agregar» iba a <code>/tickets/new</code>, que no
 * es ninguna ruta: te dejaba en Home sin decir nada. Es exactamente lo que este panel vino a
 * quitar; ver <code>vocabulario-del-menu.ts</code>.
 *
 * <b>Ahora vive en el armazón de la aplicación</b>, no dentro de cada pantalla. Eso arregla dos
 * cosas de golpe: deja de haber dos submenús, y el panel deja de encogerse con el contenido —al
 * estar dentro de la vista, con el Gantt o la carga de trabajo se quedaba en un palmo de alto—.
 *
 * El estado vive en la URL (`?filter=`), no en el componente: así una vista filtrada se comparte
 * por enlace, el botón de atrás funciona y recargar no devuelve a «ver todo».
 */
@Component({
  selector: 'app-navigation-panel',
  standalone: true,
  imports: [CommonModule, NgIcon],
  viewProviders: [provideIcons({
    lucideArchive, lucideCalendarDays, lucideFileText, lucideFolderKanban, lucideLayoutTemplate,
    lucideList, lucideLock, lucidePenLine, lucidePin, lucidePinOff, lucideShare2, lucideSquareCheck,
    lucideStar, lucideTicket, lucideTrash2, lucideUser, lucideUsers
  })],
  template: `
    <div
      class="h-full w-64 flex-shrink-0 bg-muted border-r border-border flex flex-col shadow-xl"
      [class.shadow-none]="pinned()">

      <div class="h-14 flex items-center justify-between px-4 border-b border-border shrink-0">
        <div class="flex items-center gap-2 font-medium text-sm text-foreground">
          <div class="w-6 h-6 rounded-md bg-gradient-to-tr from-blue-600 to-indigo-600 flex items-center justify-center text-white text-xs font-bold shadow-sm">
            {{ vocabulary().initial }}
          </div>
          <span class="font-semibold tracking-tight">{{ vocabulary().title }}</span>
        </div>

        <button
          type="button"
          (click)="togglePinned($event)"
          class="text-muted-foreground hover:text-foreground p-1.5 rounded-md hover:bg-secondary transition-colors"
          [attr.aria-pressed]="pinned()"
          i18n-aria-label aria-label="Fijar el panel de navegación">
          <ng-icon [name]="pinned() ? 'lucidePinOff' : 'lucidePin'" class="w-3.5 h-3.5" />
        </button>
      </div>

      <nav class="px-2 py-3 space-y-0.5 overflow-y-auto flex-1" i18n-aria-label aria-label="Vistas">
        @for (entry of vocabulary().entries; track entry.label) {
          @if (entry.separatorBefore) {
            <div class="my-2 border-t border-border/80"></div>
          }

          <button
            type="button"
            (click)="go(entry)"
            [attr.aria-current]="isActive(entry) ? 'page' : null"
            [class.bg-secondary]="isActive(entry)"
            [class.font-semibold]="isActive(entry)"
            [class.text-foreground]="isActive(entry)"
            class="w-full flex items-center gap-2 px-2.5 py-1.5 text-xs text-muted-foreground hover:bg-secondary/70 rounded-md transition-colors text-left">
            <ng-icon [name]="entry.icon" class="w-4 h-4 flex-shrink-0" />
            <span class="truncate">{{ entry.label }}</span>
          </button>
        }
      </nav>

      <!--
        Lo que aporta el propio módulo: los favoritos y las páginas recientes de Documentos, por
        ejemplo. Va debajo de la navegación y separado, porque son datos y no destinos fijos.
      -->
      @if (sections().length > 0) {
        <div class="px-2 pb-3 space-y-4 overflow-y-auto border-t border-border/80 pt-3">
          @for (section of sections(); track section.title) {
            <div>
              <span class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider block mb-1.5 px-1">
                {{ section.title }}
              </span>

              @if (section.items.length > 0) {
                <div class="space-y-0.5">
                  @for (item of section.items; track item.id) {
                    <button
                      type="button"
                      (click)="item.onSelect()"
                      class="w-full flex items-center gap-2 px-2.5 py-1.5 text-xs text-muted-foreground
                             hover:bg-secondary/70 rounded-md transition-colors text-left
                             focus:outline-none focus:ring-2 focus:ring-ring">
                      @if (item.icon) {
                        <ng-icon [name]="item.icon" class="w-3.5 h-3.5 flex-shrink-0" />
                      }
                      <span class="truncate">{{ item.label }}</span>
                    </button>
                  }
                </div>
              } @else if (section.fallback) {
                <p class="px-1 text-[11px] text-muted-foreground">{{ section.fallback }}</p>
              }
            </div>
          }
        </div>
      }
    </div>
  `
})
export class NavigationPanelComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly moduleSections = inject(PanelSectionsService);

  /** El módulo cuyo vocabulario se pinta. Cualquiera de los del menú lateral. */
  readonly moduleKey = input.required<string>();

  /**
   * El nombre del módulo según el menú lateral.
   *
   * Sólo se usa para los módulos que aún no tienen vocabulario escrito: así uno nuevo enseña su
   * nombre de verdad en la cabecera del panel en vez de la clave de la ruta.
   */
  readonly moduleName = input<string | undefined>(undefined);

  /**
   * La ruta del módulo, cuando no coincide con su identificador.
   *
   * Inicio es `home` y su ruta es `/`. Suponer `/${modulo}` llevaría a `/home`, que no existe.
   */
  readonly moduleRoute = input<string | undefined>(undefined);

  /**
   * Anclado: se queda abierto y el contenido se corre a la derecha. Sin anclar, se asoma al pasar
   * el ratón por la barra y se recoge al salir.
   *
   * Es una preferencia por persona y todavía no se guarda, así que arranca abierto: un panel que
   * empieza escondido en una pantalla nueva es un panel que nadie descubre.
   */
  readonly pinned = model(true);

  /**
   * El módulo de la pantalla en la que se está.
   *
   * Se recibe de fuera en vez de leer <code>router.url</code> aquí por dos razones: leerlo a mano
   * no reacciona a los cambios de ruta —haría falta suscribirse—, y quien pinta el panel ya lo
   * sabe. Vale nulo cuando la pantalla actual no tiene panel.
   */
  readonly currentModule = input<string | null>(null);

  private readonly params = toSignal(this.route.queryParams, { initialValue: {} as Record<string, string> });

  /** El filtro que está puesto ahora mismo, leído de la URL. */
  readonly activeFilter = computed(() => this.params()['filter'] ?? null);

  readonly vocabulary = computed<ModuleVocabulary>(
    () => vocabularyOf(this.moduleKey(), this.moduleName()));

  /** Las secciones que el módulo haya registrado. Vacío para casi todos. */
  readonly sections = computed(() => this.moduleSections.all()[this.moduleKey()] ?? []);

  /**
   * Si una entrada está puesta.
   *
   * Sólo cuenta cuando el panel enseña el módulo en el que se está: asomando el de Tickets desde
   * la pantalla de Tareas, el filtro de la URL es de Tareas y marcar la entrada haría creer que
   * los tickets ya están filtrados así.
   */
  isActive(entry: MenuEntry): boolean {
    if (!this.isCurrentModule()) return false;

    // Una entrada con ruta propia —«Mis tareas» desde Inicio— nunca es «la activa» del panel de
    // Inicio: lleva a otra pantalla, así que si estuviera marcada diría que estás en ella.
    if (entry.route) return false;

    // Los parámetros que la entrada fija tienen que coincidir todos. Es lo que distingue las
    // pestañas de Documentos entre sí, que no usan `?filter=`.
    for (const [key, value] of Object.entries(entry.params ?? {})) {
      if (this.params()[key] !== value) return false;
    }

    // Y los que no fija no pueden estar puestos, o «Todos los documentos» saldría marcado
    // estando en «Privados».
    if (!entry.params && this.params()['tab']) return false;

    return (entry.filter ?? null) === this.activeFilter();
  }

  private isCurrentModule(): boolean {
    return this.currentModule() === this.moduleKey();
  }

  /**
   * Navega al módulo del panel con el filtro elegido.
   *
   * Se navega a la ruta del módulo y no relativo a la actual porque el panel puede estar
   * enseñando otro: al asomarlo desde la barra sobre Tickets estando en Tareas, «Favoritos» tiene
   * que llevar a los tickets favoritos, no a las tareas favoritas.
   *
   * «Ver todo» quita el parámetro en vez de mandarlo vacío, para que la URL de la lista completa
   * siga siendo la de siempre y no una variante que parezca filtrada.
   */
  go(entry: MenuEntry): void {
    const target = entry.route ?? this.moduleRoute() ?? '/' + this.moduleKey();

    this.router.navigate([target], {
      queryParams: { filter: entry.filter, tab: null, type: null, ...entry.params }
    });
  }

  togglePinned(event: Event): void {
    event.stopPropagation();
    this.pinned.update(v => !v);
  }
}
