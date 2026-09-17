import { Component, OnInit, TemplateRef, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { AuthSignalStore } from '../../core/auth-signal.store';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { TicketCreateModalComponent, type Ticket } from './ticket-create-modal.component';
import { TicketDetailPanelComponent } from './ticket-detail-panel.component';
import { ESTADOS_DE_TICKET, insigniaDelEstado, nombreDeLaPrioridad, nombreDelEstado } from './vocabulario-de-tickets';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideRefreshCw, lucidePlus, lucideList,
  lucideLayoutDashboard, lucideFilter, lucideUser, lucideSave
} from '@ng-icons/lucide';
import { DataTableComponent, ColumnDef, TableState } from '../../shared/ui/data-table/data-table.component';
import { FilterField } from '../../shared/ui/data-table/advanced-filters.component';
import { ViewsService, SavedView } from '../../shared/services/views.service';
import { TableColumnService } from '../../shared/services/table-column.service';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { ToastService } from '../../shared/services/toast.service';
import { EmptyInlineComponent } from '../../shared/ui/empty-state.component';
import { BarraDeVistasComponent, type VistaIntegrada } from '../../shared/ui/barra-de-vistas/barra-de-vistas.component';

interface Column {
  key: string;
  label: string;
  badge: BadgeVariant;
  /** Lo que se pinta y sobre lo que opera el arrastre. */
  tickets: Ticket[];
  /** El resto, aún sin pintar. Se revela por tandas con «mostrar más». */
  pendientes: Ticket[];
}

/** Tarjetas por columna antes de pedir más. Ver el mismo razonamiento en tasks. */
const POR_TANDA = 25;

/**
 * Las columnas del tablero, sacadas del vocabulario compartido.
 *
 * Estaban escritas aquí a mano y **faltaba `PendingInfo`**: un ticket esperando información no
 * caía en ninguna columna, así que desaparecía del tablero sin estar borrado ni archivado. Ahora
 * salen de la misma lista que usan el cajón de detalle y el alta.
 */
const COLUMN_DEFS: Omit<Column, 'tickets' | 'pendientes'>[] = ESTADOS_DE_TICKET.map(e => ({
  key: e.clave,
  label: e.etiqueta,
  badge: e.badge
}));



@Component({
  selector: 'app-tickets',
  standalone: true,
  imports: [ClickableDirective, 
    CommonModule, FormsModule, BadgeComponent, ButtonComponent,
    NgIconComponent, DragDropModule, TicketCreateModalComponent, TicketDetailPanelComponent,
    DataTableComponent, EmptyInlineComponent, BarraDeVistasComponent
  ],
  viewProviders: [provideIcons({
    lucideRefreshCw, lucidePlus, lucideList,
    lucideLayoutDashboard, lucideFilter, lucideUser, lucideSave
  })],
  templateUrl: './tickets.component.html',
})
export class TicketsComponent implements OnInit {

  private readonly toast = inject(ToastService);
  private readonly api = inject(ApiService);
  private readonly realtime = inject(RealtimeService);
  private readonly authStore = inject(AuthSignalStore);
  private readonly viewsService = inject(ViewsService);
  private readonly columnService = inject(TableColumnService);
  private readonly route = inject(ActivatedRoute);

  readonly showModal = signal(false);
  readonly selectedTicket = signal<Ticket | null>(null);
  readonly viewMode = signal<'board' | 'list'>('board');

  /**
   * Las formas de ver que este módulo sabe pintar.
   *
   * Se declaran aquí y no dentro de la barra porque cada módulo tiene las suyas: tareas añade
   * Gantt y carga de trabajo, y una barra que las supiese todas ofrecería en tickets pestañas que
   * no llevan a ninguna parte.
   */
  readonly VISTAS_INTEGRADAS: VistaIntegrada[] = [
    { clave: 'board', etiqueta: $localize`Tablero`, icono: 'lucideLayoutDashboard' },
    { clave: 'list',  etiqueta: $localize`Lista`,   icono: 'lucideList' }
  ];
  readonly isLoading = signal(false);

  // Table State
  tableState = signal<TableState>({
    page: 1,
    pageSize: 25,
    sortDirection: 'asc'
  });

