import { Component, computed, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideCalendarDays, lucideFolderCheck, lucideSquareCheck, lucideTicket, lucideX
} from '@ng-icons/lucide';

import type { AgendaDeUnDia, CosaDelDia } from './calendario.service';

/** Una franja horaria del día, con lo que cae dentro. */
interface Franja {
  hora: number;
  eventos: CosaDelDia[];
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
  selector: 'app-dia-desplegado',
  standalone: true,
  imports: [DatePipe, NgIcon],
  viewProviders: [provideIcons({
    lucideCalendarDays, lucideFolderCheck, lucideSquareCheck, lucideTicket, lucideX
  })],
  template: `
    <div class="flex h-full flex-col">
      <div class="flex items-center justify-between border-b border-border px-4 py-3">
        <div>
          <h3 class="text-base font-semibold capitalize">{{ diaLegible() }}</h3>
          <p class="text-xs text-muted-foreground">{{ resumen() }}</p>
        </div>

        <button type="button" (click)="cerrar.emit()"
          class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-foreground
                 focus:outline-none focus:ring-2 focus:ring-ring"
          i18n-title title="Volver al mes">
          <ng-icon name="lucideX" size="18" />
        </button>
      </div>

      <div class="flex-1 overflow-y-auto">
        <!-- Lo del día que no tiene hora -->
        @if (sinHora().length > 0) {
          <div class="space-y-1.5 border-b border-border bg-muted/30 px-4 py-3">
            <p class="text-xs font-semibold uppercase tracking-wider text-muted-foreground" i18n>
              Vence hoy
            </p>

            @for (cosa of sinHora(); track cosa.tipo + cosa.id) {
              <button type="button" (click)="abrirCosa.emit(cosa)"
                class="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm
                       hover:bg-accent focus:outline-none focus:ring-2 focus:ring-ring">
                <ng-icon [name]="iconoDe(cosa.tipo)" size="14" class="shrink-0 text-muted-foreground" />
                <span class="truncate">{{ cosa.titulo }}</span>
                @if (cosa.detalle) {
                  <span class="ml-auto shrink-0 text-xs text-muted-foreground">{{ cosa.detalle }}</span>
                }
              </button>
            }
          </div>
        }

        <!-- Las horas -->
        @for (franja of franjas(); track franja.hora) {
          <div class="flex border-b border-border/50">
            <div class="w-16 shrink-0 border-r border-border/50 px-2 py-2 text-right text-xs text-muted-foreground">
              {{ franja.hora }}:00
            </div>

            <!--
              La franja entera es pulsable para crear ahí: es el gesto que espera cualquiera que
              haya usado un calendario, y evita tener que ir a buscar el botón de arriba.
            -->
            <div class="min-h-12 flex-1 space-y-1 p-1.5"
                 (click)="crearAlas.emit(franja.hora)"
                 (contextmenu)="menuEnHora.emit({ evento: $event, hora: franja.hora })">

              @for (cosa of franja.eventos; track cosa.id) {
                <button type="button"
                  (click)="$event.stopPropagation(); abrirCosa.emit(cosa)"
                  (contextmenu)="$event.stopPropagation(); menuEnEvento.emit({ evento: $event, cosa })"
                  class="flex w-full items-center gap-2 rounded-md border-l-2 px-2 py-1.5 text-left text-sm
                         transition-colors focus:outline-none focus:ring-2 focus:ring-ring"
                  [class]="cosa.anulado
                    ? 'border-muted-foreground bg-muted/50 text-muted-foreground line-through'
                    : 'border-primary bg-primary/10 hover:bg-primary/20'">
                  <span class="shrink-0 text-xs tabular-nums">{{ cosa.hora | date:'HH:mm' }}</span>
                  <span class="truncate">{{ cosa.titulo }}</span>
                  @if (cosa.anulado) {
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
export class DiaDesplegadoComponent {
  readonly agenda = input.required<AgendaDeUnDia>();

  readonly cerrar = output<void>();
  readonly crearAlas = output<number>();
  readonly abrirCosa = output<CosaDelDia>();
  readonly menuEnEvento = output<{ evento: MouseEvent; cosa: CosaDelDia }>();
  readonly menuEnHora = output<{ evento: MouseEvent; hora: number }>();

  readonly diaLegible = computed(() =>
    new Date(this.agenda().dia + 'T12:00:00').toLocaleDateString('es', {
      weekday: 'long', day: 'numeric', month: 'long', year: 'numeric'
    }));

  readonly sinHora = computed<CosaDelDia[]>(() => {
    const a = this.agenda();
    return [...a.tareasQueVencen, ...a.proyectosQueTerminan, ...a.ticketsDelDia.filter(t => !t.hora)];
  });

  readonly resumen = computed(() => {
    const a = this.agenda();
    const partes: string[] = [];

    // Se dice sólo lo que hay. «0 tickets» es ruido que hay que leer para descubrir que no dice
    // nada; un día sin nada lo dice con una frase entera.
    if (a.eventos.length) partes.push(`${a.eventos.length} evento${a.eventos.length > 1 ? 's' : ''}`);
    if (a.tareasQueVencen.length) partes.push(`${a.tareasQueVencen.length} tarea${a.tareasQueVencen.length > 1 ? 's' : ''} que vence${a.tareasQueVencen.length > 1 ? 'n' : ''}`);
    if (a.ticketsDelDia.length) partes.push(`${a.ticketsDelDia.length} ticket${a.ticketsDelDia.length > 1 ? 's' : ''}`);
    if (a.proyectosQueTerminan.length) partes.push(`${a.proyectosQueTerminan.length} proyecto${a.proyectosQueTerminan.length > 1 ? 's' : ''} que termina${a.proyectosQueTerminan.length > 1 ? 'n' : ''}`);

    return partes.length ? partes.join(' · ') : 'Nada en el calendario este día';
  });

  /**
   * Las franjas que se pintan: desde la primera con algo hasta la última, y como mínimo de 8 a 19.
   *
   * El mínimo existe para que un día vacío siga pareciendo un día —con sus horas— en lugar de una
   * caja en blanco, y para que haya dónde pulsar y crear.
   */
  readonly franjas = computed<Franja[]>(() => {
    const conHora = this.agenda().eventos.filter(e => e.hora);
    const horas = conHora.map(e => new Date(e.hora!).getHours());

    const desde = Math.min(8, ...horas);
    const hasta = Math.max(19, ...horas);

    const franjas: Franja[] = [];

    for (let hora = desde; hora <= hasta; hora++) {
      franjas.push({
        hora,
        eventos: conHora.filter(e => new Date(e.hora!).getHours() === hora)
      });
    }

    return franjas;
  });

  iconoDe(tipo: string): string {
    switch (tipo) {
      case 'Tarea': return 'lucideSquareCheck';
      case 'Ticket': return 'lucideTicket';
      case 'Proyecto': return 'lucideFolderCheck';
      default: return 'lucideCalendarDays';
    }
  }
}
