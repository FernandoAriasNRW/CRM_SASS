import {
  Component, ElementRef, computed, effect, inject, signal, viewChild,
} from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import * as lucide from '@ng-icons/lucide';
import { CommandPaletteService, type Command } from './command-palette.service';

/**
 * Paletón de comandos (⌘K / Ctrl+K).
 *
 * Navegación, búsqueda y acciones sin levantar las manos del teclado. Es la expectativa
 * de mercado en 2026 —Linear, Notion y ClickUp lo traen— y aquí carga doble sentido: la
 * queja más repetida sobre ClickUp es la fricción, así que llegar a cualquier sitio en
 * dos pulsaciones es precisamente donde este producto puede diferenciarse.
 *
 * Accesibilidad: el patrón es combobox + listbox. El foco permanece en el campo de texto
 * mientras se recorre la lista con las flechas, y `aria-activedescendant` le dice al
 * lector de pantalla qué opción está resaltada. Mover el foco real a cada opción
 * obligaría a devolverlo al campo para seguir escribiendo.
 */
@Component({
  selector: 'ui-command-palette',
  standalone: true,
  imports: [NgIcon],
  providers: [provideIcons(lucide as unknown as Record<string, string>)],
  template: `
    @if (svc.isOpen()) {
      <!-- El fondo cierra al pulsar. Es decorativo: la vía accesible para cerrar es
           Escape, que se atiende en el diálogo. Hacerlo enfocable añadiría un punto de
           tabulación sin significado, así que aquí la regla se salta a conciencia. -->
      <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
      <div class="fixed inset-0 z-50 bg-foreground/40 backdrop-blur-sm animate-fade-in"
           (click)="svc.close()"></div>

      <div class="fixed inset-0 z-50 flex items-start justify-center pt-[12vh] px-4 pointer-events-none">
        <div role="dialog"
             aria-modal="true"
             aria-label="Paleta de comandos"
             (keydown)="onKeydown($event)"
             class="pointer-events-auto w-full max-w-xl overflow-hidden rounded-xl border border-border
                    bg-card shadow-2xl animate-fade-in">

          <div class="flex items-center gap-3 border-b border-border px-4">
            <ng-icon name="lucideSearch" class="text-muted-foreground shrink-0" aria-hidden="true" />
            <input #field
                   type="text"
                   role="combobox"
                   aria-expanded="true"
                   aria-controls="paleta-opciones"
                   [attr.aria-activedescendant]="activeId()"
                   aria-label="Buscar comandos, proyectos, tareas y tickets"
                   [value]="svc.query()"
                   (input)="onInput($event)"
                   placeholder="Buscar o ejecutar una acción…"
                   class="w-full bg-transparent py-4 text-sm outline-none placeholder:text-muted-foreground" />
            @if (svc.searching()) {
              <span class="text-xs text-muted-foreground shrink-0">Buscando…</span>
            }
            <kbd class="shrink-0 rounded border border-border px-1.5 py-0.5 text-[10px] text-muted-foreground">ESC</kbd>
          </div>

          <div id="paleta-opciones" role="listbox" aria-label="Resultados"
               class="max-h-[22rem] overflow-y-auto p-2">
            @for (group of svc.grouped(); track group.key) {
              <div class="px-2 pb-1 pt-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground"
                   aria-hidden="true">{{ group.name }}</div>
              @for (cmd of group.commands; track cmd.id) {
                <!-- En el patrón combobox + listbox las opciones NO deben ser enfocables:
                     el foco permanece en el campo y aria-activedescendant señala cuál
                     está activa. El teclado se atiende en el diálogo. Hacer enfocable
                     cada opción rompería el patrón en lugar de mejorarlo. -->
                <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events, @angular-eslint/template/interactive-supports-focus -->
                <div [id]="'cmd-' + cmd.id"
                     role="option"
                     [attr.aria-selected]="cmd.id === active()?.id"
                     (click)="run(cmd)"
                     (mouseenter)="highlight(cmd)"
                     [class]="rowClasses(cmd)">
                  <ng-icon [name]="cmd.icon" class="shrink-0 text-muted-foreground" aria-hidden="true" />
                  <span class="flex-1 truncate">{{ cmd.label }}</span>
                  @if (cmd.hint) {
                    <span class="shrink-0 text-xs text-muted-foreground">{{ cmd.hint }}</span>
                  }
                </div>
              }
            } @empty {
              <p class="px-3 py-8 text-center text-sm text-muted-foreground">
                Nada coincide con «{{ svc.query() }}».
              </p>
            }
          </div>

          <div class="flex items-center gap-4 border-t border-border px-4 py-2 text-[11px] text-muted-foreground">
            <span><kbd class="rounded border border-border px-1">↑</kbd>
                  <kbd class="rounded border border-border px-1">↓</kbd> moverse</span>
            <span><kbd class="rounded border border-border px-1">↵</kbd> abrir</span>
          </div>
        </div>
      </div>
    }
  `,
})
export class CommandPaletteComponent {
  protected readonly svc = inject(CommandPaletteService);
  private readonly field = viewChild<ElementRef<HTMLInputElement>>('field');

  private readonly index = signal(0);
  private timer?: ReturnType<typeof setTimeout>;

  protected readonly active = computed<Command | undefined>(
    () => this.svc.results()[this.index()]);

  protected readonly activeId = computed(() => {
    const a = this.active();
    return a ? `cmd-${a.id}` : null;
  });

  constructor() {
    // Al abrirse, el foco va al campo. Sin esto habría que hacer clic para escribir, que
    // es justo lo que el paletón evita.
    effect(() => {
      if (this.svc.isOpen()) {
        this.index.set(0);
        queueMicrotask(() => this.field()?.nativeElement.focus());
      }
    });
  }

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.svc.query.set(value);
    this.index.set(0);

    // Los estáticos ya se filtraron en memoria al cambiar la señal. Sólo la ida al
    // servidor se retrasa, para no lanzar una petición por tecla.
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.svc.searchServer(value), 200);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const total = this.svc.results().length;

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        // Circular: desde el último se vuelve al primero, que es lo que se espera al
        // recorrer una lista corta sin mirar dónde termina.
        this.index.set(total ? (this.index() + 1) % total : 0);
        this.scrollToActive();
        break;

      case 'ArrowUp':
        event.preventDefault();
        this.index.set(total ? (this.index() - 1 + total) % total : 0);
        this.scrollToActive();
        break;

      case 'Enter': {
        event.preventDefault();
        const cmd = this.active();
        if (cmd) this.run(cmd);
        break;
      }

      case 'Escape':
        event.preventDefault();
        this.svc.close();
        break;
    }
  }

  protected run(cmd: Command): void {
    this.svc.close();
    cmd.run();
  }

  protected highlight(cmd: Command): void {
    const i = this.svc.results().findIndex(c => c.id === cmd.id);
    if (i >= 0) this.index.set(i);
  }

  protected rowClasses(cmd: Command): string {
    const base = 'flex cursor-pointer items-center gap-3 rounded-md px-3 py-2 text-sm';
    return cmd.id === this.active()?.id
      ? `${base} bg-accent text-accent-foreground`
      : `${base} text-foreground`;
  }

  /** Mantiene visible la opción resaltada al recorrer con el teclado. */
  private scrollToActive(): void {
    queueMicrotask(() => {
      const id = this.activeId();
      if (id) document.getElementById(id)?.scrollIntoView({ block: 'nearest' });
    });
  }
}
