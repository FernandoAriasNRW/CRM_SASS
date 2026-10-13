import { Component, ElementRef, inject, OnInit, signal, computed, viewChild } from '@angular/core';
import { ApiService } from '../../core/api.service';
import {
  CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent
} from '../../shared/ui/card.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideFolderKanban, lucideCheckSquare, lucideTicket, lucideTrendingUp,
  lucideClock, lucideActivity, lucidePieChart, lucideBarChart3,
  lucidePlus, lucideSettings, lucideLayout, lucideGlobe, lucideLock, lucideTag,
  lucideGripVertical, lucideMoveDiagonal2
} from '@ng-icons/lucide';
import { TagChipsComponent } from '../../shared/ui/tag-chips.component';
import { TagAssignDrawerComponent } from '../../shared/ui/tag-assign-drawer.component';
import { LineChartComponent, type BurndownDataPoint } from '../../shared/ui/charts/line-chart.component';
import { ProgressBarComponent } from '../../shared/ui/progress-bar.component';
import { DashboardsService, Dashboard } from '../../shared/services/dashboards.service';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  ReportChartComponent, type WidgetData
} from '../../shared/ui/charts/report-chart.component';
import { moveTo, resizeTo, sameLayout, type Placement } from './dashboard-layout';
import { errorMessage } from '../../shared/utils/error-message';

interface KpiData {
  totalProjects: number;
  totalTasks: number;
  doneTasks: number;
  throughput: number;
  openTickets: number;
  inProgressTickets: number;
  // Pueden venir nulos: el servidor devuelve un hueco cuando no tiene con qué calcularlos
  // —ninguna tarea cerrada todavía, o, en el caso del ciclo, ningún historial de estados—.
  // Antes eran constantes escritas a mano en el backend, así que nunca faltaban.
  avgLeadTimeDays: number | null;
  avgCycleTimeDays: number | null;
}

interface TaskStatusBreakdown {
  status: string;
  count: number;
  color: string;
}

interface ProjectProgress {
  id: string;
  name: string;
  status: string;
  totalTasks: number;
  doneTasks: number;
  completionPct: number;
}

interface ProjectBurndown {
  projectId: string;
  projectName: string;
  data: { date: string; remainingTasks: number; idealTasks: number }[];
}

import { AuthSignalStore } from '../../core/auth-signal.store';
import { HierarchySignalStore } from '../../core/hierarchy-signal.store';


