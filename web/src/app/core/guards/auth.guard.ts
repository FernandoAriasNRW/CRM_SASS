import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSignalStore } from '../auth-signal.store';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthSignalStore);
  const router = inject(Router);

  if (auth.isAuthenticated()) return true;
  return router.createUrlTree(['/login']);
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthSignalStore);
  const router = inject(Router);

  if (auth.isAuthenticated() && auth.isAdmin()) return true;
  if (!auth.isAuthenticated()) return router.createUrlTree(['/login']);
  return router.createUrlTree(['/']);
};

export const guestOrAuthGuard: CanActivateFn = () => {
  // Allow access to both guests (for public ticket form) and authenticated users
  const auth = inject(AuthSignalStore);
  const router = inject(Router);

  // If authenticated, allow
  if (auth.isAuthenticated()) return true;

  // For public routes, allow guests (they'll use guest token flow)
  return true;
};