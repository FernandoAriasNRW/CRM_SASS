import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GuestApiService } from '../../core/guest-api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';
import {
  CardComponent, CardHeaderComponent, CardTitleComponent,
  CardDescriptionComponent, CardContentComponent, CardFooterComponent
} from '../../shared/ui/card.component';

const CATEGORIES = ['Soporte técnico', 'Facturación', 'Consulta general', 'Reclamo', 'Otro'];

@Component({
  selector: 'app-public-ticket-form',
  standalone: true,
  imports: [
    FormsModule, ButtonComponent, InputComponent, LabelComponent,
    CardComponent, CardHeaderComponent, CardTitleComponent,
    CardDescriptionComponent, CardContentComponent, CardFooterComponent
  ],
  templateUrl: './public-ticket-form.component.html',
})
export class PublicTicketFormComponent {
  private readonly api = inject(GuestApiService);

  readonly tenantSlug = 'acme';
  readonly priorities = ['Low', 'Medium', 'High', 'Urgent'];

  title = '';
  description = '';
  priority = 'Medium';

  loading = signal(false);
  success = signal(false);
  error = signal('');

  submit(): void {
    if (!this.title || !this.description) {
      this.error.set('Título y descripción son requeridos');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.createTicket(this.tenantSlug, {
      title: this.title, description: this.description, priority: this.priority
    }).subscribe({
      next: () => { this.success.set(true); this.loading.set(false); },
      error: () => { this.error.set('Error al enviar el ticket. Intenta nuevamente.'); this.loading.set(false); },
    });
  }
}
