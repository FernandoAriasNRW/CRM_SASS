import { Injectable, effect, inject } from '@angular/core';
import { AuthSignalStore } from './auth-signal.store';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class SessionManagerService {
  private readonly authStore = inject(AuthSignalStore);
  private readonly api = inject(ApiService);
  
  private refreshTimeoutId: any = null;

  constructor() {
    effect(() => {
      const expiresAt = this.authStore.getTokenExpiresAt();
      this.clearTimeout();

      if (expiresAt && this.authStore.isAuthenticated()) {
        const timeUntilExpiry = expiresAt.getTime() - new Date().getTime();
        
        // Refresh 1 minute before expiration
        const refreshDelay = Math.max(0, timeUntilExpiry - 60000);

        this.refreshTimeoutId = setTimeout(() => {
          this.refreshSession();
        }, refreshDelay);
      }
    });
  }

  private clearTimeout() {
    if (this.refreshTimeoutId) {
      clearTimeout(this.refreshTimeoutId);
      this.refreshTimeoutId = null;
    }
  }

  private refreshSession() {
    this.api.post<{ accessToken: string, accessTokenExpiresAtUtc: string }>('/auth/refresh', {}).subscribe({
      next: (res) => {
        this.authStore.setAccessToken(res.accessToken, new Date(res.accessTokenExpiresAtUtc));
      },
      error: () => {
        // If refresh fails, it means the session (refresh token) has completely expired
        this.authStore.logout();
      }
    });
  }
}
