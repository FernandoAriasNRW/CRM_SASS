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
import { errorMessage } from '../../shared/utils/error-message';
import { TagFieldComponent } from '../../shared/ui/tag-field.component';
import {
  TICKET_STATUSES, TICKET_PRIORITIES, statusBadge,
  priorityLabel, statusLabel
} from './ticket-vocabulary';



import { DrawerComponent } from '../../shared/ui/drawer.component';
import { CommentsComponent } from '../../shared/ui/comments.component';
import { MentionedInComponent } from '../../shared/ui/mentioned-in.component';
import { CustomFieldsFormComponent } from '../../shared/ui/custom-fields-form.component';

import { LifecycleActionsComponent, type LifecycleChange } from '../../shared/ui/lifecycle-actions.component';
@Component({
  selector: 'app-ticket-detail-panel',
  standalone: true,
  imports: [CustomFieldsFormComponent, LifecycleActionsComponent, MentionedInComponent, CommentsComponent, FormsModule, BadgeComponent, NgIconComponent, DrawerComponent, TagFieldComponent],
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
  /** Archivado, a la papelera o restaurado: la lista se recarga y la ficha se cierra. */
  readonly lifecycleChanged = output<LifecycleChange>();

  private readonly api = inject(ApiService);

  /**
   * El ticket tal como está guardado. Cada cambio se emite sobre él y, si el servidor lo rechaza,
   * se vuelve a él. No sirve la entrada `ticket`: es el ticket con el que se abrió la ficha, y
   * emitir sobre ella deshacía en la lista lo guardado antes.
   */
  private saved!: Ticket;

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
  /** El adjunto que espera confirmación para quitarse: se confirma en su propia tarjeta. */
  readonly removingAttachmentId = signal<string | null>(null);

  // Las tres se exponen tal cual, sin envolverlas en un método: un método con el mismo nombre
  // que la función importada se llamaría a sí mismo.
  readonly statusBadge = statusBadge;
  readonly statusLabel = statusLabel;
  readonly priorityLabel = priorityLabel;

  ngOnInit(): void {
    const t = this.ticket();
    this.saved = t;
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
    return this.api.fileUrl(attachment.url);
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
        this.attachmentsError.set(errorMessage(err, $localize`No se pudieron subir los adjuntos`));
        this.uploading.set(false);
        picker.value = '';
      },
    });
  }

  removeLabel(attachment: TicketAttachment): string {
    return $localize`Quitar ${attachment.name}:name:`;
  }

  removeAttachment(attachment: TicketAttachment): void {
    this.attachmentsError.set('');
    this.api.delete(`/tickets/${this.ticket().id}/attachments/${attachment.id}`).subscribe({
      next: () => {
        this.attachments.update(current => current.filter(a => a.id !== attachment.id));
        this.removingAttachmentId.set(null);
      },
      error: err => {
        this.attachmentsError.set(errorMessage(err, $localize`No se pudo quitar el adjunto`));
        this.removingAttachmentId.set(null);
      },
    });
  }

  saveClassification(): void {
    const classification = this.classification.trim();
    if (classification === (this.saved.classification ?? '')) return;

    this.save({ classification });
  }

  saveTextFields(): void {
    this.isSaving.set(true);
    this.save(
      { title: this.title, description: this.description, priority: this.priority, status: this.status },
      () => this.isEditing = false);
  }

  changeStatus(newStatus: string): void {
    this.status = newStatus;
    this.save({ status: newStatus });
  }

  /** Guarda las etiquetas como ids del módulo de etiquetas, en su propio PATCH. */
  changeTags(ids: string[]): void {
    this.tagIds.set(ids);
    this.save({ tagIds: ids });
  }

  /**
   * Manda el cambio y, si sale bien, lo da por guardado. Si el servidor lo rechaza, devuelve los
   * campos a lo guardado; el porqué lo cuenta el aviso global de errores. Antes la ficha seguía
   * enseñando como hecho el cambio rechazado.
   */
  private save(change: Partial<Ticket>, onSaved?: () => void): void {
    this.api.patch(`/tickets/${this.saved.id}`, change).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.saved = { ...this.saved, ...change };
        onSaved?.();
        this.updated.emit(this.saved);
      },
      error: () => {
        this.isSaving.set(false);
        this.revertToSaved();
      },
    });
  }

  private revertToSaved(): void {
    const t = this.saved;
    this.title = t.title;
    this.description = t.description ?? '';
    this.status = t.status;
    this.priority = t.priority ?? 'normal';
    this.classification = t.classification ?? '';
    this.tagIds.set(t.tagIds ?? []);
  }

  close(): void { this.closed.emit(); }
}
