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
import { TagFieldComponent } from '../../shared/ui/tag-field.component';
import {
  TICKET_STATUSES, TICKET_PRIORITIES, statusBadge,
  priorityLabel, statusLabel
} from './ticket-vocabulary';



import { DrawerComponent } from '../../shared/ui/drawer.component';
import { CommentsComponent } from '../../shared/ui/comments.component';
import { MentionedInComponent } from '../../shared/ui/mentioned-in.component';

@Component({
  selector: 'app-ticket-detail-panel',
  standalone: true,
  imports: [MentionedInComponent, CommentsComponent, FormsModule, BadgeComponent, NgIconComponent, DrawerComponent, TagFieldComponent],
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
  /** Los ids de las etiquetas del ticket. */
  readonly tagIds = signal<string[]>([]);
  activeTab = signal<'comments' | 'activity'>('comments');

  readonly statuses = TICKET_STATUSES;
  readonly priorities = TICKET_PRIORITIES;
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
    this.tagIds.set(t.tagIds ?? []);
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

  /** Guarda las etiquetas como ids del módulo de etiquetas, en su propio PATCH. */
  changeTags(ids: string[]): void {
    this.tagIds.set(ids);
    this.api.patch(`/tickets/${this.ticket().id}`, { tagIds: ids }).subscribe({
      next: () => this.updated.emit({ ...this.ticket(), tagIds: ids }),
      error: () => {},
    });
  }

  close(): void { this.closed.emit(); }
}
