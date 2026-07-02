import { Component, inject, OnInit, signal, computed, ViewChild, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { ReportCreateModalComponent } from './report-create-modal.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideRefreshCw, lucidePlus, lucideDownload, lucideFileText, lucideFilter, lucideSave } from '@ng-icons/lucide';
import { DataTableComponent, ColumnDef, TableState } from '../../shared/ui/data-table/data-table.component';
import { AdvancedFiltersComponent, FilterField } from '../../shared/ui/data-table/advanced-filters.component';
import { ViewsService, SavedView } from '../../shared/services/views.service';
import { TableColumnService } from '../../shared/services/table-column.service';

interface ReportDto {
  id: string;
  name: string;
  type: string;
  format: string;
  parameters: string;
}

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, BadgeComponent, ButtonComponent, NgIconComponent, ReportCreateModalComponent, DataTableComponent, AdvancedFiltersComponent],
  viewProviders: [provideIcons({ lucideRefreshCw, lucidePlus, lucideDownload, lucideFileText, lucideFilter, lucideSave })],
  templateUrl: './reports.component.html',
})
export class ReportsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly viewsService = inject(ViewsService);
  private readonly columnService = inject(TableColumnService);

  readonly reports = signal<ReportDto[]>([]);
  readonly loading = signal(false);
  readonly showCreateModal = signal(false);
  totalItems = signal(0);

  // Table State
  tableState = signal<TableState>({
    page: 1,
    pageSize: 25,
    sortDirection: 'asc'
  });

  tableColumns: ColumnDef[] = this.columnService.buildColumns<ReportDto>({
    name: { label: 'Nombre' },
    type: { label: 'Tipo' },
    format: { label: 'Formato', type: 'custom' },
    parameters: { label: 'Parámetros', sortable: false }
  }, [
    { key: 'actions', label: 'Acciones', sortable: false, type: 'custom' }
  ]);

  filterFields = computed<FilterField[]>(() => [
    { key: 'type', label: 'Type', type: 'select', options: ['Tasks', 'Tickets', 'Sales', 'Activity'].map(s => ({ label: s, value: s })) },
    { key: 'format', label: 'Format', type: 'select', options: ['PDF', 'Excel', 'CSV'].map(s => ({ label: s, value: s })) },
  ]);

  savedViews = signal<SavedView[]>([]);
  activeViewId = signal<string | null>(null);

  @ViewChild('formatTemplate', { static: true }) formatTemplate!: TemplateRef<any>;
  @ViewChild('actionsTemplate', { static: true }) actionsTemplate!: TemplateRef<any>;

  ngOnInit(): void {
    this.tableColumns.find(c => c.key === 'format')!.template = this.formatTemplate;
    this.tableColumns.find(c => c.key === 'actions')!.template = this.actionsTemplate;
    this.loadViews();
    this.load();
  }

  loadViews(): void {
    this.viewsService.getViews('Reports').subscribe({
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
      moduleName: 'Reports',
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
      this.load();
    } catch (e) {
      console.error('Failed to parse saved view state', e);
    }
  }

  onTableStateChange(state: TableState): void {
    this.tableState.set(state);
    this.load();
  }

  onFiltersChange(filters: Record<string, any>): void {
    this.tableState.update(s => ({ ...s, filters, page: 1 }));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const state = this.tableState();
    
    let params: any = {
      pageNumber: state.page,
      pageSize: state.pageSize,
      sortColumn: state.sortColumn,
      sortDirection: state.sortDirection,
      searchTerm: state.searchTerm
    };

    if (state.filters) {
      if (state.filters['type']) params.type = state.filters['type'];
      if (state.filters['format']) params.format = state.filters['format'];
    }
    
    this.api.get<{items: ReportDto[], totalCount: number}>('/reports', params).subscribe({
      next: res => {
        this.reports.set(res.items || []);
        if (res.totalCount !== undefined) {
          this.totalItems.set(res.totalCount);
        } else {
          this.totalItems.set((res.items || []).length);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openCreateModal(): void { this.showCreateModal.set(true); }
  closeCreateModal(): void { this.showCreateModal.set(false); }
  onReportCreated(): void { this.load(); }

  generateReport(id: string, format: string): void {
    this.api.post(`/reports/${id}/generate`, {}).subscribe({
      next: () => alert(`Reporte en formato ${format} generado.`),
      error: () => alert('Error al generar el reporte.'),
    });
  }

  getFormatBadge(format: string): BadgeVariant {
    switch (format) {
      case 'PDF': return 'destructive';
      case 'Excel': return 'success';
      case 'CSV': return 'outline';
      default: return 'default';
    }
  }
}
