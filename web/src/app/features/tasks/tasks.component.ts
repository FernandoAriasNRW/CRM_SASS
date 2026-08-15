import { Component, OnInit, TemplateRef, ViewChild, computed, effect, inject, signal } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { PRIORIDADES, PRIORIDAD_POR_DEFECTO, TaskCreateModalComponent, type TaskItem } from './task-create-modal.component';
import { TaskDetailPanelComponent } from './task-detail-panel.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideRefreshCw, lucidePlus, lucideClock,
  lucideList, lucideLayoutDashboard, lucideFilter, lucideSave,
  lucideAlertCircle, lucideArrowUp, lucideMinus, lucideArrowDown, lucideListChecks, lucideUsers, lucideSquareCheck,
  lucideChartGantt, lucideChartColumn
} from '@ng-icons/lucide';
import { GanttComponent } from './gantt.component';
import { CargaComponent } from './carga.component';
import type { AristaDeDependencia } from './gantt';
import { DataTableComponent, ColumnDef, TableState, type CellEdit } from '../../shared/ui/data-table/data-table.component';
import { FilterField } from '../../shared/ui/data-table/advanced-filters.component';
import { ViewsService, SavedView } from '../../shared/services/views.service';
import { TableColumnService } from '../../shared/services/table-column.service';
import { HierarchySignalStore } from '../../core/hierarchy-signal.store';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { ToastService } from '../../shared/services/toast.service';
import { mensajeDeError } from '../../shared/utils/mensaje-de-error';
import { SkeletonListComponent } from '../../shared/ui/skeleton.component';
import { EmptyInlineComponent } from '../../shared/ui/empty-state.component';

export interface Column {
  key: string;
  label: string;
  badge: BadgeVariant;
  /** Lo que se pinta y sobre lo que opera el arrastre. */
  tasks: TaskItem[];
  /** El resto, aún sin pintar. Se revela por tandas con «mostrar más». */
  pendientes: TaskItem[];
}

/**
 * Tarjetas que se pintan por columna antes de pedir más.
 *
 * El tablero trae hasta 1000 tareas de una vez; pintarlas todas llenaba el DOM de
 * tarjetas que nadie llega a mirar, y una columna con cientos de elementos arrastrables
 * se nota al desplazarse. Un tablero se lee por arriba: lo que no cabe en una pantalla
 * casi nunca se consulta sin filtrar antes.
 */
const POR_TANDA = 25;

const COLUMN_DEFS: Omit<Column, 'tasks' | 'pendientes'>[] = [
  { key: 'To Do',       label: 'Por hacer',  badge: 'secondary' },
  { key: 'In Progress', label: 'En progreso', badge: 'default'   },
  { key: 'In Review',   label: 'En revisión', badge: 'warning'   },
  { key: 'Done',        label: 'Completado',  badge: 'success'   },
];

const STATUS_BADGE: Record<string, BadgeVariant> = {
  'To Do': 'secondary', 'In Progress': 'default', 'In Review': 'warning', 'Done': 'success'
};

/**
 * Los estados, en el orden del tablero. Se derivan de las columnas en lugar de repetirlos:
 * una lista aparte acabaría desincronizada el día que se añada un estado.
 *
 * Va aquí arriba y no como campo de la clase porque `tableColumns` lo necesita al construirse,
 * y los campos de instancia se inicializan en orden de declaración.
 */
const ESTADOS = COLUMN_DEFS.map(c => c.key);

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [ClickableDirective, FormsModule, BadgeComponent, ButtonComponent, NgIconComponent, DragDropModule, TaskCreateModalComponent, TaskDetailPanelComponent, DataTableComponent, SkeletonListComponent, EmptyInlineComponent, GanttComponent, CargaComponent],
  viewProviders: [provideIcons({
    lucideRefreshCw, lucidePlus, lucideClock,
    lucideList, lucideLayoutDashboard, lucideFilter, lucideSave,
    lucideAlertCircle, lucideArrowUp, lucideMinus, lucideArrowDown, lucideListChecks, lucideUsers, lucideSquareCheck,
    lucideChartGantt, lucideChartColumn
  })],
  templateUrl: './tasks.component.html',
})
export class TasksComponent implements OnInit {
  private readonly toast = inject(ToastService);
  private readonly api = inject(ApiService);
  private readonly realtime = inject(RealtimeService);
  private readonly viewsService = inject(ViewsService);
  private readonly columnService = inject(TableColumnService);
  private readonly hierarchyStore = inject(HierarchySignalStore);
  private readonly route = inject(ActivatedRoute);

