import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthSignalStore } from './auth-signal.store';
import { sinAvisoAutomatico } from './http-context';

/**
 * Opciones de una llamada suelta.
 *
 * `sinAviso` sirve para las peticiones cuyo error va a explicar quien las hace —junto al campo,
 * o con el nombre de lo que se revirtió—. Sin ella, el interceptor levanta además su propio
 * aviso y el mismo fallo se cuenta dos veces.
 */
export interface OpcionesDeLlamada {
  sinAviso?: boolean;
}

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

  /** Lo que se añade a una petición que se encarga de contar su propio error. */
  private contexto(opciones?: OpcionesDeLlamada) {
    return opciones?.sinAviso ? { context: sinAvisoAutomatico() } : {};
  }

  get<T>(path: string, params?: Record<string, string | number | boolean | Date | null | undefined>, opciones?: OpcionesDeLlamada): Observable<T> {
    const httpParams: Record<string, any> = {};
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
      withCredentials: true, // Important: sends cookies automatically
      ...this.contexto(opciones)
    });
  }

  post<T>(path: string, payload: unknown, opciones?: OpcionesDeLlamada): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, payload, {
      withCredentials: true, // Important: sends cookies automatically
      ...this.contexto(opciones)
    });
  }

  put<T>(path: string, payload: unknown, opciones?: OpcionesDeLlamada): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}${path}`, payload, {
      withCredentials: true, // Important: sends cookies automatically
      ...this.contexto(opciones)
    });
  }

  patch<T>(path: string, payload: unknown, opciones?: OpcionesDeLlamada): Observable<T> {
    return this.http.patch<T>(`${this.baseUrl}${path}`, payload, {
      withCredentials: true, // Important: sends cookies automatically
      ...this.contexto(opciones)
    });
  }

  delete<T>(path: string, opciones?: OpcionesDeLlamada): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`, {
      withCredentials: true, // Important: sends cookies automatically
      ...this.contexto(opciones)
    });
  }
}
