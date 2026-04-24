import { Component, inject, input, output, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import { Store } from '@ngrx/store';
import { projectDeleted, projectUpdated, type Project } from '../../state/projects/projects.state';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideLoader2, lucideTrash2, lucideEdit3, lucideX } from '@ng-icons/lucide';

@Component({
  selector: 'app-project-detail-modal',
  standalone: true,
  imports: [FormsModule, NgIconComponent],
  viewProviders: [provideIcons({ lucideLoader2, lucideTrash2, lucideEdit3, lucideX })],
  templateUrl: './project-detail-modal.component.html',
})
export class ProjectDetailModalComponent implements OnInit {
  readonly project = input.required<Project>();
  readonly closed = output<void>();

  // Edit mode
  isEditing = signal(false);
  editName = '';
  editDescription = '';
  editEstimatedEndDate = '';
  editStatus = '';
  loading = signal(false);
  deleting = signal(false);
  error = signal('');

  readonly statuses = ['Planned', 'In Progress', 'On Hold', 'Done'];

  private readonly api = inject(ApiService);
  private readonly store = inject(Store);
  private readonly toast = inject(ToastService);

  ngOnInit(): void {
    const p = this.project();
    this.editName = p.name;
    this.editDescription = p.description;
    this.editEstimatedEndDate = p.estimatedEndDate;
    this.editStatus = p.status;
  }

  startEdit(): void {
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    this.isEditing.set(false);
    const p = this.project();
    this.editName = p.name;
    this.editDescription = p.description;
    this.editEstimatedEndDate = p.estimatedEndDate;
    this.editStatus = p.status;
  }

  saveEdit(): void {
    if (!this.editName.trim()) {
      this.error.set('El nombre es requerido');
      this.toast.warning('Campo requerido', 'El nombre del proyecto es obligatorio');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.patch<Project>(`/projects/${this.project().id}`, {
      name: this.editName,
      description: this.editDescription,
      status: this.editStatus,
      estimatedEndDate: this.editEstimatedEndDate,
    }).subscribe({
      next: (updated) => {
        this.store.dispatch(projectUpdated({ item: updated }));
        this.isEditing.set(false);
        this.loading.set(false);
        this.toast.success('Proyecto actualizado', `"${updated.name}" se ha actualizado correctamente`);
      },
      error: () => {
        this.error.set('Error al actualizar el proyecto');
        this.loading.set(false);
        this.toast.error('Error', 'No se pudo actualizar el proyecto');
      },
    });
  }

  deleteProject(): void {
    if (!confirm('¿Estás seguro de eliminar este proyecto?')) return;
    this.deleting.set(true);
    this.error.set('');
    this.api.delete(`/projects/${this.project().id}`).subscribe({
      next: () => {
        this.store.dispatch(projectDeleted({ id: this.project().id }));
        this.deleting.set(false);
        this.toast.success('Proyecto eliminado', 'El proyecto ha sido eliminado correctamente');
        this.closed.emit();
      },
      error: () => {
        this.error.set('Error al eliminar el proyecto');
        this.deleting.set(false);
        this.toast.error('Error', 'No se pudo eliminar el proyecto');
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
