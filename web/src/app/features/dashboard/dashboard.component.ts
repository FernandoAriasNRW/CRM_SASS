import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { ApiService } from '../../core/api.service';
import {
  CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent
} from '../../shared/ui/card.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideFolderKanban, lucideCheckSquare, lucideTicket, lucideTrendingUp,
  lucideClock, lucideActivity, lucidePieChart, lucideBarChart3
} from '@ng-icons/lucide';
import { DoughnutChartComponent } from '../../shared/ui/charts/doughnut-chart.component';
import { LineChartComponent, type BurndownDataPoint } from '../../shared/ui/charts/line-chart.component';
import { ProgressBarComponent } from '../../shared/ui/progress-bar.component';

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

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent,
    NgIconComponent, DoughnutChartComponent, LineChartComponent, ProgressBarComponent
  ],
  viewProviders: [provideIcons({
    lucideFolderKanban, lucideCheckSquare, lucideTicket, lucideTrendingUp,
    lucideClock, lucideActivity, lucidePieChart, lucideBarChart3
  })],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly kpi = signal<KpiData | null>(null);
  readonly taskBreakdown = signal<TaskStatusBreakdown[]>([]);
  readonly projectProgress = signal<ProjectProgress[]>([]);
  readonly selectedProjectBurndown = signal<ProjectBurndown | null>(null);
  readonly selectedProjectId = signal<string | null>(null);
  readonly isLoading = signal(true);

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
    this.loadAllData();
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
