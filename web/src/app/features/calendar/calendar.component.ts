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
import { DiaDesplegadoComponent } from './dia-desplegado.component';
import { EventoDrawerComponent } from './evento-drawer.component';
import {
  CalendarioService, aFechaIso,
  type AgendaDeUnDia, type CosaDelDia, type EventoDelCalendario
} from './calendario.service';

/** Un día de la rejilla del mes. `null` en los huecos de antes del día 1. */
interface DiaDelMes {
  numero: number;
  fecha: Date;
  esHoy: boolean;
  eventos: EventoDelCalendario[];
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
  imports: [DatePipe, NgIcon, MenuContextualComponent, DiaDesplegadoComponent, EventoDrawerComponent],
  viewProviders: [provideIcons({
    lucideBan, lucideCalendarDays, lucideCalendarPlus, lucideCalendarRange, lucideChevronLeft,
    lucideChevronRight, lucideClipboardList, lucideEye, lucideFolderCheck, lucideLink,
    lucidePencil, lucideRotateCcw, lucideSettings, lucideSquareCheck, lucideTicket, lucideTrash2
  })],
  templateUrl: './calendar.component.html'
})
export class CalendarComponent implements OnInit {
  private readonly calendario = inject(CalendarioService);
  private readonly avisos = inject(ToastService);
  private readonly router = inject(Router);

  private readonly menu = viewChild.required(MenuContextualComponent);

  readonly eventos = signal<EventoDelCalendario[]>([]);
  readonly mesActual = signal(new Date());
  readonly cargando = signal(false);

  /** El día abierto, con su agenda. Nulo mientras se ve el mes. */
  readonly diaAbierto = signal<AgendaDeUnDia | null>(null);

  readonly enPapelera = signal<EventoDelCalendario[] | null>(null);

  // El cajón del formulario
  readonly drawerAbierto = signal(false);
  readonly eventoEnEdicion = signal<EventoDelCalendario | null>(null);
  readonly momentoPropuesto = signal<Date | null>(null);

  /** Sobre qué se abrió el menú: un día de la rejilla o un evento concreto. */
  private readonly objetoDelMenu = signal<{ dia: Date; evento: EventoDelCalendario | null } | null>(null);

  /**
   * Las abreviaturas de los días. Se escriben en vez de pedírselas al navegador porque
   * `toLocaleDateString` las devuelve en el idioma del sistema operativo, no en el de la
   * aplicación: con Windows en inglés saldrían «Mon, Tue» dentro de la versión española.
   */
  readonly DIAS_SEMANA = [
    $localize`Lun`, $localize`Mar`, $localize`Mié`,
    $localize`Jue`, $localize`Vie`, $localize`Sáb`, $localize`Dom`
  ];

  readonly mesLegible = computed(() =>
    this.mesActual().toLocaleDateString('es', { month: 'long', year: 'numeric' }));

  /**
   * La rejilla del mes, empezando en lunes.
   *
   * `getDay()` devuelve 0 para el domingo, y usarlo tal cual dejaba el mes desplazado un día: los
   * eventos aparecían en la columna equivocada, que es un fallo que se ve pero no se explica.
   */
  readonly diasDelMes = computed<(DiaDelMes | null)[]>(() => {
    const referencia = this.mesActual();
    const anio = referencia.getFullYear();
    const mes = referencia.getMonth();

    const primerDia = new Date(anio, mes, 1).getDay();
    const huecosAntes = (primerDia + 6) % 7;
    const cuantosDias = new Date(anio, mes + 1, 0).getDate();

    const hoy = new Date();
    const dias: (DiaDelMes | null)[] = Array(huecosAntes).fill(null);

    for (let numero = 1; numero <= cuantosDias; numero++) {
      const fecha = new Date(anio, mes, numero);

      dias.push({
        numero,
        fecha,
        esHoy: fecha.toDateString() === hoy.toDateString(),
        eventos: this.eventos().filter(e => mismoDia(new Date(e.startTime), fecha))
      });
    }

    return dias;
  });

