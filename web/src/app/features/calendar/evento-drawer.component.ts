import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCalendarDays, lucideLink, lucideLoaderCircle } from '@ng-icons/lucide';

import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ToastService } from '../../shared/services/toast.service';
import { MentionsService } from '../docs/mentions.service';
import type { MentionCandidate } from '../docs/extensions/mention';
import {
  CalendarioService, aFechaHoraLocal,
  type DatosDelEvento, type EventoDelCalendario
} from './calendario.service';

/** Los tipos que entiende el servidor, con su nombre en castellano. */
const TIPOS: { clave: string; etiqueta: string }[] = [
  { clave: 'Meeting', etiqueta: $localize`Reunión` },
  { clave: 'Appointment', etiqueta: $localize`Cita` },
  { clave: 'Task', etiqueta: $localize`Tarea` },
  { clave: 'Reminder', etiqueta: $localize`Recordatorio` },
  { clave: 'OutOfOffice', etiqueta: $localize`Fuera de oficina` },
  { clave: 'Holiday', etiqueta: $localize`Día festivo` }
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
  selector: 'app-evento-drawer',
  standalone: true,
  imports: [FormsModule, DrawerComponent, NgIcon],
  viewProviders: [provideIcons({ lucideCalendarDays, lucideLink, lucideLoaderCircle })],
  template: `
    <app-drawer
      [isOpen]="abierto()"
      [title]="titulodelCajon()"
      [subtitle]="subtitulo()"
      size="md"
      (closed)="cerrar.emit()">

      <ng-icon drawer-icon name="lucideCalendarDays" size="20" class="text-primary" />

      <div class="space-y-4">
        <div>
          <label for="titulo" class="mb-1 block text-sm font-medium" i18n>Título</label>
          <input id="titulo" [(ngModel)]="titulo" required
            class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                   focus:outline-none focus:ring-2 focus:ring-ring" />
          @if (tituloCorto()) {
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
            <input id="inicio" type="datetime-local" [(ngModel)]="inicio"
              class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label for="fin" class="mb-1 block text-sm font-medium" i18n>Termina</label>
            <input id="fin" type="datetime-local" [(ngModel)]="fin"
              class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring" />
            @if (finAntesDelInicio()) {
              <p class="mt-1 text-xs text-destructive" i18n>Tiene que terminar después de empezar.</p>
            }
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div>
            <label for="tipo" class="mb-1 block text-sm font-medium" i18n>Tipo</label>
            <select id="tipo" [(ngModel)]="tipo"
              class="h-9 w-full rounded-md border border-border bg-background px-2 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring">
              @for (t of TIPOS; track t.clave) {
                <option [value]="t.clave">{{ t.etiqueta }}</option>
              }
            </select>
          </div>
          <div>
            <label for="sitio" class="mb-1 block text-sm font-medium" i18n>Dónde</label>
            <input id="sitio" [(ngModel)]="sitio" placeholder="Sala, enlace…"
              class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
        </div>

        <div>
          <label for="descripcion" class="mb-1 block text-sm font-medium" i18n>Descripción</label>
          <textarea id="descripcion" [(ngModel)]="descripcion" rows="3"
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

          @for (e of enlazados(); track e.id) {
            <div class="flex items-center justify-between rounded-md bg-secondary px-2 py-1.5 text-sm">
              <span class="truncate"><span class="text-muted-foreground">{{ e.tipo }} ·</span> {{ e.etiqueta }}</span>
              <button type="button" (click)="quitarEnlace(e)"
                class="ml-2 shrink-0 text-xs text-muted-foreground hover:text-destructive
                       focus:outline-none focus:ring-2 focus:ring-ring rounded"
                i18n>Quitar</button>
            </div>
          }

          <div class="relative">
            <input
              [(ngModel)]="busqueda"
              (ngModelChange)="buscar($event)"
              i18n-placeholder placeholder="Buscar…"
              class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm
                     focus:outline-none focus:ring-2 focus:ring-ring" />

            @if (candidatos().length > 0) {
              <ul class="absolute z-10 mt-1 max-h-52 w-full overflow-y-auto rounded-md border border-border bg-card shadow-lg">
                @for (c of candidatos(); track c.tipo + c.id) {
                  <li>
                    <button type="button" (click)="anadirEnlace(c)"
                      class="flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm hover:bg-accent
                             focus:outline-none focus:bg-accent">
                      <span class="text-xs text-muted-foreground">{{ c.tipo }}</span>
                      <span class="truncate">{{ c.etiqueta }}</span>
                    </button>
                  </li>
                }
              </ul>
            }
          </div>
        </div>
      </div>

      <div drawer-footer class="flex items-center gap-3">
        <button type="button" (click)="cerrar.emit()"
          class="h-9 rounded-md border border-border px-4 text-sm font-medium hover:bg-accent
                 focus:outline-none focus:ring-2 focus:ring-ring" i18n>
          Cancelar
        </button>

        <button type="button" (click)="guardar()" [disabled]="!sePuedeGuardar() || guardando()"
          class="inline-flex h-9 items-center gap-2 rounded-md bg-primary px-4 text-sm font-medium
                 text-primary-foreground hover:bg-primary/90 disabled:opacity-50
                 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-ring">
          @if (guardando()) { <ng-icon name="lucideLoaderCircle" size="14" class="animate-spin" /> }
          <span i18n>Guardar</span>
        </button>
      </div>
    </app-drawer>
  `
})
export class EventoDrawerComponent {
  private readonly calendario = inject(CalendarioService);
  private readonly menciones = inject(MentionsService);
  private readonly avisos = inject(ToastService);

