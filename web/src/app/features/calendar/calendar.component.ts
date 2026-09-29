import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideBan, lucideCalendarDays, lucideCalendarPlus, lucideCalendarRange, lucideChevronLeft,
  lucideChevronRight, lucideClipboardList, lucideEye, lucideFolderCheck, lucideLink,
  lucidePencil, lucideRotateCcw, lucideSettings, lucideSquareCheck, lucideTicket, lucideTrash2
} from '@ng-icons/lucide';

import { ToastService } from '../../shared/services/toast.service';
import { MenuContextualComponent, type OpcionDelMenu } from '../../shared/ui/menu-contextual.component';
import { ExpandedDayComponent } from './expanded-day.component';
import { EventDrawerComponent } from './event-drawer.component';
import {
  CalendarService, toIsoDate,
  type DailyAgenda, type AgendaItem, type CalendarEvent
} from './calendar.service';

/** Un día de la rejilla del mes. `null` en los huecos de antes del día 1. */
interface MonthDay {
  dayNumber: number;
  date: Date;
  isToday: boolean;
  events: CalendarEvent[];
}

/**
 * El calendario: el mes, un día desplegado, y el menú del botón derecho.
 *
 * <b>Antes no enseñaba nada.</b> Pedía <code>?from=&to=</code> donde la API lee
 * <code>startDate</code> y <code>endDate</code>, y trataba la respuesta —<code>{ items, … }</code>—
 * como si fuera un array; el fallo caía en un <code>error: () =&gt; {}</code>, así que el mes salía
 * vacío tuviera lo que tuviera y nadie veía un error. Crear un evento mandaba
 * <code>startsAtUtc</code>, que el comando no conoce, y también fallaba en silencio.
 *
 * Ahora todo lo que habla con el servidor pasa por <code>CalendarioService</code>, que es donde
 * están los nombres del contrato una sola vez.
 */
