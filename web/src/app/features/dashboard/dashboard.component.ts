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
import { DoughnutChartComponent } from '../../shared/ui/charts/doughnut-chart.component';
import { LineChartComponent, type BurndownDataPoint } from '../../shared/ui/charts/line-chart.component';
import { ProgressBarComponent } from '../../shared/ui/progress-bar.component';
import { DashboardsService, Dashboard } from '../../shared/services/dashboards.service';
import { ActivatedRoute } from '@angular/router';

interface KpiData {
  totalProjects: number;
  totalTasks: number;
  doneTasks: number;
  throughput: number;
  openTickets: number;
  inProgressTickets: number;
  avgLeadTimeDays: number;
  avgCycleTimeDays: number;
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
  data: Array<{ date: string; remainingTasks: number; idealTasks: number }>;
}

import { AuthSignalStore } from '../../core/auth-signal.store';
import { HierarchySignalStore } from '../../core/hierarchy-signal.store';

import { SlicePipe, UpperCasePipe, CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '../../shared/ui/button.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonComponent,
    CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent,
    NgIconComponent, DoughnutChartComponent, LineChartComponent, ProgressBarComponent
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

  // Dashboards feature
  readonly customDashboards = signal<Dashboard[]>([]);
  readonly selectedDashboard = signal<Dashboard | null>(null);
  readonly viewType = signal<'default' | 'private' | 'public' | 'all'>('default');
  
  // Create Modal
  showCreateModal = false;
  newDashboardTitle = '';
  newDashboardIsPublic = false;

  readonly doughnutData = computed(() => {
    const breakdown = this.taskBreakdown();
    if (breakdown.length === 0) return null;
    return {
      labels: breakdown.map(b => b.status),
      values: breakdown.map(b => b.count),
      colors: breakdown.map(b => b.color)
    };
  });

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
      { label: 'Lead Time', value: k ? `${k.avgLeadTimeDays.toFixed(1)}d` : '—', icon: 'lucideClock', sub: 'Tiempo promedio' },
      { label: 'Cycle Time', value: k ? `${k.avgCycleTimeDays.toFixed(1)}d` : '—', icon: 'lucideActivity', sub: 'Ciclo promedio' },
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