  /** Lo que ofrece el menú del botón derecho. Depende de si se pulsó sobre un evento. */
  readonly opcionesDelMenu = computed<OpcionDelMenu[]>(() => {
    const sobre = this.objetoDelMenu();
    const evento = sobre?.evento ?? null;

    if (evento) {
      return [
        { clave: 'modificar', etiqueta: $localize`Modificar`, icono: 'lucidePencil' },
        { clave: 'enlazar', etiqueta: $localize`Enlazar con tarea, ticket o proyecto`, icono: 'lucideLink' },
        evento.canceladoEnUtc
          ? { clave: 'reactivar', etiqueta: $localize`Deshacer la anulación`, icono: 'lucideRotateCcw', separadorAntes: true }
          : { clave: 'anular', etiqueta: $localize`Cancelar el evento`, icono: 'lucideBan', separadorAntes: true },
        { clave: 'papelera', etiqueta: $localize`Enviar a la papelera`, icono: 'lucideTrash2', destructiva: true }
      ];
    }

    // Las del día. Es la lista que se pidió, en el orden en que se pidió.
    return [
      { clave: 'crear', etiqueta: $localize`Crear nuevo evento`, icono: 'lucideCalendarPlus' },
      { clave: 'ver-eventos', etiqueta: $localize`Ver eventos`, icono: 'lucideEye' },
      { clave: 'agenda', etiqueta: $localize`Ver agenda del día`, icono: 'lucideCalendarRange' },
      { clave: 'tareas', etiqueta: $localize`Tareas para entregar hoy`, icono: 'lucideSquareCheck', separadorAntes: true },
      { clave: 'tickets', etiqueta: $localize`Tickets del día`, icono: 'lucideTicket' },
      { clave: 'proyectos', etiqueta: $localize`Proyectos a finalizar hoy`, icono: 'lucideFolderCheck' },
      { clave: 'ajustes', etiqueta: $localize`Ajustes`, icono: 'lucideSettings', separadorAntes: true },
      { clave: 'papelera-dia', etiqueta: $localize`Enviar a la papelera los eventos`, icono: 'lucideTrash2', destructiva: true }
    ];
  });

  readonly tituloDelMenu = computed(() => {
    const sobre = this.objetoDelMenu();
    if (!sobre) return null;

    return sobre.evento
      ? sobre.evento.title
      : sobre.dia.toLocaleDateString('es', { weekday: 'long', day: 'numeric', month: 'long' });
  });

  ngOnInit(): void {
    void this.cargar();
  }

  // ── El mes ────────────────────────────────────────────────────────────────

  async cargar(): Promise<void> {
    this.cargando.set(true);

    try {
      const referencia = this.mesActual();
      const desde = new Date(referencia.getFullYear(), referencia.getMonth(), 1);
      const hasta = new Date(referencia.getFullYear(), referencia.getMonth() + 1, 0, 23, 59, 59);

      this.eventos.set(await this.calendario.eventosEntre(desde, hasta));
    } finally {
      this.cargando.set(false);
    }
  }

  mesAnterior(): void {
    this.moverMes(-1);
  }

  mesSiguiente(): void {
    this.moverMes(1);
  }

  private moverMes(cuantos: number): void {
    const referencia = this.mesActual();
    this.mesActual.set(new Date(referencia.getFullYear(), referencia.getMonth() + cuantos, 1));

    // Se cierra lo que estuviera abierto: un día de septiembre desplegado sobre el mes de octubre
    // enseñaría dos meses a la vez sin decir de cuál es cada cosa.
    this.diaAbierto.set(null);
    this.enPapelera.set(null);
    void this.cargar();
  }

  irAHoy(): void {
    this.mesActual.set(new Date());
    this.diaAbierto.set(null);
    this.enPapelera.set(null);
    void this.cargar();
  }

  // ── El día ────────────────────────────────────────────────────────────────

  async abrirDia(dia: Date): Promise<void> {
    this.enPapelera.set(null);
    this.diaAbierto.set(await this.calendario.agenda(dia));
  }

  cerrarDia(): void {
    this.diaAbierto.set(null);
  }

