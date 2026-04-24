import { Component, inject, OnInit, signal } from '@angular/core';
import { AsyncPipe, DecimalPipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import {
  CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent
} from '../../shared/ui/card.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideFolderKanban, lucideCheckSquare, lucideTicket,
  lucideTrendingUp, lucideClock, lucideRefreshCw
} from '@ng-icons/lucide';
import { ButtonComponent } from '../../shared/ui/button.component';

interface KpiDto {
  totalProjects: number;
  totalTasks: number;
  doneTasks: number;
  throughput: number;
  openTickets: number;
  inProgressTickets: number;
  avgLeadTimeDays: number;
  avgCycleTimeDays: number;
}

interface TaskBreakdown { status: string; count: number; }

interface ProjectProgress {
  id: string; name: string; status: string;
  totalTasks: number; doneTasks: number; completionPct: number;
}

const STATUS_BADGE: Record<string, BadgeVariant> = {
  'Planned': 'secondary', 'In Progress': 'default', 'Done': 'success', 'On Hold': 'warning'
};

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent,
    BadgeComponent, ButtonComponent, NgIconComponent, DecimalPipe
  ],
  viewProviders: [provideIcons({
    lucideFolderKanban, lucideCheckSquare, lucideTicket,
    lucideTrendingUp, lucideClock, lucideRefreshCw
  })],
  templateUrl: './reports.component.html',
})
export class ReportsComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly kpi = signal<KpiDto | null>(null);
  readonly breakdown = signal<TaskBreakdown[]>([]);
  readonly progress = signal<ProjectProgress[]>([]);

  totalBreakdown = () => this.breakdown().reduce((sum, b) => sum + b.count, 0);

  readonly kpiCards = () => {
    const k = this.kpi();
    return [
      { label: 'Proyectos',        value: k?.totalProjects ?? '—',      icon: 'lucideFolderKanban', sub: 'Total' },
      { label: 'Tareas',           value: k?.totalTasks ?? '—',         icon: 'lucideCheckSquare',  sub: `${k?.doneTasks ?? 0} completadas` },
      { label: 'Throughput',       value: k ? `${k.throughput}%` : '—', icon: 'lucideTrendingUp',   sub: 'Tareas completadas' },
      { label: 'Tickets abiertos', value: k?.openTickets ?? '—',        icon: 'lucideTicket',       sub: `${k?.inProgressTickets ?? 0} en progreso` },
      { label: 'Lead Time',        value: k ? `${k.avgLeadTimeDays}d` : '—', icon: 'lucideClock',  sub: 'Promedio días' },
      { label: 'Cycle Time',       value: k ? `${k.avgCycleTimeDays}d` : '—', icon: 'lucideClock', sub: 'Promedio días trabajo' },
    ];
  };

  statusBadge(status: string): BadgeVariant {
    return STATUS_BADGE[status] ?? 'outline';
  }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<KpiDto>('/reports/kpi').subscribe({ next: d => this.kpi.set(d), error: () => {} });
    this.api.get<TaskBreakdown[]>('/reports/tasks/breakdown').subscribe({ next: d => this.breakdown.set(d), error: () => {} });
    this.api.get<ProjectProgress[]>('/reports/projects/progress').subscribe({ next: d => this.progress.set(d), error: () => {} });
  }
}
