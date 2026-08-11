import { Routes } from '@angular/router';

export default [
  {
    path: '',
    loadComponent: () => import('./docs.component').then(m => m.DocsComponent)
  }
] as Routes;
