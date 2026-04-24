import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';
import { AsyncPipe } from '@angular/common';
import { Store } from '@ngrx/store';
import { selectProjects } from '../../state/projects/projects.state';

export interface TaskItem {
  id: string;
  title: string;
  description: string;
  status: string;
  estimatedHours: number;
  dueDate: string;
  projectId: string;
  assigneeId: string;
}

@Component({
  selector: 'app-task-create-modal',
  standalone: true,
  imports: [FormsModule, ButtonComponent, InputComponent, LabelComponent, AsyncPipe],
  templateUrl: './task-create-modal.component.html',
})
export class TaskCreateModalComponent {
  readonly created = output<TaskItem>();
  readonly closed = output<void>();

  title = '';
  description = '';
  projectId = '';
  assigneeId = '';
  estimatedHours = 1;
  dueDate = '';
  loading = signal(false);
  error = signal('');

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