  // DataTable columns definition
  tableColumns: ColumnDef[] = this.columnService.buildColumns<Ticket>({
    title: { label: $localize`Título` },
    description: { label: $localize`Descripción`, visible: false },
    status: { label: $localize`Estado`, type: 'custom' },
    priority: { label: $localize`Prioridad`, type: 'custom' },
    assignedAgentId: { label: $localize`Agente`, type: 'user' },
    createdAt: { label: $localize`Creado`, type: 'date' }
  });

  // Advanced Filters definition
  filterFields = computed<FilterField[]>(() => [
    { key: 'priority', label: 'Priority', type: 'select', options: this.priorities().map(p => ({ label: p, value: p })) },
    { key: 'status', label: 'Status', type: 'select', options: this.statuses.map(s => ({ label: s, value: s })) },
    { key: 'startDate', label: $localize`Desde`, type: 'date' },
    { key: 'endDate', label: $localize`Hasta`, type: 'date' }
  ]);

  // Saved Views
  savedViews = signal<SavedView[]>([]);
  activeViewId = signal<string | null>(null);

  // Data
  readonly allTickets = signal<Ticket[]>([]);
  totalItems = signal(0);
  
  cols: Column[] = COLUMN_DEFS.map(c => ({ ...c, tickets: [] as Ticket[], pendientes: [] as Ticket[] }));
  readonly columnIds = COLUMN_DEFS.map(c => c.key);

  readonly priorities = computed(() =>
    [...new Set(this.allTickets().map(t => t.priority).filter(Boolean))]
  );

  readonly statuses = ['Open', 'InProgress', 'Resolved', 'Closed'];

  statusBadge(status: string): BadgeVariant { return insigniaDelEstado(status); }

  readonly nombreDelEstado = nombreDelEstado;
  readonly nombreDeLaPrioridad = nombreDeLaPrioridad;

  @ViewChild('statusTemplate', { static: true }) statusTemplate!: TemplateRef<any>;
  @ViewChild('priorityTemplate', { static: true }) priorityTemplate!: TemplateRef<any>;

