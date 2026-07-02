import { Component, inject, OnInit, signal, computed, ViewChild, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../../core/api.service';
import { AuthSignalStore } from '../../../core/auth-signal.store';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideTrash2, lucideEdit, lucideUsers, lucideX, lucideDatabase, lucideRefreshCw } from '@ng-icons/lucide';

import { DataTableComponent, ColumnDef, TableState } from '../../../shared/ui/data-table/data-table.component';
import { AdvancedFiltersComponent, FilterField } from '../../../shared/ui/data-table/advanced-filters.component';
import { ViewsService, SavedView } from '../../../shared/services/views.service';
import { TableColumnService } from '../../../shared/services/table-column.service';

interface UserDto {
  id: string;
  name: string;
  email: string;
  tenantId: string;
  role: string;
}

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NgIconComponent,
    DataTableComponent,
    AdvancedFiltersComponent
  ],
  viewProviders: [provideIcons({ lucidePlus, lucideTrash2, lucideEdit, lucideUsers, lucideX, lucideDatabase, lucideRefreshCw })],
  templateUrl: './admin-users.component.html',
})
export class AdminUsersComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly authStore = inject(AuthSignalStore);
  private readonly router = inject(Router);
  private readonly viewsService = inject(ViewsService);
  private readonly columnService = inject(TableColumnService);

  readonly users = signal<UserDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly showModal = signal(false);
  readonly editingUser = signal<UserDto | null>(null);
  readonly isSeeding = signal(false);

  // Table & Views state
  tableState = signal<TableState>({ page: 1, pageSize: 25, sortDirection: 'asc' });
  savedViews = signal<SavedView[]>([]);
  activeViewId = signal<string | null>(null);

  tableColumns: ColumnDef[] = this.columnService.buildColumns<UserDto>({
    name: { label: 'Nombre', type: 'custom' },
    email: { label: 'Email' },
    role: { label: 'Rol', type: 'custom' }
  }, [
    { key: 'actions', label: 'Acciones', sortable: false, type: 'custom' }
  ]);

  filterFields = computed<FilterField[]>(() => [
    { key: 'role', label: 'Role', type: 'select', options: ['Admin', 'Member'].map(r => ({ label: r, value: r })) }
  ]);

  @ViewChild('nameTemplate', { static: true }) nameTemplate!: TemplateRef<any>;
  @ViewChild('roleTemplate', { static: true }) roleTemplate!: TemplateRef<any>;
  @ViewChild('actionsTemplate', { static: true }) actionsTemplate!: TemplateRef<any>;

  filteredUsers = computed(() => {
    let list = this.users();
    const st = this.tableState();
    if (st.searchTerm) {
      const q = st.searchTerm.toLowerCase();
      list = list.filter(u => u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q));
    }
    if (st.filters && st.filters['role']) {
      const roleFilter = st.filters['role'];
      list = list.filter(u => u.role === roleFilter);
    }
    return list;
  });

  // Form fields
  formName = '';
  formEmail = '';
  formPassword = '';
  formRole = 'Member';

  ngOnInit(): void {
    if (!this.authStore.isAuthenticated() || !this.authStore.isAdmin()) {
      this.router.navigate(['/']);
      return;
    }
    this.tableColumns.find(c => c.key === 'name')!.template = this.nameTemplate;
    this.tableColumns.find(c => c.key === 'role')!.template = this.roleTemplate;
    this.tableColumns.find(c => c.key === 'actions')!.template = this.actionsTemplate;
    
    this.loadViews();
    this.loadUsers();
  }

  loadViews(): void {
    this.viewsService.getViews('Users').subscribe({
      next: (views) => {
        this.savedViews.set(views);
        const defaultView = views.find(v => v.isDefault);
        if (defaultView) this.applySavedView(defaultView);
      }
    });
  }

  saveCurrentView(name: string, isDefault: boolean = false): void {
    const payload = { moduleName: 'Users', viewName: name, stateJson: JSON.stringify(this.tableState()), isDefault };
    this.viewsService.saveView(payload).subscribe({
      next: (view) => {
        this.savedViews.update(views => [...views, view]);
        this.activeViewId.set(view.id);
      }
    });
  }

  applySavedView(view: SavedView): void {
    this.activeViewId.set(view.id);
    try {
      const state = JSON.parse(view.stateJson) as TableState;
      this.tableState.set(state);
    } catch (e) {
      console.error('Failed to parse view', e);
    }
  }

  onTableStateChange(state: TableState): void {
    this.tableState.set(state);
  }

  onFiltersChange(filters: Record<string, any>): void {
    this.tableState.update(s => ({ ...s, filters, page: 1 }));
  }

  loadUsers(): void {
    this.loading.set(true);
    // Fake endpoint returning me. Imagine a real GET /users
    this.api.get<{ id: string; name: string; email: string; tenantId: string; role: string }>('/auth/users/me')
      .subscribe({
        next: (user) => {
          this.users.set([user]);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Error al cargar usuarios');
          this.loading.set(false);
        }
      });
  }

  openCreateModal(): void {
    this.editingUser.set(null);
    this.formName = '';
    this.formEmail = '';
    this.formPassword = '';
    this.formRole = 'Member';
    this.showModal.set(true);
  }

  openEditModal(user: UserDto): void {
    this.editingUser.set(user);
    this.formName = user.name;
    this.formEmail = user.email;
    this.formPassword = '';
    this.formRole = user.role;
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.editingUser.set(null);
  }

  saveUser(): void {
    if (!this.formName || !this.formEmail) {
      this.error.set('Nombre y email son requeridos');
      return;
    }
    const isEditing = this.editingUser() !== null;
    if (isEditing) {
      this.api.put<UserDto>(`/users/${this.editingUser()!.id}`, {
        name: this.formName, email: this.formEmail, role: this.formRole
      }).subscribe({
        next: (updated) => {
          this.users.update(users => users.map(u => u.id === updated.id ? updated : u));
          this.closeModal();
        },
        error: (err) => this.error.set(err.error?.error || 'Error al actualizar usuario')
      });
    } else {
      if (!this.formPassword || this.formPassword.length < 6) {
        this.error.set('Contraseña debe tener al menos 6 caracteres');
        return;
      }
      this.api.post<UserDto>('/users', {
        name: this.formName, email: this.formEmail, password: this.formPassword, role: this.formRole
      }).subscribe({
        next: (newUser) => {
          this.users.update(users => [...users, newUser]);
          this.closeModal();
        },
        error: (err) => this.error.set(err.error?.error || 'Error al crear usuario')
      });
    }
  }

  deleteUser(userId: string): void {
    if (!confirm('¿Estás seguro de eliminar este usuario?')) return;
    this.api.delete(`/users/${userId}`).subscribe({
      next: () => this.users.update(users => users.filter(u => u.id !== userId)),
      error: () => this.error.set('Error al eliminar usuario')
    });
  }

  seedDatabase(): void {
    if (!confirm('¿Estás seguro de generar data falsa? Esto añadirá usuarios, equipos, proyectos y tareas aleatorias a la base de datos.')) return;
    this.isSeeding.set(true);
    this.api.post('/admin/seed-database', {}).subscribe({
      next: () => {
        this.isSeeding.set(false);
        alert('Data falsa generada correctamente');
        this.loadUsers();
      },
      error: (err) => {
        this.isSeeding.set(false);
        this.error.set(err.error?.error || 'Error al generar data');
      }
    });
  }
}

