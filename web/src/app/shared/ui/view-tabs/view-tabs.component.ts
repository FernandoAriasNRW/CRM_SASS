import { Component, ElementRef, effect, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideChartColumn, lucideChartGantt, lucideCheck, lucideLayoutDashboard,
  lucideList, lucidePlus, lucideTrash2, lucideX
} from '@ng-icons/lucide';

import type { SavedView } from '../../services/views.service';

/**
 * Una forma de ver la lista que trae el módulo de fábrica: tablero, lista, Gantt, carga.
 *
 * Se declaran con clave e icono en cada módulo porque no todos tienen las mismas: tickets tiene
 * dos y tareas cuatro.
 */
export interface BuiltInView {
  key: string;
  label: string;
  icon: string;
}

/**
 * La barra de pestañas de una lista: las vistas de fábrica, las guardadas, y cómo crear y borrar.
 *
 * <b>Existía dos veces, copiada, y las dos copias tenían el mismo fallo.</b> Tanto en tickets como
 * en tareas las pestañas de fábrica estaban dentro de un <code>&#64;if (savedViews().length === 0)</code>:
 * en cuanto se guardaba una vista —o el sembrador creaba una— <b>desaparecían</b>. En tickets eso
 * dejaba sin manera de ver la lista, que es justo lo que se pedía; en tareas se llevaba por
 * delante también el Gantt y la carga de trabajo. Y como nadie llamaba nunca a borrar vistas, no
 * había forma de volver atrás: se quedaba así para siempre.
 *
 * <b>Las de fábrica se enseñan siempre.</b> Una guardada es un atajo a un estado, no un sustituto
 * de las formas de ver que el módulo sabe pintar; esconder unas al aparecer las otras mezcla dos
 * cosas que no son la misma.
 *
 * El nombre de la vista nueva se pide <b>aquí dentro</b> y no con <code>prompt()</code>, que es lo
 * que había: el navegador lo bloquea en cuanto la página va dentro de un marco, y entonces crear
 * una vista no hace nada y tampoco avisa.
 */
@Component({
  selector: 'app-view-tabs',
  standalone: true,
  imports: [FormsModule, NgIcon],
  viewProviders: [provideIcons({
    lucideChartColumn, lucideChartGantt, lucideCheck, lucideLayoutDashboard,
    lucideList, lucidePlus, lucideTrash2, lucideX
  })],
  template: `
    <div class="flex items-center px-2 overflow-x-auto">
      <!-- Las de fábrica: siempre, tenga o no vistas guardadas. -->
      @for (view of builtIn(); track view.key) {
        <button
          type="button"
          (click)="changeMode.emit(view.key)"
          [class.border-primary]="!activeViewId() && mode() === view.key"
          [class.text-primary]="!activeViewId() && mode() === view.key"
          [class.border-transparent]="activeViewId() || mode() !== view.key"
          class="flex items-center gap-2 px-4 py-2.5 border-b-2 font-medium text-sm whitespace-nowrap
                 cursor-pointer transition-colors hover:text-foreground">
          <ng-icon [name]="view.icon" size="14" /> {{ view.label }}
        </button>
      }

      @if (saved().length > 0) {
        <span class="mx-2 h-5 w-px bg-border shrink-0"></span>
      }

      @for (view of saved(); track view.id) {
        <div class="group relative flex items-center">
          <button
            type="button"
            (click)="apply.emit(view)"
            [class.border-primary]="activeViewId() === view.id"
            [class.text-primary]="activeViewId() === view.id"
            [class.border-transparent]="activeViewId() !== view.id"
            class="flex items-center gap-2 pl-4 pr-7 py-2.5 border-b-2 font-medium text-sm whitespace-nowrap
                   cursor-pointer transition-colors hover:text-foreground">
            <ng-icon [name]="iconOf(view)" size="14" />
            {{ view.viewName }}
          </button>

          <!--
            El aspa aparece al pasar por encima y también al tabular: escondida tras el ratón
            sería inalcanzable con el teclado, y sin poder borrar una vista mal creada la barra se
            va llenando de intentos.
          -->
          <button
            type="button"
            (click)="requestDelete(view)"
            class="absolute right-1 p-1 rounded text-muted-foreground opacity-0 transition-opacity
                   group-hover:opacity-100 focus:opacity-100 hover:text-destructive
                   focus:outline-none focus:ring-2 focus:ring-ring"
            i18n-title [title]="'Borrar la vista ' + view.viewName">
            <ng-icon name="lucideTrash2" size="12" />
          </button>
        </div>
      }

      <!-- Crear una nueva -->
      @if (creating()) {
        <div class="flex items-center gap-1.5 px-3 py-1.5">
          <input
            #field
            [(ngModel)]="newName"
            (keydown.enter)="confirmCreate()"
            (keydown.escape)="cancelCreate()"
            i18n-placeholder placeholder="Nombre de la vista"
            class="h-8 w-44 rounded-md border border-border bg-background px-2 text-sm
                   focus:outline-none focus:ring-2 focus:ring-ring" />

          <select
            [(ngModel)]="newType"
            i18n-aria-label aria-label="Cómo se verá"
            class="h-8 rounded-md border border-border bg-background px-1.5 text-sm
                   focus:outline-none focus:ring-2 focus:ring-ring">
            @for (view of builtIn(); track view.key) {
              <option [value]="view.key">{{ view.label }}</option>
            }
          </select>

          <button type="button" (click)="confirmCreate()"
            [disabled]="!newName.trim()"
            class="p-1.5 rounded-md text-primary hover:bg-accent disabled:opacity-40
                   disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-ring"
            i18n-title title="Guardar la vista">
            <ng-icon name="lucideCheck" size="14" />
          </button>

          <button type="button" (click)="cancelCreate()"
            class="p-1.5 rounded-md text-muted-foreground hover:bg-accent
                   focus:outline-none focus:ring-2 focus:ring-ring"
            i18n-title title="Cancelar">
            <ng-icon name="lucideX" size="14" />
          </button>
        </div>
      } @else {
        <button type="button" (click)="startCreate()"
          class="flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium text-muted-foreground
                 hover:text-foreground transition-colors focus:outline-none focus:ring-2 focus:ring-ring rounded-md">
          <ng-icon name="lucidePlus" size="14" /> <span i18n>Vista</span>
        </button>
      }
    </div>
  `
})
export class ViewTabsComponent {
  /** Las formas de ver que el módulo sabe pintar, en el orden en que se enseñan. */
  readonly builtIn = input.required<BuiltInView[]>();

