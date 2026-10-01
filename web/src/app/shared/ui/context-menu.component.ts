import { Component, ElementRef, HostListener, computed, inject, input, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarPlus, lucideCalendarRange, lucideClipboardList, lucideEye,
  lucideFolderCheck, lucideSettings, lucideSquareCheck, lucideTicket, lucideTrash2
} from '@ng-icons/lucide';

/** Una entrada del menú. Un separador es una entrada sin acción ni etiqueta. */
export interface MenuOption {
  key: string;
  label: string;
  icon?: string;
  /** Si la entrada destruye algo. Se pinta en rojo y va al final. */
  destructive?: boolean;
  separatorBefore?: boolean;
  disabled?: boolean;
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
  selector: 'app-context-menu',
  standalone: true,
  imports: [NgIcon],
  viewProviders: [provideIcons({
    lucideCalendarPlus, lucideCalendarRange, lucideClipboardList, lucideEye,
    lucideFolderCheck, lucideSettings, lucideSquareCheck, lucideTicket, lucideTrash2
  })],
  template: `
    @if (isOpen()) {
      <!--
        La capa de debajo captura el clic de fuera. Se usa un elemento y no un listener global
        para que ese clic **no** llegue además a lo que haya detrás: sin ella, cerrar el menú
        pulsando en otra celda abría el día de esa celda a la vez.
      -->
      <div class="fixed inset-0 z-40" (click)="close()" (contextmenu)="$event.preventDefault(); close()"></div>

      <div
        role="menu"
        class="fixed z-50 min-w-56 rounded-lg border border-border bg-card py-1 shadow-xl"
        [style.left.px]="position().x"
        [style.top.px]="position().y">

        @if (title()) {
          <p class="px-3 py-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            {{ title() }}
          </p>
        }

        @for (option of options(); track option.key) {
          @if (option.separatorBefore) {
            <div class="my-1 h-px bg-border"></div>
          }

          <button
            type="button"
            role="menuitem"
            [disabled]="option.disabled"
            (click)="choose(option)"
            class="flex w-full items-center gap-2.5 px-3 py-1.5 text-left text-sm transition-colors
                   hover:bg-accent disabled:cursor-not-allowed disabled:opacity-40
                   focus:outline-none focus:bg-accent"
            [class.text-destructive]="option.destructive">
            @if (option.icon) {
              <ng-icon [name]="option.icon" size="14" />
            }
            {{ option.label }}
          </button>
        }
      </div>
    }
  `
})
export class ContextMenuComponent {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly options = input.required<MenuOption[]>();
  readonly title = input<string | null>(null);

  readonly chosen = output<string>();

  readonly isOpen = signal(false);
  private readonly point = signal({ x: 0, y: 0 });

  /**
   * Dónde se pinta: donde se pulsó, corregido para que quepa.
   *
   * El tamaño se estima —no se mide— porque el menú todavía no está en el DOM cuando hay que
   * decidir. Estimar de más es lo seguro: como mucho el menú sale un poco más arriba de lo
   * necesario, mientras que quedarse corto lo deja medio fuera.
   */
  readonly position = computed(() => {
    const { x, y } = this.point();
    const height = 44 + this.options().length * 32;
    const width = 224;

    return {
      x: Math.min(x, window.innerWidth - width - 8),
      y: Math.min(y, window.innerHeight - height - 8)
    };
  });

  openAt(event: MouseEvent): void {
    event.preventDefault();
    this.point.set({ x: event.clientX, y: event.clientY });
    this.isOpen.set(true);
  }

  close(): void {
    this.isOpen.set(false);
  }

  choose(option: MenuOption): void {
    if (option.disabled) return;

    this.close();
    this.chosen.emit(option.key);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }

  @HostListener('window:scroll')
  @HostListener('window:resize')
  onMove(): void {
    // Si la página se mueve, el menú dejaría de señalar a lo que se eligió.
    this.close();
  }
}
