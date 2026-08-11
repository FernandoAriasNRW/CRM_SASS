import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthSignalStore } from '../auth-signal.store';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthSignalStore);
  const token = authStore.getAccessToken();

  if (token) {
    const cloned = req.clone({
      headers: req.headers.set('Authorization', `Bearer ${token}`)
    });
    return next(cloned);
  }

  return next(req);
};