  /** Cuál de las de fábrica está puesta. */
  readonly mode = input.required<string>();

  readonly saved = input.required<SavedView[]>();

  /** La vista guardada activa, o nulo si se está en una de fábrica. */
  readonly activeViewId = input<string | null>(null);

  readonly changeMode = output<string>();
  readonly apply = output<SavedView>();
  readonly create = output<{ name: string; type: string }>();
  readonly remove = output<SavedView>();

  readonly creating = signal(false);
  newName = '';
  newType = '';

  private readonly field = viewChild<ElementRef<HTMLInputElement>>('field');

  constructor() {
    /*
     * El foco se pone desde aquí y no con `autofocus`.
     *
     * `autofocus` sólo actúa al cargar la página, así que en un campo que aparece al pulsar no
     * hace nada —y el lint lo prohíbe por accesibilidad: roba el foco sin que nadie lo pida—.
     * Aquí es al revés: acabas de pulsar «Vista» para escribir un nombre, así que llevarte el
     * cursor allí es exactamente lo que esperas.
     */
    effect(() => {
      if (this.creating()) this.field()?.nativeElement.focus();
    });
  }

  /**
   * El icono sale del estado guardado, no del nombre: una vista llamada «Urgentes» puede ser un
   * tablero, y adivinarlo por el texto acertaría unas veces sí y otras no.
   */
  iconOf(view: SavedView): string {
    try {
      const state = JSON.parse(view.stateJson) as { viewType?: string };
      return this.builtIn().find(v => v.key === state.viewType)?.icon ?? 'lucideList';
    } catch {
      // Un estado ilegible no debe romper la barra: se pinta como lista y la vista sigue ahí para
      // poder borrarla, que es lo único sensato que se puede hacer con ella.
      return 'lucideList';
    }
  }

  startCreate(): void {
    this.newName = '';
    // Se propone la forma que se está viendo: quien pulsa «Vista» estando en el tablero casi
    // siempre quiere guardar ese tablero.
    this.newType = this.mode();
    this.creating.set(true);
  }

  confirmCreate(): void {
    const name = this.newName.trim();
    if (!name) return;

    this.create.emit({ name, type: this.newType });
    this.creating.set(false);
  }

  cancelCreate(): void {
    this.creating.set(false);
  }

  /**
   * Se confirma antes de borrar. Es la única acción de la barra que destruye algo, y las
   * pestañas están pegadas: un aspa a un centímetro de la vista que quieres abrir se pulsa sola.
   */
  requestDelete(view: SavedView): void {
    if (confirm(`¿Borrar la vista «${view.viewName}»?`)) {
      this.remove.emit(view);
    }
  }
}
