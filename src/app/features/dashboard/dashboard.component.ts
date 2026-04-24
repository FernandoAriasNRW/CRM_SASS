import { Component, inject, OnInit, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import {
  CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent
} from '../../shared/ui/card.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideFolderKanban, lucideCheckSquare, lucideTicket, lucideTrendingUp
} from '@ng-icons/lucide';

interface KpiData {
  totalProjects: number;
  totalTasks: number;
  doneTasks: number;
  throughput: number;
  openTickets: number;
  inProgressTickets: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CardComponent, CardHeaderComponent, CardTitleComponent, CardContentComponent, NgIconComponent],
  viewProviders: [provideIcons({ lucideFolderKanban, lucideCheckSquare, lucideTicket, lucideTrendingUp })],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly kpi = signal<KpiData | null>(null);

  readonly kpiCards = () => {
    const k = this.kpi();
    return [
      { label: 'Proyectos',        value: k?.totalProjects ?? '—',      icon: 'lucideFolderKanban', sub: 'Total activos' },
      { label: 'Tareas',           value: k?.totalTasks ?? '—',         icon: 'lucideCheckSquare',  sub: `${k?.doneTasks ?? 0} completadas` },
      { label: 'Tickets abiertos', value: k?.openTickets ?? '—',        icon: 'lucideTicket',       sub: `${k?.inProgressTickets ?? 0} en progreso` },
      { label: 'Throughput',       value: k ? `${k.throughput}%` : '—', icon: 'lucideTrendingUp',   sub: 'Tareas completadas' },
    ];
  };

  ngOnInit(): void {
    this.api.get<KpiData>('/reports/kpi').subscribe({
      next: data => this.kpi.set(data),
      error: () => {},
    });
  }
}