@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [DatePipe, NgIcon, MenuContextualComponent, ExpandedDayComponent, EventDrawerComponent],
  viewProviders: [provideIcons({
    lucideBan, lucideCalendarDays, lucideCalendarPlus, lucideCalendarRange, lucideChevronLeft,
    lucideChevronRight, lucideClipboardList, lucideEye, lucideFolderCheck, lucideLink,
    lucidePencil, lucideRotateCcw, lucideSettings, lucideSquareCheck, lucideTicket, lucideTrash2
  })],
  templateUrl: './calendar.component.html'
})
export class CalendarComponent implements OnInit {
  private readonly calendar = inject(CalendarService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  private readonly menu = viewChild.required(MenuContextualComponent);

  readonly events = signal<CalendarEvent[]>([]);
  readonly currentMonth = signal(new Date());
  readonly loading = signal(false);

  /** El día abierto, con su agenda. Nulo mientras se ve el mes. */
  readonly expandedDay = signal<DailyAgenda | null>(null);

  readonly trashed = signal<CalendarEvent[] | null>(null);

  // El cajón del formulario
  readonly drawerOpen = signal(false);
  readonly editingEvent = signal<CalendarEvent | null>(null);
  readonly proposedTime = signal<Date | null>(null);

  /** Sobre qué se abrió el menú: un día de la rejilla o un evento concreto. */
  private readonly menuTarget = signal<{ day: Date; calendarEvent: CalendarEvent | null } | null>(null);

  /**
   * Las abreviaturas de los días. Se escriben en vez de pedírselas al navegador porque
   * `toLocaleDateString` las devuelve en el idioma del sistema operativo, no en el de la
   * aplicación: con Windows en inglés saldrían «Mon, Tue» dentro de la versión española.
   */
  readonly WEEKDAYS = [
    $localize`Lun`, $localize`Mar`, $localize`Mié`,
    $localize`Jue`, $localize`Vie`, $localize`Sáb`, $localize`Dom`
  ];

  readonly monthLabel = computed(() =>
    this.currentMonth().toLocaleDateString('es', { month: 'long', year: 'numeric' }));

  /**
   * La rejilla del mes, empezando en lunes.
   *
   * `getDay()` devuelve 0 para el domingo, y usarlo tal cual dejaba el mes desplazado un día: los
   * eventos aparecían en la columna equivocada, que es un fallo que se ve pero no se explica.
   */
  readonly monthDays = computed<(MonthDay | null)[]>(() => {
    const reference = this.currentMonth();
    const anio = reference.getFullYear();
    const month = reference.getMonth();

    const firstDay = new Date(anio, month, 1).getDay();
    const leadingBlanks = (firstDay + 6) % 7;
    const dayCount = new Date(anio, month + 1, 0).getDate();

    const today = new Date();
    const days: (MonthDay | null)[] = Array(leadingBlanks).fill(null);

    for (let dayNumber = 1; dayNumber <= dayCount; dayNumber++) {
      const date = new Date(anio, month, dayNumber);

      days.push({
        dayNumber,
        date,
        isToday: date.toDateString() === today.toDateString(),
        events: this.events().filter(e => isSameDay(new Date(e.startTime), date))
      });
    }

    return days;
  });

  /** Lo que ofrece el menú del botón derecho. Depende de si se pulsó sobre un evento. */
  readonly menuOptions = computed<OpcionDelMenu[]>(() => {
    const target = this.menuTarget();
    const calendarEvent = target?.calendarEvent ?? null;

    if (calendarEvent) {
      return [
        { clave: 'edit', etiqueta: $localize`Modificar`, icono: 'lucidePencil' },
        { clave: 'link', etiqueta: $localize`Enlazar con tarea, ticket o proyecto`, icono: 'lucideLink' },
        calendarEvent.cancelledAtUtc
          ? { clave: 'reactivate', etiqueta: $localize`Deshacer la anulación`, icono: 'lucideRotateCcw', separadorAntes: true }
          : { clave: 'cancel', etiqueta: $localize`Cancelar el evento`, icono: 'lucideBan', separadorAntes: true },
        { clave: 'trash', etiqueta: $localize`Enviar a la papelera`, icono: 'lucideTrash2', destructiva: true }
      ];
    }

    // Las del día. Es la lista que se pidió, en el orden en que se pidió.
    return [
      { clave: 'create', etiqueta: $localize`Crear nuevo evento`, icono: 'lucideCalendarPlus' },
      { clave: 'view-events', etiqueta: $localize`Ver eventos`, icono: 'lucideEye' },
      { clave: 'agenda', etiqueta: $localize`Ver agenda del día`, icono: 'lucideCalendarRange' },
      { clave: 'tasks', etiqueta: $localize`Tareas para entregar hoy`, icono: 'lucideSquareCheck', separadorAntes: true },
      { clave: 'tickets', etiqueta: $localize`Tickets del día`, icono: 'lucideTicket' },
      { clave: 'projects', etiqueta: $localize`Proyectos a finalizar hoy`, icono: 'lucideFolderCheck' },
      { clave: 'settings', etiqueta: $localize`Ajustes`, icono: 'lucideSettings', separadorAntes: true },
      { clave: 'trash-day', etiqueta: $localize`Enviar a la papelera los eventos`, icono: 'lucideTrash2', destructiva: true }
    ];
  });

  readonly menuTitle = computed(() => {
    const target = this.menuTarget();
    if (!target) return null;

    return target.calendarEvent
      ? target.calendarEvent.title
      : target.day.toLocaleDateString('es', { weekday: 'long', day: 'numeric', month: 'long' });
  });

  ngOnInit(): void {
    void this.load();
  }

  // ── El mes ────────────────────────────────────────────────────────────────

  async load(): Promise<void> {
    this.loading.set(true);

    try {
      const reference = this.currentMonth();
      const from = new Date(reference.getFullYear(), reference.getMonth(), 1);
      const to = new Date(reference.getFullYear(), reference.getMonth() + 1, 0, 23, 59, 59);

      this.events.set(await this.calendar.eventsBetween(from, to));
    } finally {
      this.loading.set(false);
    }
  }

  previousMonth(): void {
    this.shiftMonth(-1);
  }

  nextMonth(): void {
    this.shiftMonth(1);
  }

  private shiftMonth(count: number): void {
    const reference = this.currentMonth();
    this.currentMonth.set(new Date(reference.getFullYear(), reference.getMonth() + count, 1));

    // Se cierra lo que estuviera abierto: un día de septiembre desplegado sobre el mes de octubre
    // enseñaría dos meses a la vez sin decir de cuál es cada cosa.
    this.expandedDay.set(null);
    this.trashed.set(null);
    void this.load();
  }

  goToToday(): void {
    this.currentMonth.set(new Date());
    this.expandedDay.set(null);
    this.trashed.set(null);
    void this.load();
  }

  // ── El día ────────────────────────────────────────────────────────────────

  async openDay(day: Date): Promise<void> {
    this.trashed.set(null);
    this.expandedDay.set(await this.calendar.agenda(day));
  }

  closeDay(): void {
    this.expandedDay.set(null);
  }

  /** Crea a una hora concreta del día abierto. */
  createAt(hour: number): void {
    const agenda = this.expandedDay();
    if (!agenda) return;

    const moment = new Date(`${agenda.day}T00:00:00`);
    moment.setHours(hour);
    this.openForm(null, moment);
  }

  /**
   * Al pulsar algo del día: si es un evento se modifica, y si es de otro módulo se va allí.
   *
   * Llevar a la tarea en vez de abrirla aquí es a propósito: el panel de una tarea tiene sus
   * comentarios, sus subtareas y sus campos, y una copia reducida dentro del calendario sería otra
   * pantalla que mantener y que se quedaría atrás.
   */
  openItem(item: AgendaItem): void {
    if (item.type === 'Event') {
      const calendarEvent = this.events().find(e => e.id === item.id);
      if (calendarEvent) this.openForm(calendarEvent);
      return;
    }

    const routes: Record<string, string> = {
      Task: '/tasks', Ticket: '/tickets', Project: '/projects'
    };

    void this.router.navigate([routes[item.type]], { queryParams: { id: item.id } });
  }

  // ── El menú del botón derecho ─────────────────────────────────────────────

  onDayContextMenu(mouseEvent: MouseEvent, day: Date): void {
    this.menuTarget.set({ day, calendarEvent: null });
    this.menu().abrirEn(mouseEvent);
  }

  onEventContextMenu(mouseEvent: MouseEvent, calendarEvent: CalendarEvent, day: Date): void {
    mouseEvent.stopPropagation();
    this.menuTarget.set({ day, calendarEvent });
    this.menu().abrirEn(mouseEvent);
  }

  onItemContextMenu({ mouseEvent, item }: { mouseEvent: MouseEvent; item: AgendaItem }): void {
    const matchingEvent = this.events().find(e => e.id === item.id) ?? null;
    const agenda = this.expandedDay();

    this.menuTarget.set({
      day: agenda ? new Date(`${agenda.day}T12:00:00`) : new Date(),
      calendarEvent: matchingEvent
    });

    this.menu().abrirEn(mouseEvent);
  }

  onHourContextMenu({ mouseEvent, hour }: { mouseEvent: MouseEvent; hour: number }): void {
    const agenda = this.expandedDay();
    if (!agenda) return;

    const moment = new Date(`${agenda.day}T00:00:00`);
    moment.setHours(hour);

    this.menuTarget.set({ day: moment, calendarEvent: null });
    this.menu().abrirEn(mouseEvent);
  }

  async onMenuChoice(key: string): Promise<void> {
    const target = this.menuTarget();
    if (!target) return;

    const { day, calendarEvent } = target;

    switch (key) {
      case 'create':
        this.openForm(null, atMidMorning(day));
        return;

      case 'view-events':
      case 'agenda':
        // Las dos abren el día: la agenda **es** la lista de eventos más lo que vence. Son dos
        // entradas porque se pidieron las dos, y llevan al mismo sitio porque partirlas en dos
        // pantallas casi iguales sería peor que tener una que lo diga todo.
        await this.openDay(day);
        return;

      case 'tasks':
        void this.router.navigate(['/tasks'], { queryParams: { dueDate: toIsoDate(day) } });
        return;

      case 'tickets':
        void this.router.navigate(['/tickets'], { queryParams: { startDate: toIsoDate(day), endDate: toIsoDate(day) } });
        return;

      case 'projects':
        void this.router.navigate(['/projects'], { queryParams: { endDate: toIsoDate(day) } });
        return;

      case 'settings':
        void this.router.navigate(['/profile'], { queryParams: { section: 'notificaciones' } });
        return;

      case 'trash-day':
        await this.trashDayEvents(day);
        return;
    }

    if (!calendarEvent) return;

    switch (key) {
      case 'edit':
      case 'link':
        // Enlazar abre el mismo formulario: los enlaces son parte del evento, y una ventana
        // aparte para tres campos sería un sitio más donde buscarlos.
        this.openForm(calendarEvent);
        return;

      case 'cancel':
        await this.cancel(calendarEvent);
        return;

      case 'reactivate':
        await this.reactivate(calendarEvent);
        return;

      case 'trash':
        await this.moveToTrash(calendarEvent);
        return;
    }
  }

  // ── Acciones sobre un evento ──────────────────────────────────────────────

  private openForm(calendarEvent: CalendarEvent | null, moment: Date | null = null): void {
    this.editingEvent.set(calendarEvent);
    this.proposedTime.set(moment);
    this.drawerOpen.set(true);
  }

  createEvent(): void {
    this.openForm(null);
  }

  closeForm(): void {
    this.drawerOpen.set(false);
    this.editingEvent.set(null);
  }

  async onSaved(): Promise<void> {
    this.closeForm();
    await this.refresh();
  }

  private async cancel(calendarEvent: CalendarEvent): Promise<void> {
    const reason = prompt(`¿Por qué se anula «${calendarEvent.title}»?`) ?? null;

    await this.calendar.cancel(calendarEvent.id, reason);
    this.toast.success($localize`Evento anulado`, 'Sigue en el calendario, tachado.');
    await this.refresh();
  }

  private async reactivate(calendarEvent: CalendarEvent): Promise<void> {
    await this.calendar.reactivate(calendarEvent.id);
    this.toast.success($localize`Evento reactivado`, calendarEvent.title);
    await this.refresh();
  }

  private async moveToTrash(calendarEvent: CalendarEvent): Promise<void> {
    if (!confirm(`¿Enviar «${calendarEvent.title}» a la papelera?`)) return;

    await this.calendar.moveToTrash(calendarEvent.id);
    this.toast.success($localize`En la papelera`, 'Se puede recuperar desde la papelera.');
    await this.refresh();
  }

  /**
   * Manda a la papelera todos los eventos de un día.
   *
   * Se pide confirmación con el número dentro. «¿Enviar 7 eventos a la papelera?» es una pregunta
   * que se puede contestar; «¿estás seguro?» no dice cuánto se va a llevar por delante.
   */
  private async trashDayEvents(day: Date): Promise<void> {
    const dayEvents = this.events().filter(e => isSameDay(new Date(e.startTime), day));

    if (dayEvents.length === 0) {
      this.toast.info($localize`Nada que enviar`, 'Este día no tiene eventos.');
      return;
    }

    if (!confirm(`¿Enviar ${dayEvents.length} evento${dayEvents.length > 1 ? 's' : ''} a la papelera?`)) return;

    // En serie y no en paralelo: son varias escrituras sobre el mismo agregado y lanzarlas a la
    // vez complica saber cuál falló si falla alguna.
    for (const calendarEvent of dayEvents) {
      await this.calendar.moveToTrash(calendarEvent.id);
    }

    this.toast.success($localize`En la papelera`, `${dayEvents.length} evento${dayEvents.length > 1 ? 's' : ''}.`);
    await this.refresh();
  }

  // ── La papelera ───────────────────────────────────────────────────────────

  async showTrash(): Promise<void> {
    this.expandedDay.set(null);
    this.trashed.set(await this.calendar.trash());
  }

  closeTrash(): void {
    this.trashed.set(null);
  }

  async restore(calendarEvent: CalendarEvent): Promise<void> {
    await this.calendar.restore(calendarEvent.id);
    this.toast.success($localize`Evento recuperado`, calendarEvent.title);

    this.trashed.set(await this.calendar.trash());
    await this.load();
  }

  /** Vuelve a pedir el mes y, si hay un día abierto, también su agenda. */
  private async refresh(): Promise<void> {
    await this.load();

    const agenda = this.expandedDay();
    if (agenda) {
      this.expandedDay.set(await this.calendar.agenda(new Date(`${agenda.day}T12:00:00`)));
    }
  }
}

const isSameDay = (a: Date, b: Date) =>
  a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();

/** Las 9:00 del día. Es la hora que casi nadie tiene que corregir. */
function atMidMorning(day: Date): Date {
  const moment = new Date(day);
  moment.setHours(9, 0, 0, 0);
  return moment;
}
