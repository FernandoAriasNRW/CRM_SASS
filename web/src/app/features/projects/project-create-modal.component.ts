import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Store } from '@ngrx/store';
import { projectCreated, type Project } from '../../state/projects/projects.state';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';

@Component({
  selector: 'app-project-create-modal',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, LabelComponent],
  templateUrl: './project-create-modal.component.html',
})
export class ProjectCreateModalComponent {
  readonly closed = output<void>();

  name = '';
  description = '';
  estimatedEndDate = '';
  loading = signal(false);
  error = signal('');

  private readonly api = inject(ApiService);
  private readonly store = inject(Store);

  submit(): void {
    if (!this.name.trim() || !this.estimatedEndDate) {
      this.error.set('Nombre y fecha estimada son requeridos');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.post<Project>('/projects', {
      name: this.name,
      description: this.description,
      estimatedEndDate: this.estimatedEndDate,
    }).subscribe({
      next: item => {
        this.store.dispatch(projectCreated({ item }));
        this.closed.emit();
      },
      error: () => {
        this.error.set('Error al crear el proyecto');
        this.loading.set(false);
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
