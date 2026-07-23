import { Component, inject, OnInit, signal, computed, ViewChild, TemplateRef, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { TaskCreateModalComponent, type TaskItem } from './task-create-modal.component';
import { TaskDetailPanelComponent } from './task-detail-panel.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideRefreshCw, lucidePlus, lucideClock,
  lucideList, lucideLayoutDashboard, lucideFilter, lucideSave
} from '@ng-icons/lucide';
import { DataTableComponent, ColumnDef, TableState } from '../../shared/ui/data-table/data-table.component';
import { AdvancedFiltersComponent, FilterField } from '../../shared/ui/data-table/advanced-filters.component';
import { ViewsService, SavedView } from '../../shared/services/views.service';
import { TableColumnService } from '../../shared/services/table-column.service';
import { HierarchySignalStore } from '../../core/hierarchy-signal.store';

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
    CommonModule, FormsModule, BadgeComponent, ButtonComponent,
    NgIconComponent, DragDropModule, TaskCreateModalComponent, TaskDetailPanelComponent,
    DataTableComponent
  ],
  viewProviders: [provideIcons({
    lucideRefreshCw, lucidePlus, lucideClock,
    lucideList, lucideLayoutDashboard, lucideFilter, lucideSave
  })],
  templateUrl: './tasks.component.html',
})
export class TasksComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly realtime = inject(RealtimeService);
  private readonly viewsService = inject(ViewsService);
  private readonly columnService = inject(TableColumnService);
  private readonly hierarchyStore = inject(HierarchySignalStore);
  private readonly route = inject(ActivatedRoute);

  readonly showModal = signal(false);
  readonly selectedTask = signal<TaskItem | null>(null);
  readonly viewMode = signal<'board' | 'list'>('board');
  readonly isLoading = signal(false);

  // Table State
  tableState = signal<TableState>({
    page: 1,
    pageSize: 25,
    sortDirection: 'asc'
  });
  
  // DataTable columns definition
  tableColumns: ColumnDef[] = this.columnService.buildColumns<TaskItem>({
    title: { label: 'Title' },
    description: { label: 'Description', visible: false },
    status: { label: 'Status', type: 'custom' },
    assigneeId: { label: 'Asignado', type: 'user' },
    estimatedHours: { label: 'Hours', type: 'number' },
    dueDate: { label: 'Due Date', type: 'date' }
  });

  // Advanced Filters definition
  filterFields = computed<FilterField[]>(() => [
    { key: 'projectId', label: 'Project', type: 'select', options: this.projectOptions() },
    { key: 'status', label: 'Status', type: 'select', options: this.statuses.map(s => ({ label: s, value: s })) },
    { key: 'startDate', label: 'Start Date', type: 'date' },
    { key: 'endDate', label: 'End Date', type: 'date' }
  ]);

  // Saved Views
  savedViews = signal<SavedView[]>([]);
  activeViewId = signal<string | null>(null);

  // Data
  readonly allTasks = signal<TaskItem[]>([]);
  totalItems = signal(0);
  
  cols: Column[] = COLUMN_DEFS.map(c => ({ ...c, tasks: [] as TaskItem[] }));
  readonly columnIds = COLUMN_DEFS.map(c => c.key);

  readonly projectOptions = computed(() => {
    const seen = new Set<string>();
    return this.allTasks()
      .filter(t => t.projectId && !seen.has(t.projectId) && (seen.add(t.projectId), true))
      .map(t => ({ label: t.projectId, value: t.projectId }));
  });

  statusBadge(status: string): BadgeVariant { return STATUS_BADGE[status] ?? 'outline'; }

  @ViewChild('statusTemplate', { static: true }) statusTemplate!: TemplateRef<any>;

  constructor() {
    effect(() => {
      const selection = this.hierarchyStore.selectedItem();
      this.tableState.update(s => {
        const newFilters = { ...s.filters };
        delete newFilters['hierarchy_type'];
        delete newFilters['hierarchy_id'];
        if (selection) {
           newFilters['hierarchy_type'] = selection.type;
           newFilters['hierarchy_id'] = selection.id;
        }
        return { ...s, filters: newFilters, page: 1 };
      });
      setTimeout(() => this.loadTasks(), 0);
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    this.tableColumns.find(c => c.key === 'status')!.template = this.statusTemplate;
    this.loadViews();

    this.route.queryParams.subscribe(params => {
      if (params['filter']) {
        this.tableState.update(s => ({ ...s, filters: { ...s.filters, filter: params['filter'] } }));
      } else {
        this.tableState.update(s => {
          const f = { ...s.filters };
          delete f['filter'];
          return { ...s, filters: f };
        });
      }
      this.loadTasks();
    });

    this.realtime.taskMoved$.subscribe(({ taskId, status }) => {
      this.allTasks.update(tasks => tasks.map(t => t.id === taskId ? { ...t, status } : t));
      this.distributeTasksToColumns();
    });
  }

  loadViews(): void {
    this.viewsService.getViews('Tasks').subscribe({
      next: (views) => {
        this.savedViews.set(views);
        const defaultView = views.find(v => v.isDefault);
        if (defaultView) {
          this.applySavedView(defaultView);
        }
      }
    });
  }

  saveCurrentView(name: string, isDefault: boolean = false): void {
    const currentState = { ...this.tableState(), viewType: this.viewMode() };
    const payload = {
      moduleName: 'Tasks',
      viewName: name,
      stateJson: JSON.stringify(currentState),
      isDefault
    };
    this.viewsService.saveView(payload).subscribe({
      next: (view) => {
        this.savedViews.update(views => [...views, view]);
        this.activeViewId.set(view.id);
      }
    });
  }

  getIconForView(view: SavedView): string {
    try {
      const state = JSON.parse(view.stateJson);
      return state.viewType === 'board' ? 'lucideLayoutDashboard' : 'lucideList';
    } catch {
      return 'lucideList';
    }
  }

  createNewView(type: 'list' | 'board'): void {
    const name = prompt('Nombre de la nueva vista:');
    if (!name) return;
    
    this.viewMode.set(type);
    
    const newState = {
      ...this.tableState(),
      viewType: type
    };
    
    const payload = {
      moduleName: 'Tasks',
      viewName: name,
      stateJson: JSON.stringify(newState),
      isDefault: false
    };
    
    this.viewsService.saveView(payload).subscribe({
      next: (view) => {
        this.savedViews.update(views => [...views, view]);
        this.activeViewId.set(view.id);
        this.tableState.set(newState);
      }
    });
  }

  applySavedView(view: SavedView): void {
    this.activeViewId.set(view.id);
    try {
      const state = JSON.parse(view.stateJson) as TableState;
      this.tableState.set(state);
      if (state.viewType === 'board' || state.viewType === 'list') {
        this.viewMode.set(state.viewType);
      }
      this.loadTasks();
    } catch (e) {
      console.error('Failed to parse saved view state', e);
    }
  }

  onTableStateChange(state: TableState): void {
    this.tableState.set(state);
    this.loadTasks();
  }

  onFiltersChange(filters: Record<string, any>): void {
    this.tableState.update(s => ({ ...s, filters, page: 1 }));
    this.loadTasks();
  }

  loadTasks(): void {
    this.isLoading.set(true);
    const state = this.tableState();
    
    let params: any = {
      pageNumber: state.page,
      pageSize: this.viewMode() === 'board' ? 1000 : state.pageSize, // Get all for board view conceptually, or implement lazy loading per column
      sortColumn: state.sortColumn,
      sortDirection: state.sortDirection,
      searchTerm: state.searchTerm
    };

    if (state.filters) {
      if (state.filters['startDate']) params.startDate = state.filters['startDate'];
      if (state.filters['endDate']) params.endDate = state.filters['endDate'];
      if (state.filters['projectId']) params.projectId = state.filters['projectId'];
      if (state.filters['status']) params.status = state.filters['status'];
      if (state.filters['filter']) params.filter = state.filters['filter'];
      if (state.filters['hierarchy_type'] === 'project') params.projectId = state.filters['hierarchy_id'];
    }

    this.api.get<{items: TaskItem[], totalCount: number}>('/tasks', params).subscribe({
      next: res => {
        let tasks = res.items || [];
        
        // Filter out "Done" tasks older than 3 months for Board view (or always)
        const threeMonthsAgo = new Date();
        threeMonthsAgo.setMonth(threeMonthsAgo.getMonth() - 3);
        
        tasks = tasks.filter(t => {
          if (t.status === 'Done') {
            const dateStr = t.dueDate; // Usando dueDate como aproximación de fecha de término
            if (dateStr && new Date(dateStr) < threeMonthsAgo) {
              return false;
            }
          }
          
          // Apply Hierarchy Filter manually for Spaces/Folders
          const type = state.filters?.['hierarchy_type'];
          const id = state.filters?.['hierarchy_id'];
          if (type && id && type !== 'project' && t.projectId) {
            if (type === 'space') {
              const projects = this.hierarchyStore.projectsBySpace()[id] || [];
              if (!projects.some(p => p.id === t.projectId)) return false;
            } else if (type === 'folder') {
              const projects = this.hierarchyStore.projectsByFolder()[id] || [];
              if (!projects.some(p => p.id === t.projectId)) return false;
            }
          }
          
          return true;
        });

        this.totalItems.set(res.totalCount || 0);
        this.allTasks.set(tasks);
        this.distributeTasksToColumns();
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  private distributeTasksToColumns() {
    const tasks = this.allTasks();
    this.cols = COLUMN_DEFS.map(c => ({
      ...c,
      tasks: tasks.filter(t => t.status === c.key)
    }));
  }

  openDetail(task: TaskItem): void {
    this.selectedTask.set(task);
  }

  onTaskUpdated(updated: TaskItem): void {
    this.allTasks.update(tasks => tasks.map(t => t.id === updated.id ? updated : t));
    this.distributeTasksToColumns();
  }

  onTaskCreated(task: TaskItem): void {
    this.allTasks.update(tasks => [...tasks, task]);
    this.distributeTasksToColumns();
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
