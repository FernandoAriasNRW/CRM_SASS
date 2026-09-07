import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { ApiService } from '../../core/api.service';
import {
  CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent
} from '../../shared/ui/card.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideFolderKanban, lucideCheckSquare, lucideTicket, lucideTrendingUp,
  lucideClock, lucideActivity, lucidePieChart, lucideBarChart3,
  lucidePlus, lucideSettings, lucideLayout, lucideGlobe, lucideLock
} from '@ng-icons/lucide';
import { LineChartComponent, type BurndownDataPoint } from '../../shared/ui/charts/line-chart.component';
import { ProgressBarComponent } from '../../shared/ui/progress-bar.component';
import { DashboardsService, Dashboard } from '../../shared/services/dashboards.service';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  GraficaDeInformeComponent, type DatosDeWidget
} from '../../shared/ui/charts/grafica-de-informe.component';

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
    GraficaDeInformeComponent
],
  viewProviders: [provideIcons({
    lucideFolderKanban, lucideCheckSquare, lucideTicket, lucideTrendingUp,
    lucideClock, lucideActivity, lucidePieChart, lucideBarChart3,
    lucidePlus, lucideSettings, lucideLayout, lucideGlobe, lucideLock
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

  readonly panel = signal<{ id: string; titulo: string } | null>(null);
  readonly widgets = signal<DatosDeWidget[]>([]);
  readonly cargandoPanel = signal(true);

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
      { label: 'Proyectos', value: k?.totalProjects ?? '—', icon: 'lucideFolderKanban', sub: 'Total activos' },
      { label: 'Tareas', value: k?.totalTasks ?? '—', icon: 'lucideCheckSquare', sub: `${k?.doneTasks ?? 0} completadas` },
      { label: 'Tickets abiertos', value: k?.openTickets ?? '—', icon: 'lucideTicket', sub: `${k?.inProgressTickets ?? 0} en progreso` },
      { label: 'Throughput', value: k ? `${k.throughput}%` : '—', icon: 'lucideTrendingUp', sub: 'Tareas completadas' },
      // El guión ya estaba para «aún no ha llegado la respuesta»; ahora cubre también «el
      // servidor no tiene con qué calcularlo». Son dos cosas distintas para quien programa y
      // la misma para quien mira: no hay dato. Lo que no puede pasar es inventarse un 0,0d,
      // que se leería como «se entrega al instante».
      { label: 'Lead Time', value: k?.avgLeadTimeDays != null ? `${k.avgLeadTimeDays.toFixed(1)}d` : '—', icon: 'lucideClock', sub: 'Tiempo promedio' },
      { label: 'Cycle Time', value: k?.avgCycleTimeDays != null ? `${k.avgCycleTimeDays.toFixed(1)}d` : '—', icon: 'lucideActivity', sub: 'Ciclo promedio' },
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
    void this.cargarPanel();
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
  async cargarPanel(): Promise<void> {
    this.cargandoPanel.set(true);

    try {
      const mio = await firstValueFrom(
        this.api.get<{ id: string; titulo: string }>('/dashboards/mio'));

      this.panel.set(mio);
      this.disposicion.set((mio as unknown as { widgets: { id: string; x: number; y: number; ancho: number; alto: number }[] }).widgets ?? []);

      const datos = await firstValueFrom(
        this.api.get<DatosDeWidget[]>(`/dashboards/${mio.id}/datos`));

      this.widgets.set(datos);
    } finally {
      this.cargandoPanel.set(false);
    }
  }

  recargarPanel(): void { void this.cargarPanel(); }

  async quitarWidget(widgetId: string): Promise<void> {
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
  columnaDe(w: DatosDeWidget): string {
    const widget = this.colocacion(w.widgetId);
    return widget ? `${widget.x + 1} / span ${widget.ancho}` : 'auto / span 6';
  }

  filaDe(w: DatosDeWidget): string {
    const widget = this.colocacion(w.widgetId);
    return widget ? `span ${widget.alto}` : 'span 4';
  }

  /** La colocación viene con el panel, no con los datos: son dos cosas distintas. */
  private colocacion(widgetId: string) {
    return this.disposicion().find(w => w.id === widgetId);
  }

  private readonly disposicion = signal<
    { id: string; x: number; y: number; ancho: number; alto: number }[]>([]);

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
