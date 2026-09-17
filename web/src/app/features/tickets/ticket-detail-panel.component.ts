import { Component, inject, input, output, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { AvatarComponent } from '../../shared/ui/avatar.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideX, lucideCheck, lucideUser, lucideTag,
  lucideFlag, lucideMessageSquare, lucidePaperclip,
  lucideSend, lucideChevronDown, lucideMail, lucidePhone, lucideBuilding
} from '@ng-icons/lucide';
import type { AdjuntoDeTicket, Ticket } from './ticket-create-modal.component';
import { mensajeDeError } from '../../shared/utils/mensaje-de-error';
import { TICKET_TAGS, type Tag } from '../../shared/utils/tags';
import {
  ESTADOS_DE_TICKET, PRIORIDADES_DE_TICKET, insigniaDelEstado,
  nombreDeLaPrioridad, nombreDelEstado
} from './vocabulario-de-tickets';



import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { ComentariosComponent } from '../../shared/ui/comentarios.component';
import { MencionadoEnComponent } from '../../shared/ui/mencionado-en.component';

@Component({
  selector: 'app-ticket-detail-panel',
  standalone: true,
  imports: [MencionadoEnComponent, ComentariosComponent, ClickableDirective, FormsModule, DatePipe, BadgeComponent, AvatarComponent, NgIconComponent, DrawerComponent],
  viewProviders: [provideIcons({
    lucideX, lucideCheck, lucideUser, lucideTag,
    lucideFlag, lucideMessageSquare, lucidePaperclip,
    lucideSend, lucideChevronDown, lucideMail, lucidePhone, lucideBuilding
  })],
  templateUrl: './ticket-detail-panel.component.html',
})
export class TicketDetailPanelComponent implements OnInit {
  readonly ticket = input.required<Ticket>();
  readonly closed = output<void>();
  readonly updated = output<Ticket>();

  private readonly api = inject(ApiService);

  isEditing = false;
  isSaving = signal(false);
  title = '';
  description = '';
  status = '';
  priority = 'normal';
  selectedTags = signal<string[]>([]);
  showTagPicker = signal(false);
  activeTab = signal<'comments' | 'activity'>('comments');

  readonly statuses = ESTADOS_DE_TICKET;
  readonly priorities = PRIORIDADES_DE_TICKET;
  readonly availableTags = TICKET_TAGS;
  readonly desconocido = $localize`Sin datos de contacto`;

  clasificacion = '';
  readonly adjuntos = signal<AdjuntoDeTicket[]>([]);
  readonly subiendo = signal(false);
  readonly errorDeAdjuntos = signal('');

  statusBadge(s: string): BadgeVariant { return insigniaDelEstado(s); }

  readonly nombreDelEstado = nombreDelEstado;
  readonly nombreDeLaPrioridad = nombreDeLaPrioridad;

  ngOnInit(): void {
    const t = this.ticket();
    this.title = t.title;
    this.description = t.description ?? '';
    this.status = t.status;
    this.priority = t.priority ?? 'normal';
    this.clasificacion = t.clasificacion ?? '';
    // Leía `tags`, un campo que el servidor nunca mandó: la ficha abría siempre sin etiquetas.
    this.selectedTags.set((t.etiquetas ?? '').split(',').map(s => s.trim()).filter(Boolean));
    this.cargarAdjuntos();
  }

  cargarAdjuntos(): void {
    this.api.get<AdjuntoDeTicket[]>(`/tickets/${this.ticket().id}/adjuntos`).subscribe({
      next: adjuntos => this.adjuntos.set(adjuntos),
      error: () => this.adjuntos.set([]),
    });
  }

  /** La dirección con la que se abre: el almacenamiento en disco devuelve rutas relativas a la API. */
  urlDe(adjunto: AdjuntoDeTicket): string {
    return this.api.urlDeFichero(adjunto.url);
  }

  esVideo(adjunto: AdjuntoDeTicket): boolean {
    return adjunto.tipoDeContenido.startsWith('video/');
  }

  subirAdjuntos(entrada: HTMLInputElement): void {
    const ficheros = Array.from(entrada.files ?? []);
    if (ficheros.length === 0) return;

    const cuerpo = new FormData();
    for (const fichero of ficheros) cuerpo.append('attachments', fichero, fichero.name);

    this.subiendo.set(true);
    this.errorDeAdjuntos.set('');
    this.api.post<AdjuntoDeTicket[]>(`/tickets/${this.ticket().id}/adjuntos`, cuerpo).subscribe({
      next: nuevos => {
        this.adjuntos.update(actuales => [...actuales, ...nuevos]);
        this.subiendo.set(false);
        entrada.value = '';
      },
      error: err => {
        this.errorDeAdjuntos.set(mensajeDeError(err, $localize`No se pudieron subir los adjuntos`));
        this.subiendo.set(false);
        entrada.value = '';
      },
    });
  }

  guardarClasificacion(): void {
    const clasificacion = this.clasificacion.trim();
    if (clasificacion === (this.ticket().clasificacion ?? '')) return;

    this.api.patch(`/tickets/${this.ticket().id}`, { classification: clasificacion }).subscribe({
      next: () => this.updated.emit({ ...this.ticket(), clasificacion }),
      error: () => {},
    });
  }


  saveField(field: string, value: unknown): void {
    this.isSaving.set(true);
    this.api.patch(`/tickets/${this.ticket().id}`, {
      title: this.title, description: this.description, priority: this.priority, status: this.status
    }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.isEditing = false;
        this.updated.emit({ ...this.ticket(), title: this.title, description: this.description, status: this.status, priority: this.priority });
      },
      error: () => this.isSaving.set(false),
    });
  }

  changeStatus(newStatus: string): void {
    this.status = newStatus;
    this.api.patch(`/tickets/${this.ticket().id}`, { status: newStatus }).subscribe({
      next: () => this.updated.emit({ ...this.ticket(), status: newStatus }),
      error: () => {},
    });
  }

  toggleTag(key: string): void {
    this.selectedTags.update(tags =>
      tags.includes(key) ? tags.filter(t => t !== key) : [...tags, key]
    );
    // Como lista y en su propio PATCH. Antes iba por `saveField`, que no mandaba las etiquetas:
    // se veían marcadas y al volver a abrir el ticket no estaban.
    const etiquetas = this.selectedTags();
    this.api.patch(`/tickets/${this.ticket().id}`, { tags: etiquetas }).subscribe({
      next: () => this.updated.emit({ ...this.ticket(), etiquetas: etiquetas.join(',') }),
      error: () => {},
    });
  }

  isTagSelected(key: string): boolean {
    return this.selectedTags().includes(key);
  }

  getTag(key: string): Tag | undefined {
    return TICKET_TAGS.find(t => t.key === key);
  }



  close(): void { this.closed.emit(); }
}
