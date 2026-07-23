import { Routes } from '@angular/router';

export const routes: Routes = [
  { 
    path: '', 
    loadComponent: () => import('./teams-list.component').then(m => m.TeamsListComponent) 
  },
  { 
    path: ':id', 
    loadComponent: () => import('./team-detail.component').then(m => m.TeamDetailComponent) 
  }
];
