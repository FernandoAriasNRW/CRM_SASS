import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthSignalStore } from '../../core/auth-signal.store';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { LabelComponent } from '../../shared/ui/label.component';
import {
  CardComponent, CardHeaderComponent, CardTitleComponent,
  CardDescriptionComponent, CardContentComponent, CardFooterComponent
} from '../../shared/ui/card.component';

interface LoginResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    FormsModule, ButtonComponent, InputComponent, LabelComponent,
    CardComponent, CardHeaderComponent, CardTitleComponent,
    CardDescriptionComponent, CardContentComponent, CardFooterComponent
  ],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  email = 'admin@acme.com';
  password = 'admin123';
  error = signal('');
  loading = signal(false);

  private readonly api = inject(ApiService);
  private readonly authStore = inject(AuthSignalStore);
  private readonly router = inject(Router);

  login(): void {
    this.loading.set(true);
    this.error.set('');

    this.api.post<LoginResponse>('/auth/login', { email: this.email, password: this.password })
      .subscribe({
        next: (res) => {
          const expiresAt = new Date(res.accessTokenExpiresAtUtc);
          this.authStore.setAccessToken(res.accessToken, expiresAt);

          this.api.get<{ id: string; name: string; email: string; tenantId: string; role: string }>('/auth/users/me')
            .subscribe({
              next: (user) => {
                this.authStore.setUserInfo(user);
                this.router.navigateByUrl('/');
              },
              error: () => {
                this.router.navigateByUrl('/');
              }
            });
        },
        error: () => {
          this.error.set('Credenciales inválidas');
          this.loading.set(false);
        },
      });
  }
}
