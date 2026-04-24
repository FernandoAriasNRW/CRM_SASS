import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { TaskCreateModalComponent, type TaskItem } from './task-create-modal.component';
import { TaskDetailPanelComponent } from './task-detail-panel.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideRefreshCw, lucidePlus, lucideClock,
  lucideList, lucideLayoutDashboard, lucideFilter
} from '@ng-icons/lucide';

export interface Column { key: string; label: string; badge: BadgeVariant; tasks: TaskItem[]; }

const COLUMN_DEFS: Omit<Column, 'tasks'>[] = [
  { key: 'To Do',       label: 'Por hacer',  badge: 'secondary' },
  { key: 'In Progress', label: 'En progreso', badge: 'default'   },
  { key: 'In Review',   label: 'En revisión', badge: 'warning'   },
  { key: 'Done',        label: 'Completado',  badge: 'success'   },
];

const STATUS_BADGE: Record<string, BadgeVariant> = {
  'To Do': 'secondary', 'In Progress': 'default', 'In Review': 'warning', 'Done': 'success'
};

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [
    FormsModule, BadgeComponent, ButtonComponent,
    NgIconComponent, DragDropModule, TaskCreateModalComponent, TaskDetailPanelComponent
  ],
  viewProviders: [provideIcons({
    lucideRefreshCw, lucidePlus, lucideClock,
    lucideList, lucideLayoutDashboard, lucideFilter
  })],
  templateUrl: './tasks.component.html',
})
export class TasksComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly realtime = inject(RealtimeService);

  readonly showModal = signal(false);
  readonly selectedTask = signal<TaskItem | null>(null);
  readonly viewMode = signal<'board' | 'list'>('board');
  readonly isLoading = signal(false);

  // Filtros - usando signals para reactivity
  filterProject = signal('');
  filterStatus = signal('');
  filterSearch = signal('');

  // Arrays mutables por columna — necesarios para CDK drag-drop
  readonly columnTasks: Record<string, TaskItem[]> = {
    'To Do': [], 'In Progress': [], 'In Review': [], 'Done': []
  };

  readonly columns: Column[] = COLUMN_DEFS.map(c => ({
    ...c, get tasks() { return [] as TaskItem[]; }
  }));

  // Columnas con referencia a los arrays mutables
  readonly boardColumns: Column[] = COLUMN_DEFS.map(c => ({
    ...c,
    get tasks(): TaskItem[] { return [] as TaskItem[]; }
  }));

  // Usamos un array simple de columnas con tasks como propiedad directa
  cols: Column[] = COLUMN_DEFS.map(c => ({ ...c, tasks: [] as TaskItem[] }));

  readonly columnIds = COLUMN_DEFS.map(c => c.key);

  // Lista plana para vista lista
  readonly allTasks = signal<TaskItem[]>([]);

  readonly filteredList = computed(() => {
    let tasks = this.allTasks();
    if (this.filterProject()) tasks = tasks.filter(t => t.projectId === this.filterProject());
    if (this.filterStatus()) tasks = tasks.filter(t => t.status === this.filterStatus());
    if (this.filterSearch()) {
      const q = this.filterSearch().toLowerCase();
      tasks = tasks.filter(t =>
        t.title.toLowerCase().includes(q) || t.description?.toLowerCase().includes(q)
      );
    }
    return tasks;
  });

  readonly totalVisible = computed(() => this.filteredList().length);

  // Computed para opciones de proyectos en el filtro
  readonly projectOptions = computed(() => {
    const seen = new Set<string>();
    return this.allTasks()
      .filter(t => t.projectId && !seen.has(t.projectId) && (seen.add(t.projectId), true))
      .map(t => ({ id: t.projectId, name: t.projectId }));
  });

  statusBadge(status: string): BadgeVariant { return STATUS_BADGE[status] ?? 'outline'; }

  ngOnInit(): void {
    this.load();
    this.realtime.taskMoved$.subscribe(({ taskId, status }) => {
      // Mover localmente en el board
      for (const col of this.cols) {
        const idx = col.tasks.findIndex(t => t.id === taskId);
        if (idx !== -1) {
          const [task] = col.tasks.splice(idx, 1);
          const target = this.cols.find(c => c.key === status);
          if (target) target.tasks.push({ ...task, status });
          break;
        }
      }
      // Actualizar lista plana
      this.allTasks.update(tasks => tasks.map(t => t.id === taskId ? { ...t, status } : t));
    });
  }

  load(): void {
    this.isLoading.set(true);
    this.api.get<TaskItem[]>('/tasks').subscribe({
      next: tasks => {
        // Resetear columnas
        this.cols = COLUMN_DEFS.map(c => ({ ...c, tasks: [] as TaskItem[] }));
        tasks.forEach(t => {
          const col = this.cols.find(c => c.key === t.status) ?? this.cols[0];
          col.tasks.push(t);
        });
        this.allTasks.set(tasks);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  applyFilters(): void {
    const filtered = this.allTasks().filter(t => {
      if (this.filterProject() && t.projectId !== this.filterProject()) return false;
      if (this.filterStatus() && t.status !== this.filterStatus()) return false;
      if (this.filterSearch()) {
        const q = this.filterSearch().toLowerCase();
        if (!t.title.toLowerCase().includes(q) && !t.description?.toLowerCase().includes(q)) return false;
      }
      return true;
    });
    this.cols = COLUMN_DEFS.map(c => ({
      ...c, tasks: filtered.filter(t => t.status === c.key)
    }));
  }

  clearFilters(): void {
    this.filterProject.set('');
    this.filterStatus.set('');
    this.filterSearch.set('');
    this.load();
  }

  openDetail(task: TaskItem): void {
    this.selectedTask.set(task);
  }

  onTaskUpdated(updated: TaskItem): void {
    this.allTasks.update(tasks => tasks.map(t => t.id === updated.id ? updated : t));
    this.cols = this.cols.map(c => ({
      ...c, tasks: c.tasks.map(t => t.id === updated.id ? updated : t)
    }));
  }

  onTaskCreated(task: TaskItem): void {
    const col = this.cols.find(c => c.key === 'To Do');
    if (col) col.tasks.push(task);
    this.allTasks.update(tasks => [...tasks, task]);
  }

  drop(event: CdkDragDrop<TaskItem[]>, targetKey: string): void {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      const task = event.previousContainer.data[event.previousIndex];
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex
      );
      this.allTasks.update(tasks =>
        tasks.map(t => t.id === task.id ? { ...t, status: targetKey } : t)
      );
      this.api.post(`/tasks/${task.id}/move`, { newStatus: targetKey }).subscribe();
    }
  }

  readonly statuses = ['To Do', 'In Progress', 'In Review', 'Done'];
}
