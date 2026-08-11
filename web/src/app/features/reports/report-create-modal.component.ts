import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';

@Component({
  selector: 'app-report-create-modal',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, LabelComponent],
  templateUrl: './report-create-modal.component.html',
})
export class ReportCreateModalComponent {
  readonly closed = output<void>();
  readonly created = output<void>();

  name = '';
  type = 'ProjectProgress';
  format = 'PDF';
  parameters = '';
  loading = signal(false);
  error = signal('');

  private readonly api = inject(ApiService);

  submit(): void {
    if (!this.name.trim() || !this.type || !this.format) {
      this.error.set('Nombre, Tipo y Formato son requeridos');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    
    this.api.post('/reports', {
      name: this.name,
      type: this.type,
      format: this.format,
      parameters: this.parameters ? this.parameters : null,
    }).subscribe({
      next: () => {
        this.created.emit();
        this.closed.emit();
      },
      error: (err) => {
        this.error.set(err?.error?.message || 'Error al solicitar el reporte');
        this.loading.set(false);
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