  /** Crea a una hora concreta del día abierto. */
  crearAlas(hora: number): void {
    const agenda = this.diaAbierto();
    if (!agenda) return;

    const momento = new Date(`${agenda.dia}T00:00:00`);
    momento.setHours(hora);
    this.abrirFormulario(null, momento);
  }

  /**
   * Al pulsar algo del día: si es un evento se modifica, y si es de otro módulo se va allí.
   *
   * Llevar a la tarea en vez de abrirla aquí es a propósito: el panel de una tarea tiene sus
   * comentarios, sus subtareas y sus campos, y una copia reducida dentro del calendario sería otra
   * pantalla que mantener y que se quedaría atrás.
   */
  abrirCosa(cosa: CosaDelDia): void {
    if (cosa.tipo === 'Evento') {
      const evento = this.eventos().find(e => e.id === cosa.id);
      if (evento) this.abrirFormulario(evento);
      return;
    }

    const rutas: Record<string, string> = {
      Tarea: '/tasks', Ticket: '/tickets', Proyecto: '/projects'
    };

    void this.router.navigate([rutas[cosa.tipo]], { queryParams: { id: cosa.id } });
  }

  // ── El menú del botón derecho ─────────────────────────────────────────────

  menuEnDia(evento: MouseEvent, dia: Date): void {
    this.objetoDelMenu.set({ dia, evento: null });
    this.menu().abrirEn(evento);
  }

  menuEnEvento(raton: MouseEvent, evento: EventoDelCalendario, dia: Date): void {
    raton.stopPropagation();
    this.objetoDelMenu.set({ dia, evento });
    this.menu().abrirEn(raton);
  }

  menuEnCosaDelDia({ evento, cosa }: { evento: MouseEvent; cosa: CosaDelDia }): void {
    const delCalendario = this.eventos().find(e => e.id === cosa.id) ?? null;
    const agenda = this.diaAbierto();

    this.objetoDelMenu.set({
      dia: agenda ? new Date(`${agenda.dia}T12:00:00`) : new Date(),
      evento: delCalendario
    });

    this.menu().abrirEn(evento);
  }

  menuEnHora({ evento, hora }: { evento: MouseEvent; hora: number }): void {
    const agenda = this.diaAbierto();
    if (!agenda) return;

    const momento = new Date(`${agenda.dia}T00:00:00`);
    momento.setHours(hora);

    this.objetoDelMenu.set({ dia: momento, evento: null });
    this.menu().abrirEn(evento);
  }

  async alElegirDelMenu(clave: string): Promise<void> {
    const sobre = this.objetoDelMenu();
    if (!sobre) return;

    const { dia, evento } = sobre;

    switch (clave) {
      case 'crear':
        this.abrirFormulario(null, aMediaManana(dia));
        return;

      case 'ver-eventos':
      case 'agenda':
        // Las dos abren el día: la agenda **es** la lista de eventos más lo que vence. Son dos
        // entradas porque se pidieron las dos, y llevan al mismo sitio porque partirlas en dos
        // pantallas casi iguales sería peor que tener una que lo diga todo.
        await this.abrirDia(dia);
        return;

      case 'tareas':
        void this.router.navigate(['/tasks'], { queryParams: { dueDate: aFechaIso(dia) } });
        return;

      case 'tickets':
        void this.router.navigate(['/tickets'], { queryParams: { startDate: aFechaIso(dia), endDate: aFechaIso(dia) } });
        return;

      case 'proyectos':
        void this.router.navigate(['/projects'], { queryParams: { endDate: aFechaIso(dia) } });
        return;

      case 'ajustes':
        void this.router.navigate(['/profile'], { queryParams: { seccion: 'notificaciones' } });
        return;

      case 'papelera-dia':
        await this.mandarElDiaALaPapelera(dia);
        return;
    }

    if (!evento) return;

    switch (clave) {
      case 'modificar':
      case 'enlazar':
        // Enlazar abre el mismo formulario: los enlaces son parte del evento, y una ventana
        // aparte para tres campos sería un sitio más donde buscarlos.
        this.abrirFormulario(evento);
        return;

      case 'anular':
        await this.anular(evento);
        return;

      case 'reactivar':
        await this.reactivar(evento);
        return;

      case 'papelera':
        await this.aLaPapelera(evento);
        return;
    }
  }

