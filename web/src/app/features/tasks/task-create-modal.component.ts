import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';
import { AsyncPipe } from '@angular/common';
import { Store } from '@ngrx/store';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import { selectProjects } from '../../state/projects/projects.state';

export interface TaskItem {
  id: string;
  title: string;
  description: string;
  status: string;
  /** Uno de los valores de PRIORIDADES. El backend responde siempre con uno válido. */
  priority: string;
  estimatedHours: number;
  dueDate: string;
  projectId: string;
  assigneeId: string;
  /** Tarea de la que ésta es subtarea, si lo es. */
  parentTaskId?: string | null;
  /** Progreso agregado que calcula el servidor; no se guarda en la tarea. */
  subtaskCount?: number;
  completedSubtaskCount?: number;
  /** Cuántas tareas bloquean a ésta y a cuántas bloquea ella. También los cuenta el servidor. */
  blockedByCount?: number;
  blocksCount?: number;
  /** Todas las personas responsables, la principal primero. `assigneeId` es la principal. */
  assignees?: string[];
  /** Progreso de la checklist; los puntos se piden aparte. */
  checklistTotal?: number;
  checklistDone?: number;
}

/** Un punto de la checklist. Llega ordenado por posición desde la API. */
export interface ChecklistItem {
  id: string;
  texto: string;
  hecho: boolean;
  posicion: number;
}

/** Una tarea vista desde el panel de dependencias. */
export interface TaskDependencyRef {
  id: string;
  title: string;
  status: string;
  priority: string;
}

export interface TaskDependencies {
  bloqueadaPor: TaskDependencyRef[];
  bloqueaA: TaskDependencyRef[];
}

/**
 * Prioridades, en el orden de negocio que fija el backend (TaskPriority).
 *
 * Las claves son exactamente los valores que acepta la API: mandar 'urgent' en minúscula
 * la rechazaría, así que no se transforman en ningún punto.
 */
export const PRIORIDADES = [
  { key: 'Urgent', label: $localize`Urgente`, icon: 'lucideAlertCircle', color: 'text-destructive' },
  { key: 'High',   label: $localize`Alta`,    icon: 'lucideArrowUp',     color: 'text-warning' },
  { key: 'Normal', label: $localize`Normal`,  icon: 'lucideMinus',       color: 'text-primary' },
  { key: 'Low',    label: $localize`Baja`,    icon: 'lucideArrowDown',   color: 'text-muted-foreground' },
] as const;

export const PRIORIDAD_POR_DEFECTO = 'Normal';

@Component({
  selector: 'app-task-create-modal',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, LabelComponent, AsyncPipe, DrawerComponent],
  templateUrl: './task-create-modal.component.html',
})
export class TaskCreateModalComponent {
  readonly created = output<TaskItem>();
  readonly closed = output<void>();

  title = '';
  description = '';
  projectId = '';
  assigneeId = '';
  priority: string = PRIORIDAD_POR_DEFECTO;
  estimatedHours = 1;
  dueDate = '';
  loading = signal(false);
  error = signal('');

  readonly prioridades = PRIORIDADES;

  private readonly api = inject(ApiService);
  readonly projects$ = inject(Store).select(selectProjects);

  submit(): void {
    if (!this.title.trim() || !this.projectId || !this.dueDate) {
      this.error.set('Título, proyecto y fecha son requeridos');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.api.post<TaskItem>('/tasks', {
      title: this.title,
      description: this.description,
      projectId: this.projectId,
      assigneeId: this.assigneeId || '00000000-0000-0000-0000-000000000000',
      priority: this.priority,
      estimatedHours: this.estimatedHours,
      dueDate: this.dueDate,
    }).subscribe({
      next: item => {
        this.created.emit(item);
        this.closed.emit();
      },
      error: () => {
        this.error.set('Error al crear la tarea');
        this.loading.set(false);
      },
    });
  }

  close(): void { this.closed.emit(); }
}
