import { Component, inject, OnInit, signal, computed, ViewChild, TemplateRef } from '@angular/core';
import { AsyncPipe, CommonModule } from '@angular/common';
import { Store } from '@ngrx/store';
import { ApiService } from '../../core/api.service';
import { ProjectCreateModalComponent } from './project-create-modal.component';
import { ProjectDetailModalComponent } from './project-detail-modal.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideRefreshCw, lucidePlus, lucideEye, lucideFilter, lucideSave } from '@ng-icons/lucide';
import {
  projectsLoaded, selectProjects, selectProjectsLoaded, type Project
} from '../../state/projects/projects.state';
import { DataTableComponent, ColumnDef, TableState } from '../../shared/ui/data-table/data-table.component';
import { AdvancedFiltersComponent, FilterField } from '../../shared/ui/data-table/advanced-filters.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { ViewsService, SavedView } from '../../shared/services/views.service';
import { TableColumnService } from '../../shared/services/table-column.service';

const STATUS_VARIANT: Record<string, string> = {
  'Planned':     'bg-gray-100 text-gray-800',
  'In Progress': 'bg-blue-100 text-blue-800',
  'Done':        'bg-green-100 text-green-800',
  'On Hold':     'bg-yellow-100 text-yellow-800',
};

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [CommonModule, NgIconComponent, ProjectCreateModalComponent, ProjectDetailModalComponent, AsyncPipe, DataTableComponent, AdvancedFiltersComponent, ButtonComponent],
  viewProviders: [provideIcons({ lucideRefreshCw, lucidePlus, lucideEye, lucideFilter, lucideSave })],
  templateUrl: './projects.component.html',
})
export class ProjectsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly store = inject(Store);
  private readonly viewsService = inject(ViewsService);
  private readonly columnService = inject(TableColumnService);

  readonly projects$ = this.store.select(selectProjects);
  readonly loaded$ = this.store.select(selectProjectsLoaded);
  readonly projects = signal<Project[]>([]);
  totalItems = signal(0);
  
  readonly isLoading = signal(false);
  readonly showCreateModal = signal(false);
  readonly selectedProject = signal<Project | null>(null);

  // Table State
  tableState = signal<TableState>({
    page: 1,
    pageSize: 25,
    sortDirection: 'asc'
  });

  tableColumns: ColumnDef[] = this.columnService.buildColumns<Project>({
    name: { label: 'Nombre' },
    description: { label: 'Descripción', sortable: false, visible: false },
    status: { label: 'Estado', type: 'custom' },
    ownerId: { label: 'Propietario', type: 'user' },
    startDate: { label: 'Inicio', type: 'date' },
    estimatedEndDate: { label: 'Fin Estimado', type: 'date' }
  }, [
    { key: 'actions', label: 'Acciones', sortable: false, type: 'custom' }
  ]);

  filterFields = computed<FilterField[]>(() => [
    { key: 'status', label: 'Status', type: 'select', options: ['Planned', 'In Progress', 'Done', 'On Hold'].map(s => ({ label: s, value: s })) },
    { key: 'startDate', label: 'Start Date', type: 'date' },
    { key: 'endDate', label: 'End Date', type: 'date' }
  ]);

  savedViews = signal<SavedView[]>([]);
  activeViewId = signal<string | null>(null);

  @ViewChild('statusTemplate', { static: true }) statusTemplate!: TemplateRef<any>;
  @ViewChild('actionsTemplate', { static: true }) actionsTemplate!: TemplateRef<any>;

  statusStyle(status: string): string {
    return STATUS_VARIANT[status] ?? 'bg-gray-100 text-gray-800';
  }

  ngOnInit(): void { 
    this.tableColumns.find(c => c.key === 'status')!.template = this.statusTemplate;
    this.tableColumns.find(c => c.key === 'actions')!.template = this.actionsTemplate;

    this.projects$.subscribe(projects => {
      this.projects.set(projects);
      // Wait we don't have total items from ngRx store if it only stores items. Let's just use array length for now.
      this.totalItems.set(projects.length);
    });
    this.loadViews();
    this.loadProjects(); 
  }

  loadViews(): void {
    this.viewsService.getViews('Projects').subscribe({
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
    const payload = {
      moduleName: 'Projects',
      viewName: name,
      stateJson: JSON.stringify(this.tableState()),
      isDefault
    };
    this.viewsService.saveView(payload).subscribe({
      next: (view) => {
        this.savedViews.update(views => [...views, view]);
        this.activeViewId.set(view.id);
      }
    });
  }

  applySavedView(view: SavedView): void {
    this.activeViewId.set(view.id);
    try {
      const state = JSON.parse(view.stateJson) as TableState;
      this.tableState.set(state);
      this.loadProjects();
    } catch (e) {
      console.error('Failed to parse saved view state', e);
    }
  }

  onTableStateChange(state: TableState): void {
    this.tableState.set(state);
    this.loadProjects();
  }

  onFiltersChange(filters: Record<string, any>): void {
    this.tableState.update(s => ({ ...s, filters, page: 1 }));
    this.loadProjects();
  }

  loadProjects(): void {
    this.isLoading.set(true);
    const state = this.tableState();
    
    let params: any = {
      pageNumber: state.page,
      pageSize: state.pageSize,
      sortColumn: state.sortColumn,
      sortDirection: state.sortDirection,
      searchTerm: state.searchTerm
    };

    if (state.filters) {
      if (state.filters['startDate']) params.startDate = state.filters['startDate'];
      if (state.filters['endDate']) params.endDate = state.filters['endDate'];
      if (state.filters['status']) params.status = state.filters['status'];
    }

    this.api.get<{items: Project[], totalCount: number}>('/projects', params).subscribe({
      next: res => {
        this.store.dispatch(projectsLoaded({ items: res.items || [] }));
        if (res.totalCount !== undefined) {
           this.totalItems.set(res.totalCount);
        }
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  openCreateModal(): void {
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  openDetailModal(project: Project): void {
    this.selectedProject.set(project);
  }

  closeDetailModal(): void {
    this.selectedProject.set(null);
  }
}
