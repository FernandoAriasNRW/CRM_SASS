import { Component, computed, inject, input, model, signal } from '@angular/core';
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
 * El panel lateral de navegación, compartido por todas las vistas de lista.
 *
 * **Existía dos veces y no se parecían.** Había un desplegable que salía al pasar el ratón por
 * un icono de la barra lateral, y el panel propio de Docs —ancho, anclable, con sitio para un
 * árbol—. El de Docs era el bueno, así que es el que se extrae aquí.
 *
 * El estado vive en la URL (`?filter=`), no en el componente. Es lo que hace que una vista
 * filtrada se pueda compartir por enlace, que el botón de atrás funcione y que recargar no
 * devuelva a «ver todo». Además, las vistas ya leían ese parámetro: el panel no tuvo que
 * enseñarles nada nuevo.
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
      class="absolute top-0 bottom-0 left-0 z-30 flex h-full"
      [class.w-64]="anclado() || encima()"
      [class.w-4]="!anclado() && !encima()"
      (mouseenter)="encima.set(true)"
      (mouseleave)="encima.set(false)">

      <div
        class="w-64 flex-shrink-0 bg-muted border-r border-border flex flex-col h-full shadow-xl transition-transform duration-300 ease-in-out"
        [class.-translate-x-full]="!anclado() && !encima()"
        [class.shadow-none]="anclado()">

        <div class="h-14 flex items-center justify-between px-4 border-b border-border">
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

        <nav class="px-2 py-3 space-y-0.5 overflow-y-auto" i18n-aria-label aria-label="Vistas">
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
    </div>
  `
})
export class PanelDeNavegacionComponent {
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);

  /** El módulo cuyo vocabulario se pinta: `tasks`, `tickets` o `projects`. */
  readonly modulo = input.required<string>();

  /**
   * Anclado de serie.
   *
   * Es una preferencia por persona y todavía no se guarda —las preferencias de barra lateral que
   * ya existen son de otra cosa—, así que arranca abierto: un panel que empieza escondido en una
   * pantalla nueva es un panel que nadie descubre.
   */
  readonly anclado = model(true);
  readonly encima = signal(false);

  private readonly parametros = toSignal(this.ruta.queryParams, { initialValue: {} as Record<string, string> });

  /** El filtro que está puesto ahora mismo, leído de la URL. */
  readonly filtroActivo = computed(() => this.parametros()['filter'] ?? null);

  readonly vocabulario = computed<VocabularioDeModulo>(
    () => VOCABULARIO[this.modulo()] ?? { titulo: this.modulo(), inicial: '·', entradas: [] });

  esLaActiva(entrada: EntradaDeMenu): boolean {
    return (entrada.filtro ?? null) === this.filtroActivo();
  }

  ir(entrada: EntradaDeMenu): void {
    // «Ver todo» quita el parámetro en vez de mandarlo vacío, para que la URL de la lista
    // completa siga siendo la de siempre y no una variante que parezca filtrada.
    this.router.navigate([], {
      relativeTo: this.ruta,
      queryParams: { filter: entrada.filtro },
      queryParamsHandling: 'merge'
    });
  }

  alternarAnclado(evento: Event): void {
    evento.stopPropagation();
    this.anclado.update(v => !v);
  }
}