  readonly showModal = signal(false);
  readonly selectedTask = signal<TaskItem | null>(null);
  readonly viewMode = signal<'board' | 'list' | 'gantt' | 'carga'>('board');

  /**
   * El grafo de dependencias, para las flechas del Gantt.
   *
   * Se pide una sola vez y sólo al abrir el Gantt: es la única vista que lo necesita, y traerlo
   * con cada carga de tareas sería un viaje de más en el tablero y en la lista.
   */
  readonly dependencias = signal<AristaDeDependencia[]>([]);
  private dependenciasPedidas = false;
  readonly isLoading = signal(false);

  // Table State
  tableState = signal<TableState>({
    page: 1,
    pageSize: 25,
    sortDirection: 'asc'
  });
  
  // DataTable columns definition
  /**
   * Las columnas de la vista de lista.
   *
   * Se pueden editar las que el servidor acepta en un `PATCH /tasks/{id}`: título, estado,
   * prioridad, horas y fecha límite. El responsable queda fuera **a propósito**: tiene su
   * propio endpoint porque una tarea admite varios y uno de ellos es el principal, y meter eso
   * en una celda de una sola línea sería prometer algo que la pantalla no puede cumplir.
   */
  tableColumns: ColumnDef[] = this.columnService.buildColumns<TaskItem>({
    title: { label: 'Title', editable: true },
    description: { label: 'Description', visible: false },
    status: {
      label: 'Status', type: 'custom', editable: true, editor: 'select',
      options: ESTADOS.map(s => ({ label: s, value: s })),
    },
    priority: {
      label: $localize`Prioridad`, type: 'custom', editable: true, editor: 'select',
      options: PRIORIDADES.map(p => ({ label: p.label, value: p.key })),
    },
    assigneeId: { label: 'Asignado', type: 'user' },
    estimatedHours: { label: 'Hours', type: 'number', editable: true, editor: 'number' },
    dueDate: { label: 'Due Date', type: 'date', editable: true, editor: 'date' }
  });

  // Advanced Filters definition
  filterFields = computed<FilterField[]>(() => [
    { key: 'projectId', label: 'Project', type: 'select', options: this.projectOptions() },
    { key: 'status', label: 'Status', type: 'select', options: this.statuses.map(s => ({ label: s, value: s })) },
    { key: 'priority', label: $localize`Prioridad`, type: 'select', options: PRIORIDADES.map(p => ({ label: p.label, value: p.key })) },
    { key: 'startDate', label: 'Start Date', type: 'date' },
    { key: 'endDate', label: 'End Date', type: 'date' }
  ]);

  // Saved Views
  savedViews = signal<SavedView[]>([]);
  activeViewId = signal<string | null>(null);

  // Data
  readonly allTasks = signal<TaskItem[]>([]);
  totalItems = signal(0);
  
  cols: Column[] = COLUMN_DEFS.map(c => ({ ...c, tasks: [] as TaskItem[], pendientes: [] as TaskItem[] }));
  readonly columnIds = COLUMN_DEFS.map(c => c.key);

  readonly projectOptions = computed(() => {
    const seen = new Set<string>();
    return this.allTasks()
      .filter(t => t.projectId && !seen.has(t.projectId) && (seen.add(t.projectId), true))
      .map(t => ({ label: t.projectId, value: t.projectId }));
  });

  statusBadge(status: string): BadgeVariant { return STATUS_BADGE[status] ?? 'outline'; }

  /** La prioridad tal como se pinta. Ante un valor desconocido, cae en la normal. */
  prioridadDe(priority: string) {
    return PRIORIDADES.find(p => p.key === priority)
      ?? PRIORIDADES.find(p => p.key === PRIORIDAD_POR_DEFECTO)!;
  }

