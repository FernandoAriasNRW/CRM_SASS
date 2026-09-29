import { Component, computed, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarDays, lucideFolderCheck, lucideSquareCheck, lucideTicket, lucideX
} from '@ng-icons/lucide';

import type { DailyAgenda, AgendaItem } from './calendar.service';

/** Una franja horaria del día, con lo que cae dentro. */
interface HourSlot {
  hour: number;
  events: AgendaItem[];
}

/**
 * Un día abierto: sus horas, sus eventos, y lo que vence ese día en los otros módulos.
 *
 * <b>Empieza en la primera hora con algo</b> y no a las 00:00. Un día que arranca con ocho franjas
 * vacías obliga a desplazarse para ver la primera reunión, y lo que se abre por la mañana casi
 * siempre empieza a las nueve.
 *
 * Lo que no tiene hora —una tarea que vence, un proyecto que termina— va arriba y no repartido: no
 * ocurre a una hora concreta, y colocarlo en una inventada haría creer que sí.
 */
@Component({
  selector: 'app-expanded-day',
  standalone: true,
  imports: [DatePipe, NgIcon],
  viewProviders: [provideIcons({
    lucideCalendarDays, lucideFolderCheck, lucideSquareCheck, lucideTicket, lucideX
  })],
  template: `
    <div class="flex h-full flex-col">
      <div class="flex items-center justify-between border-b border-border px-4 py-3">
        <div>
          <h3 class="text-base font-semibold capitalize">{{ dayLabel() }}</h3>
          <p class="text-xs text-muted-foreground">{{ summary() }}</p>
        </div>

        <button type="button" (click)="closed.emit()"
          class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-foreground
                 focus:outline-none focus:ring-2 focus:ring-ring"
          i18n-title title="Volver al mes">
          <ng-icon name="lucideX" size="18" />
        </button>
      </div>

      <div class="flex-1 overflow-y-auto">
        <!-- Lo del día que no tiene hora -->
        @if (untimed().length > 0) {
          <div class="space-y-1.5 border-b border-border bg-muted/30 px-4 py-3">
            <p class="text-xs font-semibold uppercase tracking-wider text-muted-foreground" i18n>
              Vence hoy
            </p>

            @for (item of untimed(); track item.type + item.id) {
              <button type="button" (click)="openItem.emit(item)"
                class="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm
                       hover:bg-accent focus:outline-none focus:ring-2 focus:ring-ring">
                <ng-icon [name]="iconFor(item.type)" size="14" class="shrink-0 text-muted-foreground" />
                <span class="truncate">{{ item.title }}</span>
                @if (item.detail) {
                  <span class="ml-auto shrink-0 text-xs text-muted-foreground">{{ item.detail }}</span>
                }
              </button>
            }
          </div>
        }

        <!-- Las horas -->
        @for (franja of slots(); track franja.hour) {
          <div class="flex border-b border-border/50">
            <div class="w-16 shrink-0 border-r border-border/50 px-2 py-2 text-right text-xs text-muted-foreground">
              {{ franja.hour }}:00
            </div>

            <!--
              La franja entera es pulsable para crear ahí: es el gesto que espera cualquiera que
              haya usado un calendario, y evita tener que ir a buscar el botón de arriba.
            -->
            <div class="min-h-12 flex-1 space-y-1 p-1.5"
                 (click)="createAt.emit(franja.hour)"
                 (contextmenu)="hourContextMenu.emit({ mouseEvent: $event, hour: franja.hour })">

              @for (item of franja.events; track item.id) {
                <button type="button"
                  (click)="$event.stopPropagation(); openItem.emit(item)"
                  (contextmenu)="$event.stopPropagation(); itemContextMenu.emit({ mouseEvent: $event, item })"
                  class="flex w-full items-center gap-2 rounded-md border-l-2 px-2 py-1.5 text-left text-sm
                         transition-colors focus:outline-none focus:ring-2 focus:ring-ring"
                  [class]="item.isCancelled
                    ? 'border-muted-foreground bg-muted/50 text-muted-foreground line-through'
                    : 'border-primary bg-primary/10 hover:bg-primary/20'">
                  <span class="shrink-0 text-xs tabular-nums">{{ item.time | date:'HH:mm' }}</span>
                  <span class="truncate">{{ item.title }}</span>
                  @if (item.isCancelled) {
                    <span class="ml-auto shrink-0 text-[10px] uppercase tracking-wider no-underline" i18n>Anulado</span>
                  }
                </button>
              }
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class ExpandedDayComponent {
  readonly agenda = input.required<DailyAgenda>();

  readonly closed = output<void>();
  readonly createAt = output<number>();
  readonly openItem = output<AgendaItem>();
  readonly itemContextMenu = output<{ mouseEvent: MouseEvent; item: AgendaItem }>();
  readonly hourContextMenu = output<{ mouseEvent: MouseEvent; hour: number }>();

  readonly dayLabel = computed(() =>
    new Date(this.agenda().day + 'T12:00:00').toLocaleDateString('es', {
      weekday: 'long', day: 'numeric', month: 'long', year: 'numeric'
    }));

  readonly untimed = computed<AgendaItem[]>(() => {
    const a = this.agenda();
    return [...a.tasksDue, ...a.projectsEnding, ...a.ticketsOpened.filter(t => !t.time)];
  });

  readonly summary = computed(() => {
    const a = this.agenda();
    const parts: string[] = [];

    // Se dice sólo lo que hay. «0 tickets» es ruido que hay que leer para descubrir que no dice
    // nada; un día sin nada lo dice con una frase entera.
    if (a.events.length) parts.push(`${a.events.length} evento${a.events.length > 1 ? 's' : ''}`);
    if (a.tasksDue.length) parts.push(`${a.tasksDue.length} tarea${a.tasksDue.length > 1 ? 's' : ''} que vence${a.tasksDue.length > 1 ? 'n' : ''}`);
    if (a.ticketsOpened.length) parts.push(`${a.ticketsOpened.length} ticket${a.ticketsOpened.length > 1 ? 's' : ''}`);
    if (a.projectsEnding.length) parts.push(`${a.projectsEnding.length} proyecto${a.projectsEnding.length > 1 ? 's' : ''} que termina${a.projectsEnding.length > 1 ? 'n' : ''}`);

    return parts.length ? parts.join(' · ') : 'Nada en el calendario este día';
  });

  /**
   * Las franjas que se pintan: desde la primera con algo hasta la última, y como mínimo de 8 a 19.
   *
   * El mínimo existe para que un día vacío siga pareciendo un día —con sus horas— en lugar de una
   * caja en blanco, y para que haya dónde pulsar y crear.
   */
  readonly slots = computed<HourSlot[]>(() => {
    const timed = this.agenda().events.filter(e => e.time);
    const hours = timed.map(e => new Date(e.time!).getHours());

    const from = Math.min(8, ...hours);
    const to = Math.max(19, ...hours);

    const slots: HourSlot[] = [];

    for (let hour = from; hour <= to; hour++) {
      slots.push({
        hour,
        events: timed.filter(e => new Date(e.time!).getHours() === hour)
      });
    }

    return slots;
  });

  iconFor(type: string): string {
    switch (type) {
      case 'Task': return 'lucideSquareCheck';
      case 'Ticket': return 'lucideTicket';
      case 'Project': return 'lucideFolderCheck';
      default: return 'lucideCalendarDays';
    }
  }
}
