import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCalendarDays, lucideLink, lucideLoaderCircle } from '@ng-icons/lucide';

import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ToastService } from '../../shared/services/toast.service';
import { MentionsService } from '../docs/mentions.service';
import { MENTION_TYPE_LABELS, type MentionCandidate, type MentionType } from '../docs/extensions/mention';
import {
  CalendarService, toLocalDateTime,
  type CalendarEventInput, type CalendarEvent
} from './calendar.service';

/** Los tipos que entiende el servidor, con su nombre en castellano. */
const EVENT_TYPES: { key: string; label: string }[] = [
  { key: 'Meeting', label: $localize`Reunión` },
  { key: 'Appointment', label: $localize`Cita` },
  { key: 'Task', label: $localize`Tarea` },
  { key: 'Reminder', label: $localize`Recordatorio` },
  { key: 'OutOfOffice', label: $localize`Fuera de oficina` },
  { key: 'Holiday', label: $localize`Día festivo` }
];

/**
 * El formulario de un evento, en cajón lateral como el resto de la aplicación.
 *
 * <b>Antes era un modal propio del calendario</b> —el único módulo que no usaba el cajón—, y
 * además mandaba campos que la API no conoce: <code>startsAtUtc</code> donde el comando espera
 * <code>startTime</code>. El error caía en un <code>catch</code> vacío, así que pulsar «guardar»
 * cerraba la ventana y no creaba nada.
 *
 * <b>Los enlaces se buscan escribiendo, no en un desplegable.</b> Un desplegable con las 230
 * tareas del inquilino es inservible, y uno con las primeras 25 esconde justo la que se busca.
 * Se reutiliza el buscador de las menciones, que ya pregunta al servidor.
 */
@Component({
  selector: 'app-event-drawer',
  standalone: true,
  imports: [FormsModule, DrawerComponent, NgIcon],
  viewProviders: [provideIcons({ lucideCalendarDays, lucideLink, lucideLoaderCircle })],
  template: `
    <app-drawer
      [isOpen]="isOpen()"
      [title]="drawerTitle()"
      [subtitle]="subtitle()"
      size="md"
      (closed)="closed.emit()">

      <ng-icon drawer-icon name="lucideCalendarDays" size="20" class="text-primary" />

      <div class="space-y-4">
        <div>
          <label for="titulo" class="mb-1 block text-sm font-medium" i18n>Título</label>
          <input id="titulo" [(ngModel)]="title" required
            class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                   focus:outline-none focus:ring-2 focus:ring-ring" />
          @if (titleTooShort()) {
            <!--
              El servidor exige tres caracteres. Se dice aquí antes de mandar: si no, la única
              señal sería un 400 traducido a un aviso genérico, y quien escribe «Ok» no sabría
              qué corregir.
            -->
            <p class="mt-1 text-xs text-destructive" i18n>El título necesita al menos 3 caracteres.</p>
          }
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div>
            <label for="inicio" class="mb-1 block text-sm font-medium" i18n>Empieza</label>
            <input id="inicio" type="datetime-local" [(ngModel)]="start"
              class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label for="fin" class="mb-1 block text-sm font-medium" i18n>Termina</label>
            <input id="fin" type="datetime-local" [(ngModel)]="end"
              class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring" />
            @if (endsBeforeStart()) {
              <p class="mt-1 text-xs text-destructive" i18n>Tiene que terminar después de empezar.</p>
            }
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div>
            <label for="tipo" class="mb-1 block text-sm font-medium" i18n>Tipo</label>
            <select id="tipo" [(ngModel)]="type"
              class="h-9 w-full rounded-md border border-border bg-background px-2 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring">
              @for (t of EVENT_TYPES; track t.key) {
                <option [value]="t.key">{{ t.label }}</option>
              }
            </select>
          </div>
          <div>
            <label for="sitio" class="mb-1 block text-sm font-medium" i18n>Dónde</label>
            <input id="sitio" [(ngModel)]="location" placeholder="Sala, enlace…"
              class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
        </div>

        <div>
          <label for="descripcion" class="mb-1 block text-sm font-medium" i18n>Descripción</label>
          <textarea id="descripcion" [(ngModel)]="description" rows="3"
            class="w-full rounded-md border border-border bg-background px-3 py-2 text-sm
                   focus:outline-none focus:ring-2 focus:ring-ring"></textarea>
        </div>

        <!-- Enlaces -->
        <div class="rounded-lg border border-border p-3 space-y-2">
          <div class="flex items-center gap-2 text-sm font-medium">
            <ng-icon name="lucideLink" size="14" /> <span i18n>Enlazar con</span>
          </div>

          <p class="text-xs text-muted-foreground" i18n>
            Escribe para buscar una tarea, un ticket o un proyecto.
          </p>

          @for (e of linked(); track e.id) {
            <div class="flex items-center justify-between rounded-md bg-secondary px-2 py-1.5 text-sm">
              <span class="truncate"><span class="text-muted-foreground">{{ typeLabels[e.type] }} ·</span> {{ e.label }}</span>
              <button type="button" (click)="removeLink(e)"
                class="ml-2 shrink-0 text-xs text-muted-foreground hover:text-destructive
                       focus:outline-none focus:ring-2 focus:ring-ring rounded"
                i18n>Quitar</button>
            </div>
          }

          <div class="relative">
            <input
              [(ngModel)]="searchText"
              (ngModelChange)="search($event)"
              i18n-placeholder placeholder="Buscar…"
              class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring" />

            @if (candidates().length > 0) {
              <ul class="absolute z-10 mt-1 max-h-52 w-full overflow-y-auto rounded-md border border-border bg-card shadow-lg">
                @for (c of candidates(); track c.type + c.id) {
                  <li>
                    <button type="button" (click)="addLink(c)"
                      class="flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm hover:bg-accent
                             focus:outline-none focus:bg-accent">
                      <span class="text-xs text-muted-foreground">{{ typeLabels[c.type] }}</span>
                      <span class="truncate">{{ c.label }}</span>
                    </button>
                  </li>
                }
              </ul>
            }
          </div>
        </div>
      </div>

      <div drawer-footer class="flex items-center gap-3">
        <button type="button" (click)="closed.emit()"
          class="h-9 rounded-md border border-border px-4 text-sm font-medium hover:bg-accent
                 focus:outline-none focus:ring-2 focus:ring-ring" i18n>
          Cancelar
        </button>

        <button type="button" (click)="save()" [disabled]="!canSave() || saving()"
          class="inline-flex h-9 items-center gap-2 rounded-md bg-primary px-4 text-sm font-medium
                 text-primary-foreground hover:bg-primary/90 disabled:opacity-50
                 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-ring">
          @if (saving()) { <ng-icon name="lucideLoaderCircle" size="14" class="animate-spin" /> }
          <span i18n>Guardar</span>
        </button>
      </div>
    </app-drawer>
  `
})
export class EventDrawerComponent {
  private readonly calendar = inject(CalendarService);
  private readonly mentions = inject(MentionsService);
  private readonly toast = inject(ToastService);

