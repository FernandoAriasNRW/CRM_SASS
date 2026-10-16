import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthSignalStore } from '../auth-signal.store';
import { ApiService } from '../api.service';
import { ToastService } from '../../shared/services/toast.service';
import { SKIP_GLOBAL_ERROR_TOAST } from '../http-context';

let isRefreshing = false;

interface RefreshResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthSignalStore);
  const api = inject(ApiService);
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
              toast.error($localize`Sesión expirada`, 'Por favor, inicia sesión nuevamente.');
              return throwError(() => refreshError);
            })
          );
        }
      }

      // Un 403 no saca de la pantalla: antes llevaba siempre a Inicio, y que te negaran un solo
      // campo, o una carga en segundo plano, tiraba lo que tuvieras a medias. Las pantallas ya
      // las protegen los guardas de ruta. Se avisa una vez, como cualquier otro fallo: antes
      // salían dos avisos, y el de 403 ni respetaba a quien se reservaba explicarlo.

      // Handle other errors - show toast with error message.
      //
      // Salvo que quien hizo la petición se haya reservado explicar el fallo. Antes se avisaba
      // siempre, así que una pantalla que pone el mensaje junto al campo que lo provocó
      // levantaba además este aviso con el mismo texto: dos avisos para un solo fallo.
      const callerExplains = req.context.get(SKIP_GLOBAL_ERROR_TOAST);

      if (!callerExplains && (error.status !== 0 || error.error?.message)) {
        toast.handleHttpError(error);
      }

      return throwError(() => error);
    })
  );
};
