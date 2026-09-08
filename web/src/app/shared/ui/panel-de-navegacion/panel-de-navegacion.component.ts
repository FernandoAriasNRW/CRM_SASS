import { Component, computed, inject, input, model } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArchive, lucideList, lucideLock, lucidePenLine, lucidePin, lucidePinOff,
  lucideShare2, lucideStar, lucideTrash2, lucideUser
} from '@ng-icons/lucide';

import { EntradaDeMenu, VOCABULARIO, VocabularioDeModulo } from './vocabulario-del-menu';

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
  selector: 'app-panel-de-navegacion',
  standalone: true,
  imports: [CommonModule, NgIcon],
  viewProviders: [provideIcons({
    lucideArchive, lucideList, lucideLock, lucidePenLine, lucidePin, lucidePinOff,
    lucideShare2, lucideStar, lucideTrash2, lucideUser
  })],
  template: `
    <div
      class="h-full w-64 flex-shrink-0 bg-muted border-r border-border flex flex-col shadow-xl"
      [class.shadow-none]="anclado()">

      <div class="h-14 flex items-center justify-between px-4 border-b border-border shrink-0">
        <div class="flex items-center gap-2 font-medium text-sm text-foreground">
          <div class="w-6 h-6 rounded-md bg-gradient-to-tr from-blue-600 to-indigo-600 flex items-center justify-center text-white text-xs font-bold shadow-sm">
            {{ vocabulario().inicial }}
          </div>
          <span class="font-semibold tracking-tight">{{ vocabulario().titulo }}</span>
        </div>

        <button
          type="button"
          (click)="alternarAnclado($event)"
          class="text-muted-foreground hover:text-foreground p-1.5 rounded-md hover:bg-secondary transition-colors"
          [attr.aria-pressed]="anclado()"
          i18n-aria-label aria-label="Fijar el panel de navegación">
          <ng-icon [name]="anclado() ? 'lucidePinOff' : 'lucidePin'" class="w-3.5 h-3.5" />
        </button>
      </div>

      <nav class="px-2 py-3 space-y-0.5 overflow-y-auto flex-1" i18n-aria-label aria-label="Vistas">
        @for (entrada of vocabulario().entradas; track entrada.etiqueta) {
          @if (entrada.separadorAntes) {
            <div class="my-2 border-t border-border/80"></div>
          }

          <button
            type="button"
            (click)="ir(entrada)"
            [attr.aria-current]="esLaActiva(entrada) ? 'page' : null"
            [class.bg-secondary]="esLaActiva(entrada)"
            [class.font-semibold]="esLaActiva(entrada)"
            [class.text-foreground]="esLaActiva(entrada)"
            class="w-full flex items-center gap-2 px-2.5 py-1.5 text-xs text-muted-foreground hover:bg-secondary/70 rounded-md transition-colors text-left">
            <ng-icon [name]="entrada.icono" class="w-4 h-4 flex-shrink-0" />
            <span class="truncate">{{ entrada.etiqueta }}</span>
          </button>
        }
      </nav>
    </div>
  `
})
export class PanelDeNavegacionComponent {
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);

  /** El módulo cuyo vocabulario se pinta: `tasks`, `tickets` o `projects`. */
  readonly modulo = input.required<string>();

  /**
   * Anclado: se queda abierto y el contenido se corre a la derecha. Sin anclar, se asoma al pasar
   * el ratón por la barra y se recoge al salir.
   *
   * Es una preferencia por persona y todavía no se guarda, así que arranca abierto: un panel que
   * empieza escondido en una pantalla nueva es un panel que nadie descubre.
   */
  readonly anclado = model(true);

  /**
   * El módulo de la pantalla en la que se está.
   *
   * Se recibe de fuera en vez de leer <code>router.url</code> aquí por dos razones: leerlo a mano
   * no reacciona a los cambios de ruta —haría falta suscribirse—, y quien pinta el panel ya lo
   * sabe. Vale nulo cuando la pantalla actual no tiene panel.
   */
  readonly moduloActual = input<string | null>(null);

  private readonly parametros = toSignal(this.ruta.queryParams, { initialValue: {} as Record<string, string> });

  /** El filtro que está puesto ahora mismo, leído de la URL. */
  readonly filtroActivo = computed(() => this.parametros()['filter'] ?? null);

  readonly vocabulario = computed<VocabularioDeModulo>(
    () => VOCABULARIO[this.modulo()] ?? { titulo: this.modulo(), inicial: '·', entradas: [] });

  /**
   * Si una entrada está puesta.
   *
   * Sólo cuenta cuando el panel enseña el módulo en el que se está: asomando el de Tickets desde
   * la pantalla de Tareas, el filtro de la URL es de Tareas y marcar la entrada haría creer que
   * los tickets ya están filtrados así.
   */
  esLaActiva(entrada: EntradaDeMenu): boolean {
    return this.esElModuloActual() && (entrada.filtro ?? null) === this.filtroActivo();
  }

  private esElModuloActual(): boolean {
    return this.moduloActual() === this.modulo();
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
  ir(entrada: EntradaDeMenu): void {
    this.router.navigate(['/' + this.modulo()], {
      queryParams: { filter: entrada.filtro }
    });
  }

  alternarAnclado(evento: Event): void {
    evento.stopPropagation();
    this.anclado.update(v => !v);
  }
}
