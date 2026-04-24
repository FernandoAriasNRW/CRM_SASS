import { Injectable, signal, computed } from '@angular/core';
import { Router } from '@angular/router';

export interface UserInfo {
  id: string;
  name: string;
  email: string;
  tenantId: string;
  role: string;
}

@Injectable({ providedIn: 'root' })
export class AuthSignalStore {
  // Access token ONLY in memory (never persisted - security best practice)
  private readonly _accessToken = signal<string | null>(null);
  private readonly _tokenExpiresAt = signal<Date | null>(null);

  // User info (persisted in localStorage for session restoration)
  readonly userInfo = signal<UserInfo | null>(this.loadUserInfo());

  // Computed states
  readonly isAuthenticated = computed(() => !!this._accessToken());
  readonly isAdmin = computed(() => this.userInfo()?.role === 'Admin');
  readonly isTokenExpired = computed(() => {
    const expires = this._tokenExpiresAt();
    if (!expires) return true;
    return new Date() >= expires;
  });

  constructor(private router: Router) {}

  private loadUserInfo(): UserInfo | null {
    const stored = localStorage.getItem('crm_user');
    return stored ? JSON.parse(stored) : null;
  }

  /**
   * Sets the access token in memory only (NOT localStorage for security).
   * Refresh token is handled via httpOnly cookie by the backend.
   */
  setAccessToken(accessToken: string, expiresAt: Date): void {
    this._accessToken.set(accessToken);
    this._tokenExpiresAt.set(expiresAt);
  }

  /**
   * Clears all auth state from memory and localStorage.
   * Refresh token cookie is cleared by the backend on logout.
   */
  clearTokens(): void {
    this._accessToken.set(null);
    this._tokenExpiresAt.set(null);
    this.userInfo.set(null);

    localStorage.removeItem('crm_user');
  }

  /**
   * Gets the current access token from memory.
   */
  getAccessToken(): string | null {
    return this._accessToken();
  }

  /**
   * Gets the token expiration date.
   */
  getTokenExpiresAt(): Date | null {
    return this._tokenExpiresAt();
  }

  /**
   * Sets user info and persists to localStorage for session restoration.
   */
  setUserInfo(info: UserInfo): void {
    this.userInfo.set(info);
    localStorage.setItem('crm_user', JSON.stringify(info));
  }

  /**
   * Updates user info partially.
   */
  updateUserInfo(changes: Partial<UserInfo>): void {
    const current = this.userInfo();
    if (current) {
      const updated = { ...current, ...changes };
      this.userInfo.set(updated);
      localStorage.setItem('crm_user', JSON.stringify(updated));
    }
  }

  /**
   * Logs out the user by clearing local state.
   * The backend will clear the refresh token cookie.
   */
  logout(): void {
    this.clearTokens();
    this.router.navigate(['/login']);
  }
}
