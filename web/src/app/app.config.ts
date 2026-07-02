import { ApplicationConfig, isDevMode, APP_INITIALIZER } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { routes } from './app.routes';
import { provideServiceWorker } from '@angular/service-worker';
import { authReducer } from './state/auth.state';
import { notificationsReducer } from './state/notifications/notifications.state';
import { projectsReducer } from './state/projects/projects.state';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { ApiService } from './core/api.service';
import { AuthSignalStore } from './core/auth-signal.store';
import { catchError, map, of, tap } from 'rxjs';

export function initializeApp(api: ApiService, auth: AuthSignalStore) {
  return () => {
    if (auth.userInfo()) {
      return api.post<{ accessToken: string, expiresAt: string }>('/auth/refresh', {}).pipe(
        tap(res => auth.setAccessToken(res.accessToken, new Date(res.expiresAt))),
        catchError(() => {
          auth.clearTokens();
          return of(null);
        })
      );
    }
    return of(null);
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(
      withInterceptors([authInterceptor, errorInterceptor])
    ),
    provideStore({
      auth: authReducer,
      notifications: notificationsReducer,
      projects: projectsReducer,
    }),
    provideEffects([]),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeApp,
      deps: [ApiService, AuthSignalStore],
      multi: true
    },
  ],
};