import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';

export interface Ticket {
  id: string;
  subject: string;
  company: string;
  contactName: string;
  contactPhone: string;
  contactEmail: string;
  message: string;
  category: string;
  status: string;
  assignedToId?: string;
}

const CATEGORIES = ['Soporte técnico', 'Facturación', 'Consulta general', 'Reclamo', 'Otro'];

@Component({
  selector: 'app-ticket-create-modal',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, LabelComponent],
  templateUrl: './ticket-create-modal.component.html',
})
export class TicketCreateModalComponent {
  readonly created = output<Ticket>();
  readonly closed = output<void>();

  readonly categories = CATEGORIES;

  subject = '';
  company = '';
  contactName = '';
  contactPhone = '';
  contactEmail = '';
  message = '';
  category = CATEGORIES[0];

  loading = signal(false);
  error = signal('');

  private readonly api = inject(ApiService);

  submit(): void {
    if (!this.subject.trim() || !this.contactEmail.trim() || !this.message.trim()) {
      this.error.set('Asunto, email y mensaje son requeridos');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.post<Ticket>('/tickets', {
      subject: this.subject, company: this.company,
      contactName: this.contactName, contactPhone: this.contactPhone,
      contactEmail: this.contactEmail, message: this.message, category: this.category,
    }).subscribe({
      next: ticket => { this.created.emit(ticket); this.closed.emit(); },
      error: () => { this.error.set('Error al crear el ticket'); this.loading.set(false); },
    });
  }

  close(): void { this.closed.emit(); }
}
