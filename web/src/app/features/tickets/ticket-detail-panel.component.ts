import { Component, inject, input, output, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { BadgeComponent } from '../../shared/ui/badge.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideX, lucideCheck, lucideUser, lucideTag,
  lucideFlag, lucideMessageSquare, lucidePaperclip,
  lucideSend, lucideChevronDown, lucideMail, lucidePhone, lucideBuilding
} from '@ng-icons/lucide';
import type { TicketAttachment, Ticket } from './ticket-create-modal.component';
import { mensajeDeError } from '../../shared/utils/mensaje-de-error';
import { TICKET_TAGS, type Tag } from '../../shared/utils/tags';
import {
  TICKET_STATUSES, TICKET_PRIORITIES, statusBadge,
  priorityLabel, statusLabel
} from './ticket-vocabulary';



import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { ComentariosComponent } from '../../shared/ui/comentarios.component';
import { MencionadoEnComponent } from '../../shared/ui/mencionado-en.component';

@Component({
  selector: 'app-ticket-detail-panel',
  standalone: true,
  imports: [MencionadoEnComponent, ComentariosComponent, ClickableDirective, FormsModule, BadgeComponent, NgIconComponent, DrawerComponent],
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

  readonly statuses = TICKET_STATUSES;
  readonly priorities = TICKET_PRIORITIES;
  readonly availableTags = TICKET_TAGS;
  readonly noContactData = $localize`Sin datos de contacto`;

  classification = '';
  readonly attachments = signal<TicketAttachment[]>([]);
  readonly uploading = signal(false);
  readonly attachmentsError = signal('');

  // Las tres se exponen tal cual, sin envolverlas en un método: un método con el mismo nombre
  // que la función importada se llamaría a sí mismo.
  readonly statusBadge = statusBadge;
  readonly statusLabel = statusLabel;
  readonly priorityLabel = priorityLabel;

  ngOnInit(): void {
    const t = this.ticket();
    this.title = t.title;
    this.description = t.description ?? '';
    this.status = t.status;
    this.priority = t.priority ?? 'normal';
    this.classification = t.classification ?? '';
    // Leía `tags`, un campo que el servidor nunca mandó: la ficha abría siempre sin etiquetas.
    this.selectedTags.set((t.tags ?? '').split(',').map(s => s.trim()).filter(Boolean));
    this.loadAttachments();
  }

  loadAttachments(): void {
    this.api.get<TicketAttachment[]>(`/tickets/${this.ticket().id}/attachments`).subscribe({
      next: attachments => this.attachments.set(attachments),
      error: () => this.attachments.set([]),
    });
  }

  /** La dirección con la que se abre: el almacenamiento en disco devuelve rutas relativas a la API. */
  fileUrl(attachment: TicketAttachment): string {
    return this.api.urlDeFichero(attachment.url);
  }

  isVideo(attachment: TicketAttachment): boolean {
    return attachment.contentType.startsWith('video/');
  }

  uploadAttachments(picker: HTMLInputElement): void {
    const files = Array.from(picker.files ?? []);
    if (files.length === 0) return;

    const body = new FormData();
    for (const file of files) body.append('attachments', file, file.name);

    this.uploading.set(true);
    this.attachmentsError.set('');
    this.api.post<TicketAttachment[]>(`/tickets/${this.ticket().id}/attachments`, body).subscribe({
      next: added => {
        this.attachments.update(current => [...current, ...added]);
        this.uploading.set(false);
        picker.value = '';
      },
      error: err => {
        this.attachmentsError.set(mensajeDeError(err, $localize`No se pudieron subir los adjuntos`));
        this.uploading.set(false);
        picker.value = '';
      },
    });
  }

  saveClassification(): void {
    const classification = this.classification.trim();
    if (classification === (this.ticket().classification ?? '')) return;

    this.api.patch(`/tickets/${this.ticket().id}`, { classification }).subscribe({
      next: () => this.updated.emit({ ...this.ticket(), classification }),
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
    const keys = this.selectedTags();
    this.api.patch(`/tickets/${this.ticket().id}`, { tags: keys }).subscribe({
      next: () => this.updated.emit({ ...this.ticket(), tags: keys.join(',') }),
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
