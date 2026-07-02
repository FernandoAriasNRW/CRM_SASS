import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthSignalStore } from './auth-signal.store';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly baseUrl = 'http://localhost:8080/api/v1';

  constructor(
    private readonly http: HttpClient,
    private readonly authStore: AuthSignalStore
  ) {}

  /**
   * Sets the access token in the auth store.
   * Refresh token is handled via httpOnly cookie (not here).
   */
  setAccessToken(accessToken: string, expiresAt: Date): void {
    this.authStore.setAccessToken(accessToken, expiresAt);
  }

  /**
   * Gets the access token from the auth store.
   */
  private getAccessToken(): string | null {
    return this.authStore.getAccessToken();
  }

  get<T>(path: string, params?: Record<string, string | number | boolean | Date | null | undefined>): Observable<T> {
    let httpParams: Record<string, any> = {};
    if (params) {
      Object.keys(params).forEach(key => {
        const val = params[key];
        if (val !== null && val !== undefined && val !== '') {
          httpParams[key] = val instanceof Date ? val.toISOString() : val;
        }
      });
    }

    return this.http.get<T>(`${this.baseUrl}${path}`, {
      params: httpParams,
      withCredentials: true // Important: sends cookies automatically
    });
  }

  post<T>(path: string, payload: unknown): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, payload, {
      withCredentials: true // Important: sends cookies automatically
    });
  }

  put<T>(path: string, payload: unknown): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}${path}`, payload, {
      withCredentials: true // Important: sends cookies automatically
    });
  }

  patch<T>(path: string, payload: unknown): Observable<T> {
    return this.http.patch<T>(`${this.baseUrl}${path}`, payload, {
      withCredentials: true // Important: sends cookies automatically
    });
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`, {
      withCredentials: true // Important: sends cookies automatically
    });
  }

  // private getHeaders(): HttpHeaders {
  //   const token = this.getAccessToken();
  //   return token
  //     ? new HttpHeaders({ Authorization: `Bearer ${token}` })
  //     : new HttpHeaders();
  // }
}
