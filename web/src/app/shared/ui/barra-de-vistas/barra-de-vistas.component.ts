import { Component, input, output, signal } from '@angular/core';
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
export interface VistaIntegrada {
  clave: string;
  etiqueta: string;
  icono: string;
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
  selector: 'app-barra-de-vistas',
  standalone: true,
  imports: [FormsModule, NgIcon],
  viewProviders: [provideIcons({
    lucideChartColumn, lucideChartGantt, lucideCheck, lucideLayoutDashboard,
    lucideList, lucidePlus, lucideTrash2, lucideX
  })],
  template: `
    <div class="flex items-center px-2 overflow-x-auto">
      <!-- Las de fábrica: siempre, tenga o no vistas guardadas. -->
      @for (vista of integradas(); track vista.clave) {
        <button
          type="button"
          (click)="cambiarModo.emit(vista.clave)"
          [class.border-primary]="!vistaActivaId() && modo() === vista.clave"
          [class.text-primary]="!vistaActivaId() && modo() === vista.clave"
          [class.border-transparent]="vistaActivaId() || modo() !== vista.clave"
          class="flex items-center gap-2 px-4 py-2.5 border-b-2 font-medium text-sm whitespace-nowrap
                 cursor-pointer transition-colors hover:text-foreground">
          <ng-icon [name]="vista.icono" size="14" /> {{ vista.etiqueta }}
        </button>
      }

      @if (guardadas().length > 0) {
        <span class="mx-2 h-5 w-px bg-border shrink-0"></span>
      }

      @for (vista of guardadas(); track vista.id) {
        <div class="group relative flex items-center">
          <button
            type="button"
            (click)="aplicar.emit(vista)"
            [class.border-primary]="vistaActivaId() === vista.id"
            [class.text-primary]="vistaActivaId() === vista.id"
            [class.border-transparent]="vistaActivaId() !== vista.id"
            class="flex items-center gap-2 pl-4 pr-7 py-2.5 border-b-2 font-medium text-sm whitespace-nowrap
                   cursor-pointer transition-colors hover:text-foreground">
            <ng-icon [name]="iconoDe(vista)" size="14" />
            {{ vista.viewName }}
          </button>

          <!--
            El aspa aparece al pasar por encima y también al tabular: escondida tras el ratón
            sería inalcanzable con el teclado, y sin poder borrar una vista mal creada la barra se
            va llenando de intentos.
          -->
          <button
            type="button"
            (click)="pedirBorrado(vista)"
            class="absolute right-1 p-1 rounded text-muted-foreground opacity-0 transition-opacity
                   group-hover:opacity-100 focus:opacity-100 hover:text-destructive
                   focus:outline-none focus:ring-2 focus:ring-ring"
            i18n-title [title]="'Borrar la vista ' + vista.viewName">
            <ng-icon name="lucideTrash2" size="12" />
          </button>
        </div>
      }

      <!-- Crear una nueva -->
      @if (creando()) {
        <div class="flex items-center gap-1.5 px-3 py-1.5">
          <input
            #campo
            [(ngModel)]="nombreNuevo"
            (keydown.enter)="confirmarCreacion()"
            (keydown.escape)="cancelarCreacion()"
            i18n-placeholder placeholder="Nombre de la vista"
            class="h-8 w-44 rounded-md border border-border bg-background px-2 text-sm
                   focus:outline-none focus:ring-2 focus:ring-ring"
            autofocus />

          <select
            [(ngModel)]="tipoNuevo"
            i18n-aria-label aria-label="Cómo se verá"
            class="h-8 rounded-md border border-border bg-background px-1.5 text-sm
                   focus:outline-none focus:ring-2 focus:ring-ring">
            @for (vista of integradas(); track vista.clave) {
              <option [value]="vista.clave">{{ vista.etiqueta }}</option>
            }
          </select>

          <button type="button" (click)="confirmarCreacion()"
            [disabled]="!nombreNuevo.trim()"
            class="p-1.5 rounded-md text-primary hover:bg-accent disabled:opacity-40
                   disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-ring"
            i18n-title title="Guardar la vista">
            <ng-icon name="lucideCheck" size="14" />
          </button>

          <button type="button" (click)="cancelarCreacion()"
            class="p-1.5 rounded-md text-muted-foreground hover:bg-accent
                   focus:outline-none focus:ring-2 focus:ring-ring"
            i18n-title title="Cancelar">
            <ng-icon name="lucideX" size="14" />
          </button>
        </div>
      } @else {
        <button type="button" (click)="empezarCreacion()"
          class="flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium text-muted-foreground
                 hover:text-foreground transition-colors focus:outline-none focus:ring-2 focus:ring-ring rounded-md">
          <ng-icon name="lucidePlus" size="14" /> <span i18n>Vista</span>
        </button>
      }
    </div>
  `
})
export class BarraDeVistasComponent {
  /** Las formas de ver que el módulo sabe pintar, en el orden en que se enseñan. */
  readonly integradas = input.required<VistaIntegrada[]>();

  /** Cuál de las de fábrica está puesta. */
  readonly modo = input.required<string>();

  readonly guardadas = input.required<SavedView[]>();

  /** La vista guardada activa, o nulo si se está en una de fábrica. */
  readonly vistaActivaId = input<string | null>(null);

  readonly cambiarModo = output<string>();
  readonly aplicar = output<SavedView>();
  readonly crear = output<{ nombre: string; tipo: string }>();
  readonly borrar = output<SavedView>();

  readonly creando = signal(false);
  nombreNuevo = '';
  tipoNuevo = '';

  /**
   * El icono sale del estado guardado, no del nombre: una vista llamada «Urgentes» puede ser un
   * tablero, y adivinarlo por el texto acertaría unas veces sí y otras no.
   */
  iconoDe(vista: SavedView): string {
    try {
      const estado = JSON.parse(vista.stateJson) as { viewType?: string };
      return this.integradas().find(v => v.clave === estado.viewType)?.icono ?? 'lucideList';
    } catch {
      // Un estado ilegible no debe romper la barra: se pinta como lista y la vista sigue ahí para
      // poder borrarla, que es lo único sensato que se puede hacer con ella.
      return 'lucideList';
    }
  }

  empezarCreacion(): void {
    this.nombreNuevo = '';
    // Se propone la forma que se está viendo: quien pulsa «Vista» estando en el tablero casi
    // siempre quiere guardar ese tablero.
    this.tipoNuevo = this.modo();
    this.creando.set(true);
  }

  confirmarCreacion(): void {
    const nombre = this.nombreNuevo.trim();
    if (!nombre) return;

    this.crear.emit({ nombre, tipo: this.tipoNuevo });
    this.creando.set(false);
  }

  cancelarCreacion(): void {
    this.creando.set(false);
  }

  /**
   * Se confirma antes de borrar. Es la única acción de la barra que destruye algo, y las
   * pestañas están pegadas: un aspa a un centímetro de la vista que quieres abrir se pulsa sola.
   */
  pedirBorrado(vista: SavedView): void {
    if (confirm(`¿Borrar la vista «${vista.viewName}»?`)) {
      this.borrar.emit(vista);
    }
  }
}
