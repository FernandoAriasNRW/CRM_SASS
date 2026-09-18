import { Component, OnInit, TemplateRef, ViewChild, computed, effect, inject, signal } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { PRIORITIES, DEFAULT_PRIORITY, TaskCreateModalComponent, type TaskItem } from './task-create-modal.component';
import { TaskDetailPanelComponent } from './task-detail-panel.component';
import { TASK_STATUSES, taskStatusBadge, taskStatusLabel } from './task-vocabulary';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideRefreshCw, lucidePlus, lucideClock,
  lucideList, lucideLayoutDashboard, lucideFilter, lucideSave,
  lucideAlertCircle, lucideArrowUp, lucideMinus, lucideArrowDown, lucideListChecks, lucideUsers, lucideSquareCheck,
  lucideChartGantt, lucideChartColumn
} from '@ng-icons/lucide';
import { GanttComponent } from './gantt.component';
import { WorkloadComponent } from './workload.component';
import type { DependencyEdge } from './gantt';
import { DataTableComponent, ColumnDef, TableState, type CellEdit } from '../../shared/ui/data-table/data-table.component';
import { FilterField } from '../../shared/ui/data-table/advanced-filters.component';
import { ViewsService, SavedView } from '../../shared/services/views.service';
import { BarraDeVistasComponent, type VistaIntegrada } from '../../shared/ui/barra-de-vistas/barra-de-vistas.component';
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
  pending: TaskItem[];
}

/**
 * Tarjetas que se pintan por columna antes de pedir más.
 *
 * El tablero trae hasta 1000 tareas de una vez; pintarlas todas llenaba el DOM de
 * tarjetas que nadie llega a mirar, y una columna con cientos de elementos arrastrables
 * se nota al desplazarse. Un tablero se lee por arriba: lo que no cabe en una pantalla
 * casi nunca se consulta sin filtrar antes.
 */
const BATCH_SIZE = 25;

/**
 * Las columnas del tablero, sacadas del vocabulario compartido.
 *
 * Estaban escritas aquí con sus nombres en español a mano, y el cajón de detalle enseñaba las
 * claves del servidor —«To Do», «In Progress»—: la misma tarea decía dos cosas según dónde se
 * mirara, y ninguna de las dos se traducía al cambiar de idioma.
 */
const COLUMN_DEFS: Omit<Column, 'tasks' | 'pending'>[] = TASK_STATUSES.map(e => ({
  key: e.key,
  label: e.label,
  badge: e.badge
}));

/**
 * Los estados, en el orden del tablero. Se derivan de las columnas en lugar de repetirlos:
 * una lista aparte acabaría desincronizada el día que se añada un estado.
 *
 * Va aquí arriba y no como campo de la clase porque `tableColumns` lo necesita al construirse,
 * y los campos de instancia se inicializan en orden de declaración.
 */
