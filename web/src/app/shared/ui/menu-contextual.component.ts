import { Component, ElementRef, HostListener, computed, inject, input, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarPlus, lucideCalendarRange, lucideClipboardList, lucideEye,
  lucideFolderCheck, lucideSettings, lucideSquareCheck, lucideTicket, lucideTrash2
} from '@ng-icons/lucide';

/** Una entrada del menú. Un separador es una entrada sin acción ni etiqueta. */
export interface OpcionDelMenu {
  clave: string;
  etiqueta: string;
  icono?: string;
  /** Si la entrada destruye algo. Se pinta en rojo y va al final. */
  destructiva?: boolean;
  separadorAntes?: boolean;
  deshabilitada?: boolean;
}

/**
 * El menú que sale al pulsar con el botón derecho.
 *
 * <b>Se posiciona dentro de la ventana, no donde se pulsó.</b> Un menú anclado al puntero a secas
 * se sale por abajo cuando se pulsa en la última fila del mes, y entonces sus últimas opciones
 * —que son justamente las de borrar— quedan fuera de la pantalla y no se pueden alcanzar.
 *
 * Se cierra al pulsar fuera, con Escape y al hacer scroll: un menú que se queda flotando mientras
 * la página se mueve acaba señalando a una celda que ya no es la que se eligió.
 */
@Component({
  selector: 'app-menu-contextual',
  standalone: true,
  imports: [NgIcon],
  viewProviders: [provideIcons({
    lucideCalendarPlus, lucideCalendarRange, lucideClipboardList, lucideEye,
    lucideFolderCheck, lucideSettings, lucideSquareCheck, lucideTicket, lucideTrash2
  })],
  template: `
    @if (abierto()) {
      <!--
        La capa de debajo captura el clic de fuera. Se usa un elemento y no un listener global
        para que ese clic **no** llegue además a lo que haya detrás: sin ella, cerrar el menú
        pulsando en otra celda abría el día de esa celda a la vez.
      -->
      <div class="fixed inset-0 z-40" (click)="cerrar()" (contextmenu)="$event.preventDefault(); cerrar()"></div>

      <div
        role="menu"
        class="fixed z-50 min-w-56 rounded-lg border border-border bg-card py-1 shadow-xl"
        [style.left.px]="posicion().x"
        [style.top.px]="posicion().y">

        @if (titulo()) {
          <p class="px-3 py-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            {{ titulo() }}
          </p>
        }

        @for (opcion of opciones(); track opcion.clave) {
          @if (opcion.separadorAntes) {
            <div class="my-1 h-px bg-border"></div>
          }

          <button
            type="button"
            role="menuitem"
            [disabled]="opcion.deshabilitada"
            (click)="elegir(opcion)"
            class="flex w-full items-center gap-2.5 px-3 py-1.5 text-left text-sm transition-colors
                   hover:bg-accent disabled:cursor-not-allowed disabled:opacity-40
                   focus:outline-none focus:bg-accent"
            [class.text-destructive]="opcion.destructiva">
            @if (opcion.icono) {
              <ng-icon [name]="opcion.icono" size="14" />
            }
            {{ opcion.etiqueta }}
          </button>
        }
      </div>
    }
  `
})
export class MenuContextualComponent {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly opciones = input.required<OpcionDelMenu[]>();
  readonly titulo = input<string | null>(null);

  readonly elegida = output<string>();

  readonly abierto = signal(false);
  private readonly punto = signal({ x: 0, y: 0 });

  /**
   * Dónde se pinta: donde se pulsó, corregido para que quepa.
   *
   * El tamaño se estima —no se mide— porque el menú todavía no está en el DOM cuando hay que
   * decidir. Estimar de más es lo seguro: como mucho el menú sale un poco más arriba de lo
   * necesario, mientras que quedarse corto lo deja medio fuera.
   */
  readonly posicion = computed(() => {
    const { x, y } = this.punto();
    const alto = 44 + this.opciones().length * 32;
    const ancho = 224;

    return {
      x: Math.min(x, window.innerWidth - ancho - 8),
      y: Math.min(y, window.innerHeight - alto - 8)
    };
  });

  abrirEn(evento: MouseEvent): void {
    evento.preventDefault();
    this.punto.set({ x: evento.clientX, y: evento.clientY });
    this.abierto.set(true);
  }

  cerrar(): void {
    this.abierto.set(false);
  }

  elegir(opcion: OpcionDelMenu): void {
    if (opcion.deshabilitada) return;

    this.cerrar();
    this.elegida.emit(opcion.clave);
  }

  @HostListener('document:keydown.escape')
  alPulsarEscape(): void {
    this.cerrar();
  }

  @HostListener('window:scroll')
  @HostListener('window:resize')
  alMoverse(): void {
    // Si la página se mueve, el menú dejaría de señalar a lo que se eligió.
    this.cerrar();
  }
}
