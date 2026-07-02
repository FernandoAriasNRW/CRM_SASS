import { Component, inject, OnInit, signal, computed, ViewChild, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ApiService } from '../../core/api.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { TicketCreateModalComponent, type Ticket } from './ticket-create-modal.component';
import { TicketDetailPanelComponent } from './ticket-detail-panel.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideRefreshCw, lucidePlus, lucideList,
  lucideLayoutDashboard, lucideFilter, lucideUser, lucideSave
} from '@ng-icons/lucide';
import { DataTableComponent, ColumnDef, TableState } from '../../shared/ui/data-table/data-table.component';
import { AdvancedFiltersComponent, FilterField } from '../../shared/ui/data-table/advanced-filters.component';
import { ViewsService, SavedView } from '../../shared/services/views.service';
import { TableColumnService } from '../../shared/services/table-column.service';

interface Column { key: string; label: string; badge: BadgeVariant; tickets: Ticket[]; }

const COLUMN_DEFS: Omit<Column, 'tickets'>[] = [
  { key: 'Open',         label: 'Abierto',      badge: 'secondary' },
  { key: 'InProgress',   label: 'En progreso',  badge: 'default'   },
  { key: 'Resolved',     label: 'Resuelto',     badge: 'success'   },
  { key: 'Closed',       label: 'Cerrado',      badge: 'outline'   },
];

const STATUS_BADGE: Record<string, BadgeVariant> = {
  'Open': 'secondary', 'InProgress': 'default', 'Resolved': 'success', 'Closed': 'outline'
};

@Component({
  selector: 'app-tickets',
  standalone: true,
  imports: [
    CommonModule, FormsModule, BadgeComponent, ButtonComponent,
    NgIconComponent, DragDropModule, TicketCreateModalComponent, TicketDetailPanelComponent,
    DataTableComponent, AdvancedFiltersComponent
  ],
  viewProviders: [provideIcons({
    lucideRefreshCw, lucidePlus, lucideList,
    lucideLayoutDashboard, lucideFilter, lucideUser, lucideSave
  })],
  templateUrl: './tickets.component.html',
})
export class TicketsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly viewsService = inject(ViewsService);
  private readonly columnService = inject(TableColumnService);

  readonly showModal = signal(false);
  readonly selectedTicket = signal<Ticket | null>(null);
  readonly viewMode = signal<'board' | 'list'>('board');
  readonly isLoading = signal(false);

  // Table State
  tableState = signal<TableState>({
    page: 1,
    pageSize: 25,
    sortDirection: 'asc'
  });

  // DataTable columns definition
  tableColumns: ColumnDef[] = this.columnService.buildColumns<Ticket>({
    title: { label: 'Title' },
    description: { label: 'Description', visible: false },
    status: { label: 'Status', type: 'custom' },
    priority: { label: 'Priority', type: 'custom' },
    assignedAgentId: { label: 'Agente', type: 'user' },
    createdAt: { label: 'Created At', type: 'date' }
  });

  // Advanced Filters definition
  filterFields = computed<FilterField[]>(() => [
    { key: 'priority', label: 'Priority', type: 'select', options: this.priorities().map(p => ({ label: p, value: p })) },
    { key: 'status', label: 'Status', type: 'select', options: this.statuses.map(s => ({ label: s, value: s })) },
    { key: 'startDate', label: 'Start Date', type: 'date' },
    { key: 'endDate', label: 'End Date', type: 'date' }
  ]);

  // Saved Views
  savedViews = signal<SavedView[]>([]);
  activeViewId = signal<string | null>(null);

  // Data
  readonly allTickets = signal<Ticket[]>([]);
  totalItems = signal(0);
  
  cols: Column[] = COLUMN_DEFS.map(c => ({ ...c, tickets: [] as Ticket[] }));
  readonly columnIds = COLUMN_DEFS.map(c => c.key);

  readonly priorities = computed(() =>
    [...new Set(this.allTickets().map(t => t.priority).filter(Boolean))]
  );

  readonly statuses = ['Open', 'InProgress', 'Resolved', 'Closed'];

  statusBadge(status: string): BadgeVariant { return STATUS_BADGE[status] ?? 'outline'; }

  @ViewChild('statusTemplate', { static: true }) statusTemplate!: TemplateRef<any>;
  @ViewChild('priorityTemplate', { static: true }) priorityTemplate!: TemplateRef<any>;

  ngOnInit(): void {
    this.tableColumns.find(c => c.key === 'status')!.template = this.statusTemplate;
    this.tableColumns.find(c => c.key === 'priority')!.template = this.priorityTemplate;
    this.loadViews();
    this.loadTickets();
  }

  loadViews(): void {
    this.viewsService.getViews('Tickets').subscribe({
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
      moduleName: 'Tickets',
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
      this.loadTickets();
    } catch (e) {
      console.error('Failed to parse saved view state', e);
    }
  }

  onTableStateChange(state: TableState): void {
    this.tableState.set(state);
    this.loadTickets();
  }

  onFiltersChange(filters: Record<string, any>): void {
    this.tableState.update(s => ({ ...s, filters, page: 1 }));
    this.loadTickets();
  }

  loadTickets(): void {
    this.isLoading.set(true);
    const state = this.tableState();
    
    let params: any = {
      pageNumber: state.page,
      pageSize: this.viewMode() === 'board' ? 1000 : state.pageSize, // Get all for board view
      sortColumn: state.sortColumn,
      sortDirection: state.sortDirection,
      searchTerm: state.searchTerm
    };

    if (state.filters) {
      if (state.filters['startDate']) params.startDate = state.filters['startDate'];
      if (state.filters['endDate']) params.endDate = state.filters['endDate'];
      if (state.filters['priority']) params.priority = state.filters['priority'];
      if (state.filters['status']) params.status = state.filters['status'];
    }

    this.api.get<{items: Ticket[], totalCount: number}>('/tickets', params).subscribe({
      next: res => {
        const tickets = res.items || [];
        this.totalItems.set(res.totalCount || 0);
        this.allTickets.set(tickets);
        this.distributeTicketsToColumns();
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  private distributeTicketsToColumns() {
    const tickets = this.allTickets();
    this.cols = COLUMN_DEFS.map(c => ({
      ...c,
      tickets: tickets.filter(t => t.status === c.key)
    }));
  }

  openDetail(ticket: Ticket): void {
    this.selectedTicket.set(ticket);
  }

  onTicketUpdated(updated: Ticket): void {
    this.allTickets.update(tickets => tickets.map(t => t.id === updated.id ? updated : t));
    this.distributeTicketsToColumns();
  }

  onTicketCreated(ticket: Ticket): void {
    this.allTickets.update(tickets => [ticket, ...tickets]);
    this.distributeTicketsToColumns();
  }

  drop(event: CdkDragDrop<Ticket[]>, targetKey: string): void {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      const ticket = event.previousContainer.data[event.previousIndex];
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex
      );
      this.allTickets.update(tickets =>
        tickets.map(t => t.id === ticket.id ? { ...t, status: targetKey } : t)
      );
      this.api.patch(`/tickets/${ticket.id}`, { status: targetKey }).subscribe();
    }
  }
}
