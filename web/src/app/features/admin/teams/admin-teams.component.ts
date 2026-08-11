import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/api.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideUsers, lucidePlus, lucideTrash2, lucideEdit3, lucideShieldCheck,
  lucideSearch, lucideRefreshCw, lucideUserPlus, lucideX, lucideFolderKanban
} from '@ng-icons/lucide';
import { GranularPermissionsModalComponent } from '../permissions/granular-permissions-modal.component';
import { DrawerComponent } from '../../../shared/ui/drawer.component';
import { ClickableDirective } from '../../../shared/directives/clickable.directive';

export interface TeamMemberDto {
  userId: string;
  role: string;
  name?: string;
  email?: string;
}

export interface TeamDto {
  id: string;
  name: string;
  description: string;
  createdAtUtc: string;
  members: TeamMemberDto[];
}

export interface UserOptionDto {
  id: string;
  name: string;
  email: string;
}

@Component({
  selector: 'app-admin-teams',
  standalone: true,
  imports: [ClickableDirective, 
    CommonModule,
    FormsModule,
    NgIconComponent,
    GranularPermissionsModalComponent,
    DrawerComponent
  ],
  viewProviders: [
    provideIcons({
      lucideUsers, lucidePlus, lucideTrash2, lucideEdit3, lucideShieldCheck,
      lucideSearch, lucideRefreshCw, lucideUserPlus, lucideX, lucideFolderKanban
    })
  ],
  templateUrl: './admin-teams.component.html',
})
export class AdminTeamsComponent implements OnInit {
  private readonly api = inject(ApiService);

  teams = signal<TeamDto[]>([]);
  availableUsers = signal<UserOptionDto[]>([]);
  loading = signal(false);
  searchQuery = signal('');
  error = signal('');

  // Modals state
  showFormModal = signal(false);
  editingTeam = signal<TeamDto | null>(null);
  showPermissionsModal = signal(false);
  selectedTeamForPermissions = signal<TeamDto | null>(null);

  // Form fields
  formName = '';
  formDescription = '';
  selectedMemberIds = signal<string[]>([]);

  filteredTeams = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return this.teams();
    return this.teams().filter(t =>
      t.name.toLowerCase().includes(q) || (t.description || '').toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set('');

    this.api.get<TeamDto[]>('/teams').subscribe({
      next: (teamsData) => {
        this.teams.set(teamsData || []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Error al cargar equipos');
        this.loading.set(false);
      }
    });

    this.api.get<UserOptionDto[]>('/users').subscribe({
      next: (usersData) => {
        this.availableUsers.set(usersData || []);
      }
    });
  }

  openCreateModal(): void {
    this.editingTeam.set(null);
    this.formName = '';
    this.formDescription = '';
    this.selectedMemberIds.set([]);
    this.showFormModal.set(true);
  }

  openEditModal(team: TeamDto): void {
    this.editingTeam.set(team);
    this.formName = team.name;
    this.formDescription = team.description || '';
    this.selectedMemberIds.set(team.members ? team.members.map(m => m.userId) : []);
    this.showFormModal.set(true);
  }

  closeFormModal(): void {
    this.showFormModal.set(false);
    this.editingTeam.set(null);
  }

  toggleMemberSelection(userId: string): void {
    this.selectedMemberIds.update(ids => {
      if (ids.includes(userId)) return ids.filter(id => id !== userId);
      return [...ids, userId];
    });
  }

  saveTeam(): void {
    if (!this.formName.trim()) {
      alert('El nombre del equipo es requerido');
      return;
    }

    const payload = {
      name: this.formName,
      description: this.formDescription,
      memberIds: this.selectedMemberIds()
    };

    if (this.editingTeam()) {
      this.api.put(`/teams/${this.editingTeam()!.id}`, payload).subscribe({
        next: () => {
          this.closeFormModal();
          this.loadData();
        },
        error: (err) => alert(err?.error || 'Error al actualizar equipo')
      });
    } else {
      this.api.post('/teams', payload).subscribe({
        next: () => {
          this.closeFormModal();
          this.loadData();
        },
        error: (err) => alert(err?.error || 'Error al crear equipo')
      });
    }
  }

  deleteTeam(team: TeamDto): void {
    if (!confirm(`¿Estás seguro de eliminar el equipo "${team.name}"?`)) return;
    this.api.delete(`/teams/${team.id}`).subscribe({
      next: () => {
        this.teams.update(list => list.filter(t => t.id !== team.id));
      },
      error: (err) => alert(err?.error || 'Error al eliminar equipo')
    });
  }

  openPermissionsModal(team: TeamDto): void {
    this.selectedTeamForPermissions.set(team);
    this.showPermissionsModal.set(true);
  }

  closePermissionsModal(): void {
    this.showPermissionsModal.set(false);
    this.selectedTeamForPermissions.set(null);
  }
}
