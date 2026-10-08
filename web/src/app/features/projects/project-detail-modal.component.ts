import { Component, inject, input, output, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import { Store } from '@ngrx/store';
import { projectDeleted, projectUpdated, type Project } from '../../state/projects/projects.state';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideLoader2, lucideTrash2, lucideEdit3, lucideX, lucideLayoutDashboard } from '@ng-icons/lucide';
import { Router } from '@angular/router';

import { DrawerComponent } from '../../shared/ui/drawer.component';
import { TagFieldComponent } from '../../shared/ui/tag-field.component';
import { CommentsComponent } from '../../shared/ui/comments.component';
import { PROJECT_STATUSES } from './project-vocabulary';
import { errorMessage } from '../../shared/utils/error-message';

@Component({
  selector: 'app-project-detail-modal',
  standalone: true,
  imports: [CommentsComponent, FormsModule, NgIconComponent, DrawerComponent, TagFieldComponent],
  viewProviders: [provideIcons({ lucideLoader2, lucideTrash2, lucideEdit3, lucideX, lucideLayoutDashboard })],
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
  /** Las etiquetas del proyecto. Se guardan al marcarlas, como en tareas y tickets. */
  readonly tagIds = signal<string[]>([]);
  readonly tagsError = signal('');
  deleting = signal(false);
  error = signal('');

  readonly statuses = PROJECT_STATUSES;

  private readonly api = inject(ApiService);
  private readonly store = inject(Store);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  /** Al tablero de tareas del proyecto: sus tareas, y sólo las suyas. */
  openBoard(): void {
    this.closed.emit();
    void this.router.navigate(['/tasks'], { queryParams: { projectId: this.project().id, view: 'board' } });
  }

  ngOnInit(): void {
    const p = this.project();
    this.editName = p.name;
    this.editDescription = p.description;
    this.editEstimatedEndDate = p.estimatedEndDate;
    this.editStatus = p.status;
    this.tagIds.set(p.tagIds ?? []);
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
      this.error.set($localize`El nombre es requerido`);
      this.toast.warning($localize`Campo requerido`, 'El nombre del proyecto es obligatorio');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    const changes = {
      name: this.editName,
      description: this.editDescription,
      status: this.editStatus,
      estimatedEndDate: this.editEstimatedEndDate,
    };
    // El PATCH responde 200 sin cuerpo. Antes se usaba esa respuesta como el proyecto guardado:
    // llegaba `null`, el reducer leía `null.id` y la lista de proyectos se rompía al guardar.
    this.api.patch<void>(`/projects/${this.project().id}`, changes).subscribe({
      next: () => {
        const updated: Project = { ...this.project(), ...changes, tagIds: this.tagIds() };
        this.store.dispatch(projectUpdated({ item: updated }));
        this.isEditing.set(false);
        this.loading.set(false);
        this.toast.success($localize`Proyecto actualizado`, `"${updated.name}" se ha actualizado correctamente`);
      },
      error: () => {
        this.error.set($localize`Error al actualizar el proyecto`);
        this.loading.set(false);
        this.toast.error('Error', 'No se pudo actualizar el proyecto');
      },
    });
  }

  changeTags(ids: string[]): void {
    const previous = this.tagIds();
    this.tagIds.set(ids);
    this.tagsError.set('');
    this.api.patch<void>(`/projects/${this.project().id}`, { tagIds: ids }, { silent: true }).subscribe({
      next: () => this.store.dispatch(projectUpdated({ item: { ...this.project(), tagIds: ids } })),
      error: response => {
        // Lo que se ve tiene que ser lo que el servidor aceptó: si rechaza, se vuelve a lo de antes.
        this.tagIds.set(previous);
        this.tagsError.set(errorMessage(response, $localize`No se pudieron guardar las etiquetas`));
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
        this.toast.success($localize`Proyecto eliminado`, 'El proyecto ha sido eliminado correctamente');
        this.closed.emit();
      },
      error: () => {
        this.error.set($localize`Error al eliminar el proyecto`);
        this.deleting.set(false);
        this.toast.error('Error', 'No se pudo eliminar el proyecto');
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
