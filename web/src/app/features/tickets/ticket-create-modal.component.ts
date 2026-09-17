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
  /** «Aplicacion» o «Externo»: si lo abrió alguien con sesión o llegó con una clave de entrada. */
  origen?: string;
  /** Quién lo pidió, cuando llegó desde fuera. Un cliente de la organización no es un usuario. */
  solicitanteNombre?: string | null;
  solicitanteEmail?: string | null;
  solicitanteTelefono?: string | null;
  solicitanteEmpresa?: string | null;
  clasificacion?: string | null;
  teamId?: string | null;
  /** Las claves de las etiquetas separadas por comas, como las guarda el servidor. */
  etiquetas?: string;
}

/** Una imagen o un vídeo adjunto a un ticket. */
export interface AdjuntoDeTicket {
  id: string;
  nombre: string;
  url: string;
  tipoDeContenido: string;
  tamano: number;
  subidoUtc: string;
  /** Si llegó con el ticket desde fuera, desde la web o el backend del cliente. */
  desdeFuera: boolean;
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
      this.error.set($localize`Título y descripción son requeridos`);
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.post<Ticket>('/tickets', {
      title: this.title, description: this.description, priority: this.priority
    }).subscribe({
      next: ticket => { this.created.emit(ticket); this.closed.emit(); },
      error: () => { this.error.set($localize`Error al crear el ticket`); this.loading.set(false); },
    });
  }

  close(): void { this.closed.emit(); }
}
