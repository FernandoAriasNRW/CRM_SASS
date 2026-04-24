import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../../core/api.service';
import { AuthSignalStore } from '../../../core/auth-signal.store';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideTrash2, lucideEdit, lucideUsers, lucideX } from '@ng-icons/lucide';

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
    FormsModule,
    NgIconComponent
  ],
  viewProviders: [provideIcons({ lucidePlus, lucideTrash2, lucideEdit, lucideUsers, lucideX })],
  templateUrl: './admin-users.component.html',
})
export class AdminUsersComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly authStore = inject(AuthSignalStore);
  private readonly router = inject(Router);

  readonly users = signal<UserDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly showModal = signal(false);
  readonly editingUser = signal<UserDto | null>(null);

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
    this.loadUsers();
  }

  loadUsers(): void {
    this.loading.set(true);
    // For now, we'll just fetch from /auth/users/me to get current user
    // In a real implementation, there would be a GET /users endpoint
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
      // Update existing user
      this.api.put<UserDto>(`/users/${this.editingUser()!.id}`, {
        name: this.formName,
        email: this.formEmail,
        role: this.formRole
      }).subscribe({
        next: (updated) => {
          this.users.update(users => users.map(u => u.id === updated.id ? updated : u));
          this.closeModal();
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Error al actualizar usuario');
        }
      });
    } else {
      // Create new user
      if (!this.formPassword || this.formPassword.length < 6) {
        this.error.set('Contraseña debe tener al menos 6 caracteres');
        return;
      }

      this.api.post<UserDto>('/users', {
        name: this.formName,
        email: this.formEmail,
        password: this.formPassword,
        role: this.formRole
      }).subscribe({
        next: (newUser) => {
          this.users.update(users => [...users, newUser]);
          this.closeModal();
        },
        error: (err) => {
          this.error.set(err.error?.error || 'Error al crear usuario');
        }
      });
    }
  }

  deleteUser(userId: string): void {
    if (!confirm('¿Estás seguro de eliminar este usuario?')) return;

    this.api.delete(`/users/${userId}`).subscribe({
      next: () => {
        this.users.update(users => users.filter(u => u.id !== userId));
      },
      error: () => {
        this.error.set('Error al eliminar usuario');
      }
    });
  }
}