  readonly EVENT_TYPES = EVENT_TYPES;

  readonly isOpen = input.required<boolean>();

  /** El evento a modificar, o nulo para crear uno nuevo. */
  readonly calendarEvent = input<CalendarEvent | null>(null);

  /** El día y la hora que se proponen al crear: los del sitio donde se pulsó. */
  readonly proposedTime = input<Date | null>(null);

  readonly closed = output<void>();
  readonly saved = output<CalendarEvent>();

  title = '';
  description = '';
  type = 'Meeting';
  location = '';
  start = '';
  end = '';
  searchText = '';

  /** El nombre en pantalla de cada tipo enlazable. */
  readonly typeLabels = MENTION_TYPE_LABELS;

  readonly linked = signal<MentionCandidate[]>([]);
  readonly candidates = signal<MentionCandidate[]>([]);
  readonly saving = signal(false);

  constructor() {
    // Se rellena al abrirse, no en el constructor: el cajón es el mismo componente para crear y
    // para modificar, y si no se recargara enseñaría los datos del evento anterior.
    effect(() => {
      if (!this.isOpen()) return;
      this.fill();
    });
  }

  private fill(): void {
    const calendarEvent = this.calendarEvent();

    if (calendarEvent) {
      this.title = calendarEvent.title;
      this.description = calendarEvent.description ?? '';
      this.type = calendarEvent.type;
      this.location = calendarEvent.location ?? '';
      this.start = toLocalDateTime(new Date(calendarEvent.startTime));
      this.end = toLocalDateTime(new Date(calendarEvent.endTime));
      this.linked.set(linksOf(calendarEvent));
    } else {
      const from = this.proposedTime() ?? nextFullHour();

      this.title = '';
      this.description = '';
      this.type = 'Meeting';
      this.location = '';
      this.start = toLocalDateTime(from);
      this.end = toLocalDateTime(new Date(from.getTime() + 60 * 60 * 1000));
      this.linked.set([]);
    }

    this.searchText = '';
    this.candidates.set([]);
  }

  /**
   * El título del cajón.
   *
   * Va en un método y no en un ternario dentro de la plantilla porque `$localize` usa comillas
   * invertidas, y la plantilla del componente también: escrito ahí, la cierra a media frase.
   */
  drawerTitle(): string {
    return this.calendarEvent() ? $localize`Modificar evento` : $localize`Nuevo evento`;
  }

  subtitle(): string {
    const calendarEvent = this.calendarEvent();
    return calendarEvent?.cancelledAtUtc ? 'Este evento está anulado' : '';
  }

  titleTooShort(): boolean {
    return this.title.trim().length > 0 && this.title.trim().length < 3;
  }