  /** Texto del progreso de la checklist para el `title` de la tarjeta. */
  tituloDeChecklist(task: TaskItem): string {
    return $localize`${task.checklistDone ?? 0} de ${task.checklistTotal} puntos hechos`;
  }

  /** Texto del distintivo de responsables para el `title` de la tarjeta. */
  tituloDeResponsables(task: TaskItem): string {
    return $localize`${task.assignees?.length ?? 0} personas responsables`;
  }

  /** Texto del distintivo de bloqueada para el `title` de la tarjeta. */
  tituloDelBloqueo(task: TaskItem): string {
    return $localize`Bloqueada por ${task.blockedByCount} tarea(s)`;
  }

  /** Texto del progreso de subtareas para el `title` de la tarjeta. */
  tituloDelProgreso(task: TaskItem): string {
    return $localize`${task.completedSubtaskCount ?? 0} de ${task.subtaskCount} subtareas completadas`;
  }

  /**
   * Si la prioridad merece distintivo en la tarjeta.
   *
   * Sólo lo que se sale de lo normal. Marcar las cuatro llenaría el tablero de etiquetas
   * equivalentes y no señalaría nada; y una prioridad vacía —fila anterior a que existieran—
   * tampoco es una señal.
   */
  esPrioridadDestacable(priority: string): boolean {
    return !!priority && priority !== PRIORIDAD_POR_DEFECTO && PRIORIDADES.some(p => p.key === priority);
  }

  @ViewChild('statusTemplate', { static: true }) statusTemplate!: TemplateRef<unknown>;
  @ViewChild('priorityTemplate', { static: true }) priorityTemplate!: TemplateRef<unknown>;

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

