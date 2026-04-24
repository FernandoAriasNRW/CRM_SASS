import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthSignalStore } from '../../core/auth-signal.store';
import { ApiService } from '../../core/api.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideSave, lucideLogOut, lucideLock, lucideUser } from '@ng-icons/lucide';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    FormsModule,
    NgIconComponent
  ],
  viewProviders: [provideIcons({ lucideSave, lucideLogOut, lucideLock, lucideUser })],
  templateUrl: './profile.component.html',
})
export class ProfileComponent implements OnInit {
  readonly authStore = inject(AuthSignalStore);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  // User info
  readonly user = this.authStore.userInfo;

  // Password change
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  passwordError = signal('');
  passwordSuccess = signal('');
  savingPassword = signal(false);

  // General error
  error = signal('');

  ngOnInit(): void {
    if (!this.authStore.isAuthenticated()) {
      this.router.navigate(['/login']);
    }
  }

  changePassword(): void {
    this.passwordError.set('');
    this.passwordSuccess.set('');

    if (!this.currentPassword || !this.newPassword || !this.confirmPassword) {
      this.passwordError.set('Todos los campos son requeridos');
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.passwordError.set('Las contraseñas no coinciden');
      return;
    }

    if (this.newPassword.length < 6) {
      this.passwordError.set('La nueva contraseña debe tener al menos 6 caracteres');
      return;
    }

    this.savingPassword.set(true);

    this.api.put<{ message: string }>('/profile/password', {
      currentPassword: this.currentPassword,
      newPassword: this.newPassword
    }).subscribe({
      next: () => {
        this.passwordSuccess.set('Contraseña cambiada exitosamente');
        this.currentPassword = '';
        this.newPassword = '';
        this.confirmPassword = '';
        this.savingPassword.set(false);
      },
      error: (err) => {
        this.passwordError.set(err.error?.error || 'Error al cambiar contraseña');
        this.savingPassword.set(false);
      }
    });
  }

  logout(): void {
    // Refresh token is sent via httpOnly cookie automatically
    this.api.post('/auth/logout', {}).subscribe({
      next: () => this.authStore.logout(),
      error: () => this.authStore.logout()
    });
  }
}