  endsBeforeStart(): boolean {
    return !!this.start && !!this.end && new Date(this.end) <= new Date(this.start);
  }

  canSave(): boolean {
    return this.title.trim().length >= 3 && !!this.start && !!this.end && !this.endsBeforeStart();
  }

  async search(text: string): Promise<void> {
    const query = text.trim();

    if (query.length < 2) {
      this.candidates.set([]);
      return;
    }

    // Sólo cosas, no personas: enlazar un evento con alguien sería invitarlo, que es otra
    // función y no existe todavía. Ofrecerlo aquí prometería algo que no pasa.
    const found = await this.mentions.search('#', query);
    const alreadyPlaced = new Set(this.linked().map(e => e.type + e.id));

    this.candidates.set(found.filter(c => !alreadyPlaced.has(c.type + c.id)));
  }

  addLink(candidate: MentionCandidate): void {
    // Uno de cada tipo: los enlaces son tres campos en el evento, no una lista. Añadir un
    // segundo ticket sustituye al primero en vez de perderse en silencio al guardar.
    this.linked.update(current => [...current.filter(e => e.type !== candidate.type), candidate]);
    this.searchText = '';
    this.candidates.set([]);
  }

  removeLink(candidate: MentionCandidate): void {
    this.linked.update(current => current.filter(e => e.id !== candidate.id));
  }

  async save(): Promise<void> {
    if (!this.canSave()) return;

    this.saving.set(true);

    const data: CalendarEventInput = {
      title: this.title.trim(),
      description: this.description.trim() || null,
      type: this.type,
      // Se manda en ISO con zona: el `datetime-local` da una hora sin huso, y mandarla tal cual
      // haría que el servidor la interpretara como UTC y el evento se moviera de hora.
      startTime: new Date(this.start).toISOString(),
      endTime: new Date(this.end).toISOString(),
      location: this.location.trim() || null,
      ...this.linksAsFields()
    };

    try {
      const calendarEvent = this.calendarEvent();

      const saved = calendarEvent
        ? await this.saveChanges(calendarEvent.id, data)
        : await this.calendar.create(data);

      this.toast.success(calendarEvent ? $localize`Evento modificado` : $localize`Evento creado`, saved.title);
      this.saved.emit(saved);
    } finally {
      // En `finally` para que un fallo no deje el botón girando para siempre. El aviso del error
      // lo levanta el interceptor.
      this.saving.set(false);
    }
  }

  /**
   * Modificar son dos llamadas: los datos y los enlaces.
   *
   * El comando de actualización no toca los enlaces —y no debe: `PATCH` no puede distinguir «quita
   * el enlace» de «no lo mandes»—, así que los enlaces van por su propio `PUT`, que los manda los
   * tres a la vez. Se hacen en orden y no en paralelo para que el evento que se emite arriba sea
   * el último estado, con los enlaces ya puestos.
   */
  private async saveChanges(id: string, data: CalendarEventInput) {
    await this.calendar.update(id, {
      title: data.title,
      description: data.description,
      startTime: data.startTime,
      endTime: data.endTime,
      location: data.location
    });

    return this.calendar.link(id, {
      projectId: data.projectId ?? null,
      taskId: data.taskId ?? null,
      ticketId: data.ticketId ?? null
    });
  }

  private linksAsFields() {
    const idOf = (type: MentionType) => this.linked().find(e => e.type === type)?.id ?? null;

    return {
      projectId: idOf('Project'),
      taskId: idOf('Task'),
      ticketId: idOf('Ticket')
    };
  }
}

/** Los enlaces que ya tiene un evento, en la forma que entiende el buscador. */
function linksOf(calendarEvent: CalendarEvent): MentionCandidate[] {
  const placed: MentionCandidate[] = [];

  // Sin el título de lo enlazado: el evento sólo trae los identificadores. Se enseña el tipo y se
  // deja el identificador acortado, que al menos permite reconocerlo y quitarlo. Ponerles nombre
  // exige pedir cada uno a su módulo, y es trabajo aparte.
  if (calendarEvent.projectId) placed.push({ id: calendarEvent.projectId, label: shortId(calendarEvent.projectId), type: 'Project' });
  if (calendarEvent.taskId) placed.push({ id: calendarEvent.taskId, label: shortId(calendarEvent.taskId), type: 'Task' });
  if (calendarEvent.ticketId) placed.push({ id: calendarEvent.ticketId, label: shortId(calendarEvent.ticketId), type: 'Ticket' });

  return placed;
}

const shortId = (id: string) => id.slice(0, 8);

/**
 * La próxima hora en punto.
 *
 * Proponer «ahora mismo» daría las 14:37, que nadie quiere como hora de reunión y hay que
 * corregir siempre.
 */
function nextFullHour(): Date {
  const date = new Date();
  date.setMinutes(0, 0, 0);
  date.setHours(date.getHours() + 1);
  return date;
}
