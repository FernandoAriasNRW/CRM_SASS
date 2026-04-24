import { Component, inject, OnInit, signal } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { Store } from '@ngrx/store';
import { ApiService } from '../../core/api.service';
import { ProjectCreateModalComponent } from './project-create-modal.component';
import { ProjectDetailModalComponent } from './project-detail-modal.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideRefreshCw, lucidePlus, lucideEye } from '@ng-icons/lucide';
import {
  projectsLoaded, selectProjects, selectProjectsLoaded, type Project
} from '../../state/projects/projects.state';

const STATUS_VARIANT: Record<string, string> = {
  'Planned':     'bg-gray-100 text-gray-800',
  'In Progress': 'bg-blue-100 text-blue-800',
  'Done':        'bg-green-100 text-green-800',
  'On Hold':     'bg-yellow-100 text-yellow-800',
};

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [NgIconComponent, ProjectCreateModalComponent, ProjectDetailModalComponent, AsyncPipe],
  viewProviders: [provideIcons({ lucideRefreshCw, lucidePlus, lucideEye })],
  templateUrl: './projects.component.html',
})
export class ProjectsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly store = inject(Store);

  readonly projects$ = this.store.select(selectProjects);
  readonly loaded$ = this.store.select(selectProjectsLoaded);

  readonly showCreateModal = signal(false);
  readonly selectedProject = signal<Project | null>(null);

  statusStyle(status: string): string {
    return STATUS_VARIANT[status] ?? 'bg-gray-100 text-gray-800';
  }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<Project[]>('/projects').subscribe({
      next: items => this.store.dispatch(projectsLoaded({ items })),
      error: () => {},
    });
  }

  openCreateModal(): void {
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  openDetailModal(project: Project): void {
    this.selectedProject.set(project);
  }

  closeDetailModal(): void {
    this.selectedProject.set(null);
  }
}
