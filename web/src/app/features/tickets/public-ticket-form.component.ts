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
  readonly categories = CATEGORIES;

  subject = '';
  company = '';
  contactName = '';
  contactPhone = '';
  contactEmail = '';
  message = '';
  category = CATEGORIES[0];

  loading = signal(false);
  success = signal(false);
  error = signal('');

  submit(): void {
    if (!this.subject || !this.contactEmail || !this.message) {
      this.error.set('Asunto, email y mensaje son requeridos');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.createTicket(this.tenantSlug, {
      subject: this.subject, company: this.company, contactName: this.contactName,
      contactPhone: this.contactPhone, contactEmail: this.contactEmail,
      message: this.message, category: this.category,
    }).subscribe({
      next: () => { this.success.set(true); this.loading.set(false); },
      error: () => { this.error.set('Error al enviar el ticket. Intenta nuevamente.'); this.loading.set(false); },
    });
  }
}