  readonly TIPOS = TIPOS;

  readonly abierto = input.required<boolean>();

  /** El evento a modificar, o nulo para crear uno nuevo. */
  readonly evento = input<EventoDelCalendario | null>(null);

  /** El día y la hora que se proponen al crear: los del sitio donde se pulsó. */
  readonly momentoPropuesto = input<Date | null>(null);

  readonly cerrar = output<void>();
  readonly guardado = output<EventoDelCalendario>();

  titulo = '';
  descripcion = '';
  tipo = 'Meeting';
  sitio = '';
  inicio = '';
  fin = '';
  busqueda = '';

  readonly enlazados = signal<MentionCandidate[]>([]);
  readonly candidatos = signal<MentionCandidate[]>([]);
  readonly guardando = signal(false);

  constructor() {
    // Se rellena al abrirse, no en el constructor: el cajón es el mismo componente para crear y
    // para modificar, y si no se recargara enseñaría los datos del evento anterior.
    effect(() => {
      if (!this.abierto()) return;
      this.rellenar();
    });
  }

  private rellenar(): void {
    const evento = this.evento();

    if (evento) {
      this.titulo = evento.title;
      this.descripcion = evento.description ?? '';
      this.tipo = evento.type;
      this.sitio = evento.location ?? '';
      this.inicio = aFechaHoraLocal(new Date(evento.startTime));
      this.fin = aFechaHoraLocal(new Date(evento.endTime));
      this.enlazados.set(enlacesDe(evento));
    } else {
      const desde = this.momentoPropuesto() ?? proximaHoraEnPunto();

      this.titulo = '';
      this.descripcion = '';
      this.tipo = 'Meeting';
      this.sitio = '';
      this.inicio = aFechaHoraLocal(desde);
      this.fin = aFechaHoraLocal(new Date(desde.getTime() + 60 * 60 * 1000));
      this.enlazados.set([]);
    }

    this.busqueda = '';
    this.candidatos.set([]);
  }

  /**
   * El título del cajón.
   *
   * Va en un método y no en un ternario dentro de la plantilla porque `$localize` usa comillas
   * invertidas, y la plantilla del componente también: escrito ahí, la cierra a media frase.
   */
  titulodelCajon(): string {
    return this.evento() ? $localize`Modificar evento` : $localize`Nuevo evento`;
  }

  subtitulo(): string {
    const evento = this.evento();
    return evento?.canceladoEnUtc ? 'Este evento está anulado' : '';
  }

  tituloCorto(): boolean {
    return this.titulo.trim().length > 0 && this.titulo.trim().length < 3;
  }

  finAntesDelInicio(): boolean {
    return !!this.inicio && !!this.fin && new Date(this.fin) <= new Date(this.inicio);
  }

  sePuedeGuardar(): boolean {
    return this.titulo.trim().length >= 3 && !!this.inicio && !!this.fin && !this.finAntesDelInicio();
  }