  saveCurrentView(name: string, isDefault = false): void {
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
    
    const params: any = {
      pageNumber: state.page,
      // El tablero y el Gantt piden todo: los dos colocan cada tarea en su sitio —columna o
      // fecha— y una página suelta dejaría huecos que parecerían trabajo inexistente.
      pageSize: this.viewMode() === 'list' ? state.pageSize : 1000,
      sortColumn: state.sortColumn,
      sortDirection: state.sortDirection,
      searchTerm: state.searchTerm
    };

    if (state.filters) {
      if (state.filters['startDate']) params.startDate = state.filters['startDate'];
      if (state.filters['endDate']) params.endDate = state.filters['endDate'];
      if (state.filters['projectId']) params.projectId = state.filters['projectId'];
      if (state.filters['status']) params.status = state.filters['status'];
      if (state.filters['priority']) params.priority = state.filters['priority'];
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

  /**
   * Reparte las tareas por columna, dejando fuera de la vista lo que excede la primera
   * tanda.
   *
   * El corte se hace aquí y no en la plantilla a propósito: `cdkDropListData` y los
   * índices que maneja el arrastre tienen que referirse al MISMO array que se pinta. Si
   * la plantilla mostrara un `slice` mientras el arrastre opera sobre la lista completa,
   * los índices no coincidirían y las tarjetas acabarían en posiciones equivocadas.
   */
  private distributeTasksToColumns() {
    const tasks = this.allTasks();
    this.cols = COLUMN_DEFS.map(c => {
      const suyas = tasks.filter(t => t.status === c.key);
      // Se conserva lo ya revelado al recargar: si alguien pulsó «mostrar más» y luego
      // llega una actualización, volver a esconderlo sería desconcertante.
      const yaVisibles = this.cols.find(x => x.key === c.key)?.tasks.length ?? 0;
      const corte = Math.max(POR_TANDA, yaVisibles);
      return { ...c, tasks: suyas.slice(0, corte), pendientes: suyas.slice(corte) };
    });
  }

  /** Revela la siguiente tanda de una columna. */
  mostrarMas(col: Column): void {
    col.tasks = [...col.tasks, ...col.pendientes.slice(0, POR_TANDA)];
    col.pendientes = col.pendientes.slice(POR_TANDA);
  }

  /** Total real de la columna, contando lo que aún no se pinta. */
  totalColumna(col: Column): number {
    return col.tasks.length + col.pendientes.length;
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

  /**
   * Mueve la tarjeta al soltarla.
   *
   * La tarjeta se mueve en pantalla antes de que conteste el servidor, porque esperar la
   * respuesta se percibe como que el tablero va lento. La contrapartida es que hay que
   * poder deshacerlo: si el servidor rechaza el movimiento —una transición no permitida,
   * un problema de permisos, la red— la tarjeta vuelve a su columna y se avisa.
   *
   * Sin esa reversión la interfaz miente: la tarjeta se queda donde el usuario la soltó y
   * salta a su sitio en la siguiente recarga, sin explicación.
   */
  drop(event: CdkDragDrop<TaskItem[]>, targetKey: string): void {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
      return;
    }

    const task = event.previousContainer.data[event.previousIndex];
    const estadoAnterior = task.status;

    transferArrayItem(
      event.previousContainer.data,
      event.container.data,
      event.previousIndex,
      event.currentIndex
    );
    this.allTasks.update(tasks =>
      tasks.map(t => t.id === task.id ? { ...t, status: targetKey } : t)
    );

    this.api.post(`/tasks/${task.id}/move`, { newStatus: targetKey }).subscribe({
      error: () => {
        // Devolver la tarjeta a su columna. Se mueve entre los mismos arrays que usa el
        // cdkDropList, no sólo en la señal, o el tablero quedaría descuadrado respecto a
        // lo que se ve.
        transferArrayItem(
          event.container.data,
          event.previousContainer.data,
          event.container.data.findIndex(t => t.id === task.id),
          event.previousIndex
        );
        this.allTasks.update(tasks =>
          tasks.map(t => t.id === task.id ? { ...t, status: estadoAnterior } : t)
        );

        this.toast.error(
          'No se pudo mover la tarea',
          `«${task.title}» sigue en ${estadoAnterior}.`);
      },
    });
  }


  /**
   * Guarda lo editado en una celda de la lista.
   *
   * Se pinta antes de tener respuesta y **se revierte si el servidor rechaza**, igual que al
   * arrastrar una tarjeta en el tablero: dejar en pantalla un valor que no se guardó hace que
   * alguien se vaya creyendo que el cambio quedó hecho.
   *
   * El aviso de error dice qué tarea y a qué valor ha vuelto. Un «no se pudo guardar» a secas
   * obliga a adivinar cuál de las veinticinco filas es.
   */
  onCellEdit({ item, key, valor }: CellEdit<TaskItem>): void {
    const anterior = (item as unknown as Record<string, unknown>)[key];
    const nuevo = key === 'estimatedHours' ? Number(valor) : valor;

    if (key === 'estimatedHours' && Number.isNaN(nuevo as number)) {
      this.toast.error(
        $localize`No se pudo guardar`,
        $localize`«${valor}» no es un número de horas.`);
      return;
    }

    this.aplicarEnLista(item.id, key, nuevo);

    // `sinAviso`: el error se cuenta abajo con el nombre de la tarea que se revirtió, que es lo
    // único que el interceptor no puede saber. Sin esto salían los dos avisos.
    this.api.patch(`/tasks/${item.id}`, { [key]: nuevo }, { sinAviso: true }).subscribe({
      next: () => this.distributeTasksToColumns(),
      error: respuesta => {
        this.aplicarEnLista(item.id, key, anterior);
        this.toast.error(
          $localize`«${item.title}» se queda como estaba`,
          mensajeDeError(respuesta, $localize`No se pudo guardar el cambio.`));
      },
    });
  }

  /**
   * Abre el Gantt y, la primera vez, trae el grafo de dependencias.
   *
   * Si falla, el diagrama se pinta sin flechas en lugar de no pintarse: las barras siguen
   * diciendo la verdad, y quedarse sin vista por no poder dibujar un adorno sería peor.
   */
  verGantt(): void {
    this.viewMode.set('gantt');

    if (this.dependenciasPedidas) return;
    this.dependenciasPedidas = true;

    this.api.get<AristaDeDependencia[]>('/tasks/dependencies').subscribe({
      next: aristas => this.dependencias.set(aristas ?? []),
      error: () => this.dependenciasPedidas = false,
    });
  }

  private aplicarEnLista(id: string, key: string, valor: unknown): void {
    this.allTasks.update(tasks =>
      tasks.map(t => t.id === id ? { ...t, [key]: valor } : t));
  }

  readonly statuses = ESTADOS;
}