const STATUS_KEYS = COLUMN_DEFS.map(c => c.key);

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [ClickableDirective, FormsModule, BadgeComponent, ButtonComponent, NgIconComponent, DragDropModule, TaskCreateModalComponent, TaskDetailPanelComponent, DataTableComponent, SkeletonListComponent, EmptyInlineComponent, GanttComponent, WorkloadComponent, BarraDeVistasComponent],
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
   * Las cuatro formas de ver que este módulo sabe pintar. Es la lista que dibuja las pestañas y
   * también la que valida qué modo puede ponerse: si estuviera escrita dos veces, guardar una
   * vista de Gantt acabaría abriendo un tablero.
   */
  readonly BUILT_IN_VIEWS: VistaIntegrada[] = [
    { clave: 'board', etiqueta: $localize`Tablero`, icono: 'lucideLayoutDashboard' },
    { clave: 'list', etiqueta: $localize`Lista`, icono: 'lucideList' },
    { clave: 'gantt', etiqueta: $localize`Gantt`, icono: 'lucideChartGantt' },
    { clave: 'carga', etiqueta: $localize`Carga`, icono: 'lucideChartColumn' }
  ];

  /**
   * El grafo de dependencias, para las flechas del Gantt.
   *
   * Se pide una sola vez y sólo al abrir el Gantt: es la única vista que lo necesita, y traerlo
   * con cada carga de tareas sería un viaje de más en el tablero y en la lista.
   */
  readonly dependencies = signal<DependencyEdge[]>([]);
  private dependenciesRequested = false;
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
    title: { label: $localize`Título`, editable: true },
    description: { label: $localize`Descripción`, visible: false },
    status: {
      label: $localize`Estado`, type: 'custom', editable: true, editor: 'select',
      options: TASK_STATUSES.map(e => ({ label: e.label, value: e.key })),
    },
    priority: {
      label: $localize`Prioridad`, type: 'custom', editable: true, editor: 'select',
      options: PRIORITIES.map(p => ({ label: p.label, value: p.key })),
    },
    assigneeId: { label: $localize`Asignado`, type: 'user' },
    estimatedHours: { label: $localize`Horas`, type: 'number', editable: true, editor: 'number' },
    dueDate: { label: $localize`Fecha límite`, type: 'date', editable: true, editor: 'date' }
  });

  // Advanced Filters definition
  filterFields = computed<FilterField[]>(() => [
    { key: 'projectId', label: $localize`Proyecto`, type: 'select', options: this.projectOptions() },
    { key: 'status', label: $localize`Estado`, type: 'select', options: TASK_STATUSES.map(e => ({ label: e.label, value: e.key })) },
    { key: 'priority', label: $localize`Prioridad`, type: 'select', options: PRIORITIES.map(p => ({ label: p.label, value: p.key })) },
    { key: 'startDate', label: $localize`Desde`, type: 'date' },
    { key: 'endDate', label: $localize`Hasta`, type: 'date' }
  ]);

  // Saved Views
  savedViews = signal<SavedView[]>([]);
  activeViewId = signal<string | null>(null);

  // Data
  readonly allTasks = signal<TaskItem[]>([]);
  totalItems = signal(0);
  
  cols: Column[] = COLUMN_DEFS.map(c => ({ ...c, tasks: [] as TaskItem[], pending: [] as TaskItem[] }));
  readonly columnIds = COLUMN_DEFS.map(c => c.key);

  readonly projectOptions = computed(() => {
    const seen = new Set<string>();
    return this.allTasks()
      .filter(t => t.projectId && !seen.has(t.projectId) && (seen.add(t.projectId), true))
      .map(t => ({ label: t.projectId, value: t.projectId }));
  });

  statusBadge(status: string): BadgeVariant { return taskStatusBadge(status); }

  readonly statusLabel = taskStatusLabel;

  /** La prioridad tal como se pinta. Ante un valor desconocido, cae en la normal. */
  priorityOf(priority: string) {
    return PRIORITIES.find(p => p.key === priority)
      ?? PRIORITIES.find(p => p.key === DEFAULT_PRIORITY)!;
  }

  /** Texto del progreso de la checklist para el `title` de la tarjeta. */
  checklistTitle(task: TaskItem): string {
    return $localize`${task.checklistDone ?? 0} de ${task.checklistTotal} puntos hechos`;
  }

  /** Texto del distintivo de responsables para el `title` de la tarjeta. */
  assigneesTitle(task: TaskItem): string {
    return $localize`${task.assignees?.length ?? 0} personas responsables`;
  }

  /** Texto del distintivo de bloqueada para el `title` de la tarjeta. */
  blockedTitle(task: TaskItem): string {
    return $localize`Bloqueada por ${task.blockedByCount} tarea(s)`;
  }

  /** Texto del progreso de subtareas para el `title` de la tarjeta. */
  progressTitle(task: TaskItem): string {
    return $localize`${task.completedSubtaskCount ?? 0} de ${task.subtaskCount} subtareas completadas`;
  }

  /**
   * Si la prioridad merece distintivo en la tarjeta.
   *
   * Sólo lo que se sale de lo normal. Marcar las cuatro llenaría el tablero de etiquetas
   * equivalentes y no señalaría nada; y una prioridad vacía —fila anterior a que existieran—
   * tampoco es una señal.
   */
  isHighlightedPriority(priority: string): boolean {
    return !!priority && priority !== DEFAULT_PRIORITY && PRIORITIES.some(p => p.key === priority);
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
    });
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

  /** Cambia a una vista de fábrica y deja de estar en una guardada. */
  viewAs(mode: string): void {
    this.applyMode(mode);
    this.activeViewId.set(null);
  }

  createView({ nombre, tipo }: { nombre: string; tipo: string }): void {
    this.applyMode(tipo);

    const status = { ...this.tableState(), viewType: tipo };

    this.viewsService.saveView({
      moduleName: 'Tasks',
      viewName: nombre,
      stateJson: JSON.stringify(status),
      isDefault: false
    }).subscribe({
      next: (view) => {
        this.savedViews.update(views => [...views, view]);
        this.activeViewId.set(view.id);
        this.tableState.set(status as TableState);
      }
    });
  }

  /**
   * Borra una vista guardada. La API tenía el endpoint desde el principio y no lo llamaba nadie:
   * se podían crear vistas y no quitarlas.
   */
  deleteView(view: SavedView): void {
    this.viewsService.deleteView(view.id).subscribe({
      next: () => {
        this.savedViews.update(views => views.filter(v => v.id !== view.id));

        if (this.activeViewId() === view.id) {
          this.activeViewId.set(null);
          this.applyMode('board');
          this.loadTasks();
        }
      }
    });
  }

  applySavedView(view: SavedView): void {
    this.activeViewId.set(view.id);
    try {
      const state = JSON.parse(view.stateJson) as TableState;
      this.tableState.set(state);
      if (state.viewType) this.applyMode(state.viewType);
      this.loadTasks();
    } catch (e) {
      console.error('Failed to parse saved view state', e);
    }
  }

  /**
   * Pone un modo comprobando que sea uno de los que este módulo pinta.
   *
   * Antes se comparaba a mano contra 'board' y 'list', así que **una vista guardada de Gantt o de
   * carga se abría como tablero**: la pestaña quedaba marcada y debajo salía otra cosa. Se valida
   * contra la misma lista que dibuja las pestañas, que es la única forma de que no se
   * desincronicen.
   */
  private applyMode(mode: string): void {
    if (!this.BUILT_IN_VIEWS.some(v => v.clave === mode)) return;

    // El Gantt no es sólo un modo: la primera vez tiene que pedir el grafo de dependencias, o
    // sale sin flechas. Se pasa por `showGantt` en lugar de poner la señal a mano, que es lo que
    // hacía que una vista guardada de Gantt se abriera pelada.
    if (mode === 'gantt') {
      this.showGantt();
      return;
    }

    this.viewMode.set(mode as 'board' | 'list' | 'carga');
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
      const mine = tasks.filter(t => t.status === c.key);
      // Se conserva lo ya revelado al recargar: si alguien pulsó «mostrar más» y luego
      // llega una actualización, volver a esconderlo sería desconcertante.
      const alreadyVisible = this.cols.find(x => x.key === c.key)?.tasks.length ?? 0;
      const cutoff = Math.max(BATCH_SIZE, alreadyVisible);
      return { ...c, tasks: mine.slice(0, cutoff), pending: mine.slice(cutoff) };
    });
  }

  /** Revela la siguiente tanda de una columna. */
  showMore(col: Column): void {
    col.tasks = [...col.tasks, ...col.pending.slice(0, BATCH_SIZE)];
    col.pending = col.pending.slice(BATCH_SIZE);
  }

  /** Total real de la columna, contando lo que aún no se pinta. */
  columnTotal(col: Column): number {
    return col.tasks.length + col.pending.length;
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
    const previousStatus = task.status;

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
          tasks.map(t => t.id === task.id ? { ...t, status: previousStatus } : t)
        );

        this.toast.error($localize`No se pudo mover la tarea`,
          `«${task.title}» sigue en ${previousStatus}.`);
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
  onCellEdit({ item, key, valor: value }: CellEdit<TaskItem>): void {
    const previous = (item as unknown as Record<string, unknown>)[key];
    const updated = key === 'estimatedHours' ? Number(value) : value;

    if (key === 'estimatedHours' && Number.isNaN(updated as number)) {
      this.toast.error(
        $localize`No se pudo guardar`,
        $localize`«${value}» no es un número de horas.`);
      return;
    }

    this.applyInList(item.id, key, updated);

    // `sinAviso`: el error se cuenta abajo con el nombre de la tarea que se revirtió, que es lo
    // único que el interceptor no puede saber. Sin esto salían los dos avisos.
    this.api.patch(`/tasks/${item.id}`, { [key]: updated }, { sinAviso: true }).subscribe({
      next: () => this.distributeTasksToColumns(),
      error: response => {
        this.applyInList(item.id, key, previous);
        this.toast.error(
          $localize`«${item.title}» se queda como estaba`,
          mensajeDeError(response, $localize`No se pudo guardar el cambio.`));
      },
    });
  }

  /**
   * Abre el Gantt y, la primera vez, trae el grafo de dependencias.
   *
   * Si falla, el diagrama se pinta sin flechas en lugar de no pintarse: las barras siguen
   * diciendo la verdad, y quedarse sin vista por no poder dibujar un adorno sería peor.
   */
  showGantt(): void {
    this.viewMode.set('gantt');

    if (this.dependenciesRequested) return;
    this.dependenciesRequested = true;

    this.api.get<DependencyEdge[]>('/tasks/dependencies').subscribe({
      next: edges => this.dependencies.set(edges ?? []),
      error: () => this.dependenciesRequested = false,
    });
  }

  private applyInList(id: string, key: string, value: unknown): void {
    this.allTasks.update(tasks =>
      tasks.map(t => t.id === id ? { ...t, [key]: value } : t));
  }

  readonly statuses = STATUS_KEYS;
}