  async buscar(texto: string): Promise<void> {
    const consulta = texto.trim();

    if (consulta.length < 2) {
      this.candidatos.set([]);
      return;
    }

    // Sólo cosas, no personas: enlazar un evento con alguien sería invitarlo, que es otra
    // función y no existe todavía. Ofrecerlo aquí prometería algo que no pasa.
    const encontrados = await this.menciones.search('#', consulta);
    const yaPuestos = new Set(this.enlazados().map(e => e.tipo + e.id));

    this.candidatos.set(encontrados.filter(c => !yaPuestos.has(c.tipo + c.id)));
  }

  anadirEnlace(candidato: MentionCandidate): void {
    // Uno de cada tipo: los enlaces son tres campos en el evento, no una lista. Añadir un
    // segundo ticket sustituye al primero en vez de perderse en silencio al guardar.
    this.enlazados.update(actuales => [...actuales.filter(e => e.tipo !== candidato.tipo), candidato]);
    this.busqueda = '';
    this.candidatos.set([]);
  }

  quitarEnlace(candidato: MentionCandidate): void {
    this.enlazados.update(actuales => actuales.filter(e => e.id !== candidato.id));
  }

  async guardar(): Promise<void> {
    if (!this.sePuedeGuardar()) return;

    this.guardando.set(true);

    const datos: DatosDelEvento = {
      title: this.titulo.trim(),
      description: this.descripcion.trim() || null,
      type: this.tipo,
      // Se manda en ISO con zona: el `datetime-local` da una hora sin huso, y mandarla tal cual
      // haría que el servidor la interpretara como UTC y el evento se moviera de hora.
      startTime: new Date(this.inicio).toISOString(),
      endTime: new Date(this.fin).toISOString(),
      location: this.sitio.trim() || null,
      ...this.enlacesComoCampos()
    };

    try {
      const evento = this.evento();

      const guardado = evento
        ? await this.guardarModificacion(evento.id, datos)
        : await this.calendario.crear(datos);

      this.avisos.success(evento ? $localize`Evento modificado` : $localize`Evento creado`, guardado.title);
      this.guardado.emit(guardado);
    } finally {
      // En `finally` para que un fallo no deje el botón girando para siempre. El aviso del error
      // lo levanta el interceptor.
      this.guardando.set(false);
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
  private async guardarModificacion(id: string, datos: DatosDelEvento) {
    await this.calendario.modificar(id, {
      title: datos.title,
      description: datos.description,
      startTime: datos.startTime,
      endTime: datos.endTime,
      location: datos.location
    });

    return this.calendario.enlazar(id, {
      projectId: datos.projectId ?? null,
      taskId: datos.taskId ?? null,
      ticketId: datos.ticketId ?? null
    });
  }

  private enlacesComoCampos() {
    const de = (tipo: string) => this.enlazados().find(e => e.tipo === tipo)?.id ?? null;

    return {
      projectId: de('Proyecto'),
      taskId: de('Tarea'),
      ticketId: de('Ticket')
    };
  }
}

/** Los enlaces que ya tiene un evento, en la forma que entiende el buscador. */
function enlacesDe(evento: EventoDelCalendario): MentionCandidate[] {
  const puestos: MentionCandidate[] = [];

  // Sin el título de lo enlazado: el evento sólo trae los identificadores. Se enseña el tipo y se
  // deja el identificador acortado, que al menos permite reconocerlo y quitarlo. Ponerles nombre
  // exige pedir cada uno a su módulo, y es trabajo aparte.
  if (evento.projectId) puestos.push({ id: evento.projectId, etiqueta: corto(evento.projectId), tipo: 'Proyecto' });
  if (evento.taskId) puestos.push({ id: evento.taskId, etiqueta: corto(evento.taskId), tipo: 'Tarea' });
  if (evento.ticketId) puestos.push({ id: evento.ticketId, etiqueta: corto(evento.ticketId), tipo: 'Ticket' });

  return puestos;
}

const corto = (id: string) => id.slice(0, 8);

/**
 * La próxima hora en punto.
 *
 * Proponer «ahora mismo» daría las 14:37, que nadie quiere como hora de reunión y hay que
 * corregir siempre.
 */
function proximaHoraEnPunto(): Date {
  const fecha = new Date();
  fecha.setMinutes(0, 0, 0);
  fecha.setHours(fecha.getHours() + 1);
  return fecha;
}
