import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthSignalStore } from '../auth-signal.store';
import { ApiService } from '../api.service';
import { Router } from '@angular/router';
import { ToastService } from '../../shared/services/toast.service';

let isRefreshing = false;

interface RefreshResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthSignalStore);
  const api = inject(ApiService);
  const router = inject(Router);
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Handle 401 Unauthorized - attempt token refresh
      if (error.status === 401 && !req.url.includes('/auth/')) {
        if (!isRefreshing) {
          isRefreshing = true;

          // Call /auth/refresh - the refresh token cookie is sent automatically
          // No need to send refresh token in body - backend reads from cookie
          return api.post<RefreshResponse>('/auth/refresh', {}).pipe(
            switchMap((response) => {
              isRefreshing = false;

              // Update access token in memory
              const expiresAt = new Date(response.accessTokenExpiresAtUtc);
              authStore.setAccessToken(response.accessToken, expiresAt);

              // Retry the original request with new token
              const cloned = req.clone({
                headers: req.headers.set('Authorization', `Bearer ${response.accessToken}`)
              });
              return next(cloned);
            }),
            catchError((refreshError) => {
              isRefreshing = false;
              authStore.logout();
              toast.error('Sesión expirada', 'Por favor, inicia sesión nuevamente.');
              return throwError(() => refreshError);
            })
          );
        }
      }

      // Handle 403 Forbidden
      if (error.status === 403) {
        toast.warning('Acceso denegado', 'No tienes permisos para realizar esta acción.');
        router.navigate(['/']);
      }

      // Handle other errors - show toast with error message
      if (error.status !== 0 || error.error?.message) {
        toast.handleHttpError(error);
      }

      return throwError(() => error);
    })
  );
};