  ngOnInit(): void {
    this.tableColumns.find(c => c.key === 'status')!.template = this.statusTemplate;
    this.tableColumns.find(c => c.key === 'priority')!.template = this.priorityTemplate;
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
      this.loadTickets();
    });

    const userInfo = this.authStore.userInfo();
    if (userInfo?.tenantId) {
      this.realtime.connectTickets(userInfo.tenantId);
    }

    this.realtime.ticketMoved$.subscribe(({ ticketId, status }) => {
      // Map status number to string if necessary, assuming status string comes through.
      // If it comes as a number (0=Open, 1=InProgress, 2=Resolved, 3=Closed)
      const statusMap = ['Open', 'InProgress', 'Resolved', 'Closed'];
      const statusStr = typeof status === 'number' ? statusMap[status] : status;
      this.allTickets.update(tickets => tickets.map(t => t.id === ticketId ? { ...t, status: statusStr as string } : t));
      this.distributeTicketsToColumns();
    });
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

  saveCurrentView(name: string, isDefault = false): void {
    const currentState = { ...this.tableState(), viewType: this.viewMode() };
    const payload = {
      moduleName: 'Tickets',
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

  /**
   * Cambia a una vista de fábrica, y deja de estar en una guardada.
   *
   * Limpiar `activeViewId` importa: si no, la pestaña guardada seguiría marcada mientras se está
   * viendo otra cosa, que es enseñar dos verdades a la vez.
   */
  verComo(modo: string): void {
    this.viewMode.set(modo as 'board' | 'list');
    this.activeViewId.set(null);
  }

  crearVista({ nombre, tipo }: { nombre: string; tipo: string }): void {
    this.viewMode.set(tipo as 'board' | 'list');

    const estado = { ...this.tableState(), viewType: tipo };

    this.viewsService.saveView({
      moduleName: 'Tickets',
      viewName: nombre,
      stateJson: JSON.stringify(estado),
      isDefault: false
    }).subscribe({
      next: (view) => {
        this.savedViews.update(views => [...views, view]);
        this.activeViewId.set(view.id);
        this.tableState.set(estado as TableState);
      }
    });
  }

  /**
   * Borra una vista guardada.
   *
   * La API tenía el endpoint desde el principio y **no lo llamaba nadie**: se podían crear vistas
   * y no quitarlas. Si además la que estaba puesta era la borrada, se vuelve al tablero; dejar
   * marcada una pestaña que ya no existe deja la pantalla enseñando algo sin nombre.
   */
  borrarVista(vista: SavedView): void {
    this.viewsService.deleteView(vista.id).subscribe({
      next: () => {
        this.savedViews.update(views => views.filter(v => v.id !== vista.id));

        if (this.activeViewId() === vista.id) {
          this.activeViewId.set(null);
          this.viewMode.set('board');
          this.loadTickets();
        }
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
    
    const params: any = {
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
      if (state.filters['filter']) params.filter = state.filters['filter'];
    }

    this.api.get<{items: Ticket[], totalCount: number}>('/tickets', params).subscribe({
      next: res => {
        let tickets = res.items || [];
        
        // Filter out "Resolved" or "Closed" tickets older than 3 months for Board view (or always)
        const threeMonthsAgo = new Date();
        threeMonthsAgo.setMonth(threeMonthsAgo.getMonth() - 3);
        
        tickets = tickets.filter(t => {
          if (t.status === 'Resolved' || t.status === 'Closed') {
            const dateStr = t.createdAt; // Usando createdAt porque los tickets no tienen dueDate
            if (dateStr && new Date(dateStr) < threeMonthsAgo) {
              return false;
            }
          }
          return true;
        });

        this.totalItems.set(res.totalCount || 0);
        this.allTickets.set(tickets);
        this.distributeTicketsToColumns();
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  /**
   * Reparte los tickets por columna dejando fuera lo que excede la primera tanda.
   *
   * El corte se hace aquí y no en la plantilla: `cdkDropListData` y los índices del
   * arrastre tienen que referirse al mismo array que se pinta.
   */
  private distributeTicketsToColumns() {
    const tickets = this.allTickets();
    this.cols = COLUMN_DEFS.map(c => {
      const suyos = tickets.filter(t => t.status === c.key);
      const yaVisibles = this.cols.find(x => x.key === c.key)?.tickets.length ?? 0;
      const corte = Math.max(POR_TANDA, yaVisibles);
      return { ...c, tickets: suyos.slice(0, corte), pendientes: suyos.slice(corte) };
    });
  }

  /** Revela la siguiente tanda de una columna. */
  mostrarMas(col: Column): void {
    col.tickets = [...col.tickets, ...col.pendientes.slice(0, POR_TANDA)];
    col.pendientes = col.pendientes.slice(POR_TANDA);
  }

  /** Total real de la columna, contando lo que aún no se pinta. */
  totalColumna(col: Column): number {
    return col.tickets.length + col.pendientes.length;
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

  /**
   * Mueve el ticket al soltarlo.
   *
   * La tarjeta se mueve en pantalla antes de que conteste el servidor, porque esperar la
   * respuesta se percibe como que el tablero va lento. Eso obliga a poder deshacerlo: si
   * el servidor rechaza el cambio —permisos, red, un ticket que otro ya modificó— la
   * tarjeta vuelve a su columna y se avisa.
   *
   * Sin la reversión la interfaz miente: la tarjeta se queda donde se soltó y salta a su
   * sitio en la siguiente recarga, sin explicación.
   */
  drop(event: CdkDragDrop<Ticket[]>, targetKey: string): void {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
      return;
    }

    const ticket = event.previousContainer.data[event.previousIndex];
    const estadoAnterior = ticket.status;

    transferArrayItem(
      event.previousContainer.data,
      event.container.data,
      event.previousIndex,
      event.currentIndex
    );
    this.allTickets.update(tickets =>
      tickets.map(t => t.id === ticket.id ? { ...t, status: targetKey } : t)
    );

    this.api.patch(`/tickets/${ticket.id}`, { status: targetKey }).subscribe({
      error: () => {
        // Entre los mismos arrays que usa cdkDropList, no sólo en la señal, o el tablero
        // quedaría descuadrado respecto a lo que se ve.
        transferArrayItem(
          event.container.data,
          event.previousContainer.data,
          event.container.data.findIndex(t => t.id === ticket.id),
          event.previousIndex
        );
        this.allTickets.update(tickets =>
          tickets.map(t => t.id === ticket.id ? { ...t, status: estadoAnterior } : t)
        );

        this.toast.error($localize`No se pudo mover el ticket`,
          `«${ticket.title}» sigue en ${estadoAnterior}.`);
      },
    });
  }
}
