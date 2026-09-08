import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { PRIORIDADES_DE_TICKET } from './vocabulario-de-tickets';

export interface Ticket {
  id: string;
  title: string;
  description: string;
  priority: string;
  status: string;
  assignedAgentId?: string;
  customerId?: string;
  createdAt: string;
}

/**
 * Las prioridades salen del vocabulario compartido.
 *
 * Aquí estaba escrito `['Low', 'Medium', 'High', 'Urgent']`, y **«Urgent» no existe** en el
 * servidor —se llama «Critical»—. El comando lo recibía, no casaba con ningún valor y el ticket
 * se creaba con la prioridad por defecto sin avisar de nada.
 */
const PRIORITIES = PRIORIDADES_DE_TICKET;

@Component({
  selector: 'app-ticket-create-modal',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, LabelComponent, DrawerComponent],
  templateUrl: './ticket-create-modal.component.html',
})
export class TicketCreateModalComponent {
  readonly created = output<Ticket>();
  readonly closed = output<void>();

  readonly priorities = PRIORITIES;

  title = '';
  description = '';
  priority = 'Medium';

  loading = signal(false);
  error = signal('');

  private readonly api = inject(ApiService);

  submit(): void {
    if (!this.title.trim() || !this.description.trim()) {
      this.error.set('Título y descripción son requeridos');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.post<Ticket>('/tickets', {
      title: this.title, description: this.description, priority: this.priority
    }).subscribe({
      next: ticket => { this.created.emit(ticket); this.closed.emit(); },
      error: () => { this.error.set('Error al crear el ticket'); this.loading.set(false); },
    });
  }

  close(): void { this.closed.emit(); }
}