import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '../../shared/ui/button.component';
import { DrawerComponent } from '../../shared/ui/drawer.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    FormsModule,
    ButtonComponent,
    CardComponent,
    CardHeaderComponent,
    CardTitleComponent,
    CardContentComponent,
    NgIconComponent,
    LineChartComponent,
    ProgressBarComponent,
    DrawerComponent,
    ReportChartComponent,
    TagChipsComponent,
    TagAssignDrawerComponent
],
  viewProviders: [provideIcons({
    lucideFolderKanban, lucideCheckSquare, lucideTicket, lucideTrendingUp,
    lucideClock, lucideActivity, lucidePieChart, lucideBarChart3,
    lucidePlus, lucideSettings, lucideLayout, lucideGlobe, lucideLock, lucideTag,
    lucideGripVertical, lucideMoveDiagonal2
  })],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dashboardsService = inject(DashboardsService);
  private readonly route = inject(ActivatedRoute);
  readonly authStore = inject(AuthSignalStore);
  readonly hierarchyStore = inject(HierarchySignalStore);

  readonly kpi = signal<KpiData | null>(null);
  readonly taskBreakdown = signal<TaskStatusBreakdown[]>([]);
  readonly projectProgress = signal<ProjectProgress[]>([]);
  readonly selectedProjectBurndown = signal<ProjectBurndown | null>(null);
  readonly selectedProjectId = signal<string | null>(null);
  readonly isLoading = signal(true);

  // ── Mi panel ──────────────────────────────────────────────────────────────────────────────
  //
  // La rejilla es de quien la mira: el servidor devuelve el panel de esta persona, y lo crea con
  // los informes de partida la primera vez. Antes esta pantalla tenía tres gráficas fijas iguales
  // para todo el mundo.

  readonly panel = signal<{ id: string; title: string; isMine: boolean } | null>(null);
  readonly widgets = signal<WidgetData[]>([]);
  readonly myDashboardLoading = signal(true);

  // Dashboards feature
  readonly customDashboards = signal<Dashboard[]>([]);
  readonly selectedDashboard = signal<Dashboard | null>(null);
  readonly viewType = signal<'default' | 'private' | 'public' | 'all'>('default');
  
  // Create Modal
  showCreateModal = false;
  newDashboardTitle = '';
  newDashboardIsPublic = false;

  // `doughnutData` vivía aquí para la tarta de distribución de tareas. Se ha quitado porque eso
  // es ahora un recuadro del panel —«tareas por estado, en tarta»— y tenerlo en los dos sitios
  // sería enseñar lo mismo dos veces y arriesgarse a que un día discrepen.

  readonly burndownData = computed((): BurndownDataPoint[] => {
    const bd = this.selectedProjectBurndown();
    if (!bd || bd.data.length === 0) return [];
    return bd.data.map(d => ({
      date: d.date,
      remainingTasks: d.remainingTasks,
      idealTasks: d.idealTasks
    }));
  });

  readonly kpiCards = () => {
    const k = this.kpi();
    return [
      { label: $localize`Proyectos`, value: k?.totalProjects ?? '—', icon: 'lucideFolderKanban', sub: $localize`Total activos` },
      { label: $localize`Tareas`, value: k?.totalTasks ?? '—', icon: 'lucideCheckSquare', sub: $localize`${k?.doneTasks ?? 0}:COMPLETADAS: completadas` },
      { label: $localize`Tickets abiertos`, value: k?.openTickets ?? '—', icon: 'lucideTicket', sub: $localize`${k?.inProgressTickets ?? 0}:ENPROGRESO: en progreso` },
      { label: $localize`Rendimiento`, value: k ? `${k.throughput}%` : '—', icon: 'lucideTrendingUp', sub: $localize`Tareas completadas` },
      // El guión ya estaba para «aún no ha llegado la respuesta»; ahora cubre también «el
      // servidor no tiene con qué calcularlo». Son dos cosas distintas para quien programa y
      // la misma para quien mira: no hay dato. Lo que no puede pasar es inventarse un 0,0d,
      // que se leería como «se entrega al instante».
      { label: $localize`Tiempo de entrega`, value: k?.avgLeadTimeDays != null ? `${k.avgLeadTimeDays.toFixed(1)}d` : '—', icon: 'lucideClock', sub: $localize`Promedio` },
      { label: $localize`Tiempo de ciclo`, value: k?.avgCycleTimeDays != null ? `${k.avgCycleTimeDays.toFixed(1)}d` : '—', icon: 'lucideActivity', sub: $localize`Promedio` },
    ];
  };

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const type = params['type'];
      if (type === 'private' || type === 'public' || type === 'all') {
        this.viewType.set(type);
      } else {
        this.viewType.set('default');
      }
      this.selectedDashboard.set(null); // Reset selection
      this.loadDashboards();
    });
    this.loadAllData();
    void this.loadMyDashboard();
  }

  loadDashboards(): void {
    this.dashboardsService.getAllDashboards().subscribe({
      next: (dashboards) => {
        let filtered = dashboards;
        if (this.viewType() === 'private') {
          filtered = dashboards.filter(d => !d.isPublic && d.createdById === this.authStore.userInfo()?.id);
        } else if (this.viewType() === 'public') {
          filtered = dashboards.filter(d => d.isPublic);
        } else if (this.viewType() === 'all') {
          filtered = dashboards;
        } else {
          filtered = []; // For default, we don't show custom list
        }
        this.customDashboards.set(filtered);
      },
      error: () => {}
    });
  }

  openCreateModal(): void {
    this.newDashboardTitle = '';
    this.newDashboardIsPublic = false;
    this.showCreateModal = true;
  }

  createDashboard(): void {
    if (!this.newDashboardTitle) return;
    this.dashboardsService.createDashboard({
      title: this.newDashboardTitle,
      isPublic: this.newDashboardIsPublic
    }).subscribe({
      next: () => {
        this.showCreateModal = false;
        this.loadDashboards();
      }
    });
  }

  deleteDashboard(id: string): void {
    if (!confirm('¿Seguro que deseas eliminar este dashboard?')) return;
    this.dashboardsService.deleteDashboard(id).subscribe({
      next: () => this.loadDashboards()
    });
  }

  /** El panel cuyas etiquetas se están eligiendo, o `null`. */
  readonly dashboardTagging = signal<Dashboard | null>(null);

  /**
   * Por su propio endpoint y no con el PUT del panel entero: así no se reenvían los recuadros, que
   * otra persona puede haber cambiado mientras tanto.
   */
  readonly saveDashboardTags = (tagIds: string[]) =>
    this.api.put<void>(`/dashboards/${this.dashboardTagging()!.id}/tags`, { tagIds }, { silent: true });

  /** Quién puede cambiar sus etiquetas: quien lo creó o un administrador, como para borrarlo. */
  canEditDashboard(dashboard: Dashboard): boolean {
    return dashboard.createdById === this.authStore.userInfo()?.id || this.authStore.isAdmin();
  }

  selectDashboard(dashboard: Dashboard): void {
    this.selectedDashboard.set(dashboard);
  }

  /**
   * Carga el panel y **los datos de todos sus recuadros en una sola petición**.
   *
   * Es la decisión del plan: «un widget que trae sus propios datos con su propia llamada convierte
   * el dashboard en veinte peticiones». Y además, pidiéndolos juntos, todos los recuadros son de
   * la misma foto: con veinte llamadas escalonadas, el de arriba puede contar tickets de antes de
   * que llegara uno nuevo y el de abajo de después.
   */
  async loadMyDashboard(): Promise<void> {
    this.myDashboardLoading.set(true);

    try {
      const mine = await firstValueFrom(
        this.api.get<{ id: string; title: string; isMine: boolean; widgets: Placement[] }>('/dashboards/mine'));

      this.panel.set(mine);
      this.layout.set(mine.widgets ?? []);

      const data = await firstValueFrom(
        this.api.get<WidgetData[]>(`/dashboards/${mine.id}/data`));

      this.widgets.set(data);
    } finally {
      this.myDashboardLoading.set(false);
    }
  }

  reloadMyDashboard(): void { void this.loadMyDashboard(); }

  async removeWidget(widgetId: string): Promise<void> {
    const panel = this.panel();
    if (!panel) return;

    await firstValueFrom(this.api.delete(`/dashboards/${panel.id}/widgets/${widgetId}`));

    // Se quita de la lista en vez de recargar el panel entero: recargar volvería a consultar
    // todos los informes para enseñar uno menos.
    this.widgets.update(ws => ws.filter(w => w.widgetId !== widgetId));
  }

  /**
   * Dónde empieza y cuánto ocupa un recuadro en la rejilla de CSS.
   *
   * El servidor guarda columna y ancho en base 0 y CSS cuenta desde 1, así que se suma uno. Es un
   * desfase pequeño y de los que se pagan caros: sin él, todos los recuadros aparecen una columna
   * a la izquierda y el último se sale de la pantalla.
   */
  gridColumnOf(w: WidgetData): string {
    const widget = this.placementOf(w.widgetId);
    return widget ? `${widget.x + 1} / span ${widget.width}` : 'auto / span 6';
  }

  /**
   * La fila de inicio sale de `y`. Antes sólo se ponía el alto (`span N`) y CSS colocaba cada
   * recuadro donde cupiera: la fila guardada se ignoraba, y mover un recuadro no habría servido de
   * nada.
   */
  gridRowOf(w: WidgetData): string {
    const widget = this.placementOf(w.widgetId);
    return widget ? `${widget.y + 1} / span ${widget.height}` : 'auto / span 4';
  }

  /** La colocación viene con el panel, no con los datos: son dos cosas distintas. */
  private placementOf(widgetId: string) {
    return this.layout().find(w => w.id === widgetId);
  }

  private readonly layout = signal<Placement[]>([]);

  // ── Mover y redimensionar ───────────────────────────────────────────────────────────────────
  //
  // Los recuadros se guardaban con su posición y había endpoint para cambiarla, pero en pantalla
  // sólo se podían quitar. Ahora cada uno tiene un asidero para moverlo y otro en la esquina para
  // cambiarle el tamaño, con el ratón o con el teclado (flechas). Sólo en el panel propio: el
  // servidor rechaza recolocar el de otra persona.

  readonly grid = viewChild<ElementRef<HTMLElement>>('grid');
  readonly layoutError = signal('');

  /** Si el panel es de quien lo mira: sólo entonces se puede recolocar. */
  readonly canArrange = computed(() => this.panel()?.isMine === true);

  private gesture: {
    id: string; mode: 'move' | 'resize'; startX: number; startY: number;
    origin: Placement; before: Placement[];
  } | null = null;

  private saveTimer: ReturnType<typeof setTimeout> | null = null;
  private savedLayout: Placement[] = [];

  moveLabel(w: WidgetData): string { return $localize`Mover «${w.title}:title:»`; }

  resizeLabel(w: WidgetData): string { return $localize`Cambiar el tamaño de «${w.title}:title:»`; }

  startGesture(event: PointerEvent, w: WidgetData, mode: 'move' | 'resize'): void {
    const origin = this.placementOf(w.widgetId);
    if (!origin || event.button !== 0) return;
    event.preventDefault();
    (event.target as HTMLElement).setPointerCapture?.(event.pointerId);
    this.gesture = { id: w.widgetId, mode, startX: event.clientX, startY: event.clientY, origin, before: this.layout() };
  }

  moveGesture(event: PointerEvent): void {
    const g = this.gesture;
    const grid = this.grid()?.nativeElement;
    if (!g || !grid) return;

    // El tamaño de una celda se lee de la rejilla tal como se pinta, con su separación: así el
    // arrastre sigue al ratón también en pantallas estrechas o con otro tamaño de letra.
    const style = getComputedStyle(grid);
    const gap = parseFloat(style.columnGap) || 0;
    const rowGap = parseFloat(style.rowGap) || 0;
    const columnWidth = (grid.clientWidth + gap) / 12;
    const rowHeight = (parseFloat(style.gridAutoRows) || 80) + rowGap;

    const dx = Math.round((event.clientX - g.startX) / columnWidth);
    const dy = Math.round((event.clientY - g.startY) / rowHeight);

    this.layout.set(g.mode === 'move'
      ? moveTo(g.before, g.id, g.origin.x + dx, g.origin.y + dy)
      : resizeTo(g.before, g.id, g.origin.width + dx, g.origin.height + dy));
  }

  endGesture(): void {
    const g = this.gesture;
    this.gesture = null;
    if (g && !sameLayout(g.before, this.layout())) void this.saveLayout(g.before);
  }

  /**
   * Con el teclado: en el asidero de mover, las flechas lo mueven una celda; en el de la esquina,
   * lo ensanchan o lo alargan. Se guarda al dejar de pulsar, no en cada tecla.
   */
  nudge(event: KeyboardEvent, w: WidgetData, mode: 'move' | 'resize'): void {
    const steps: Record<string, [number, number]> = {
      ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1],
    };
    const step = steps[event.key];
    const current = this.placementOf(w.widgetId);
    if (!step || !current) return;
    event.preventDefault();

    if (!this.saveTimer) this.savedLayout = this.layout();
    const [dx, dy] = step;
    this.layout.set(mode === 'move'
      ? moveTo(this.layout(), w.widgetId, current.x + dx, current.y + dy)
      : resizeTo(this.layout(), w.widgetId, current.width + dx, current.height + dy));

    if (this.saveTimer) clearTimeout(this.saveTimer);
    this.saveTimer = setTimeout(() => {
      this.saveTimer = null;
      if (!sameLayout(this.savedLayout, this.layout())) void this.saveLayout(this.savedLayout);
    }, 500);
  }

  /** Guarda la disposición entera. Si el servidor la rechaza, vuelve a como estaba y dice por qué. */
  private async saveLayout(previous: Placement[]): Promise<void> {
    const panel = this.panel();
    if (!panel) return;
    this.layoutError.set('');
    try {
      await firstValueFrom(this.api.put<void>(`/dashboards/${panel.id}/layout`, { widgets: this.layout() }, { silent: true }));
    } catch (e) {
      this.layout.set(previous);
      this.layoutError.set(errorMessage(e, $localize`No se pudo guardar la nueva colocación.`));
    }
  }

  loadAllData(): void {
    this.isLoading.set(true);
    this.api.get<KpiData>('/reports/kpi').subscribe({
      next: data => this.kpi.set(data),
      error: () => {},
      complete: () => this.isLoading.set(false)
    });

    this.api.get<TaskStatusBreakdown[]>('/reports/tasks/breakdown').subscribe({
      next: data => this.taskBreakdown.set(data),
      error: () => {}
    });

    this.api.get<ProjectProgress[]>('/reports/projects/progress').subscribe({
      next: data => {
        this.projectProgress.set(data);
        // Auto-select first project for burndown
        if (data.length > 0) {
          this.selectProject(data[0].id);
        }
      },
      error: () => {}
    });
  }

  selectProject(projectId: string): void {
    this.selectedProjectId.set(projectId);
    this.api.get<ProjectBurndown>(`/reports/projects/${projectId}/burndown`).subscribe({
      next: data => this.selectedProjectBurndown.set(data),
      error: () => {}
    });
  }
}