  // ── Acciones sobre un evento ──────────────────────────────────────────────

  private abrirFormulario(evento: EventoDelCalendario | null, momento: Date | null = null): void {
    this.eventoEnEdicion.set(evento);
    this.momentoPropuesto.set(momento);
    this.drawerAbierto.set(true);
  }

  crearEvento(): void {
    this.abrirFormulario(null);
  }

  cerrarFormulario(): void {
    this.drawerAbierto.set(false);
    this.eventoEnEdicion.set(null);
  }

  async alGuardar(): Promise<void> {
    this.cerrarFormulario();
    await this.refrescar();
  }

  private async anular(evento: EventoDelCalendario): Promise<void> {
    const motivo = prompt(`¿Por qué se anula «${evento.title}»?`) ?? null;

    await this.calendario.anular(evento.id, motivo);
    this.avisos.success($localize`Evento anulado`, 'Sigue en el calendario, tachado.');
    await this.refrescar();
  }

  private async reactivar(evento: EventoDelCalendario): Promise<void> {
    await this.calendario.reactivar(evento.id);
    this.avisos.success($localize`Evento reactivado`, evento.title);
    await this.refrescar();
  }

  private async aLaPapelera(evento: EventoDelCalendario): Promise<void> {
    if (!confirm(`¿Enviar «${evento.title}» a la papelera?`)) return;

    await this.calendario.aLaPapelera(evento.id);
    this.avisos.success($localize`En la papelera`, 'Se puede recuperar desde la papelera.');
    await this.refrescar();
  }

  /**
   * Manda a la papelera todos los eventos de un día.
   *
   * Se pide confirmación con el número dentro. «¿Enviar 7 eventos a la papelera?» es una pregunta
   * que se puede contestar; «¿estás seguro?» no dice cuánto se va a llevar por delante.
   */
  private async mandarElDiaALaPapelera(dia: Date): Promise<void> {
    const delDia = this.eventos().filter(e => mismoDia(new Date(e.startTime), dia));

    if (delDia.length === 0) {
      this.avisos.info($localize`Nada que enviar`, 'Este día no tiene eventos.');
      return;
    }

    if (!confirm(`¿Enviar ${delDia.length} evento${delDia.length > 1 ? 's' : ''} a la papelera?`)) return;

    // En serie y no en paralelo: son varias escrituras sobre el mismo agregado y lanzarlas a la
    // vez complica saber cuál falló si falla alguna.
    for (const evento of delDia) {
      await this.calendario.aLaPapelera(evento.id);
    }

    this.avisos.success($localize`En la papelera`, `${delDia.length} evento${delDia.length > 1 ? 's' : ''}.`);
    await this.refrescar();
  }

  // ── La papelera ───────────────────────────────────────────────────────────

  async verPapelera(): Promise<void> {
    this.diaAbierto.set(null);
    this.enPapelera.set(await this.calendario.papelera());
  }

  cerrarPapelera(): void {
    this.enPapelera.set(null);
  }

  async restaurar(evento: EventoDelCalendario): Promise<void> {
    await this.calendario.restaurar(evento.id);
    this.avisos.success($localize`Evento recuperado`, evento.title);

    this.enPapelera.set(await this.calendario.papelera());
    await this.cargar();
  }

  /** Vuelve a pedir el mes y, si hay un día abierto, también su agenda. */
  private async refrescar(): Promise<void> {
    await this.cargar();

    const agenda = this.diaAbierto();
    if (agenda) {
      this.diaAbierto.set(await this.calendario.agenda(new Date(`${agenda.dia}T12:00:00`)));
    }
  }
}

const mismoDia = (a: Date, b: Date) =>
  a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();

/** Las 9:00 del día. Es la hora que casi nadie tiene que corregir. */
function aMediaManana(dia: Date): Date {
  const momento = new Date(dia);
  momento.setHours(9, 0, 0, 0);
  return momento;
}
