import { Routes } from '@angular/router';
import { authGuard, adminGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: 'login',   loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  { path: 'support', loadComponent: () => import('./features/tickets/public-ticket-form.component').then(m => m.PublicTicketFormComponent) },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '',             loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent) },
      { path: 'dashboard',    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'projects',     loadComponent: () => import('./features/projects/projects.component').then(m => m.ProjectsComponent) },
      { path: 'tasks',        loadComponent: () => import('./features/tasks/tasks.component').then(m => m.TasksComponent) },
      { path: 'tickets',      loadComponent: () => import('./features/tickets/tickets.component').then(m => m.TicketsComponent) },
      { path: 'chat',         loadComponent: () => import('./features/chat/chat.component').then(m => m.ChatComponent) },
      { path: 'calendar',     loadComponent: () => import('./features/calendar/calendar.component').then(m => m.CalendarComponent) },
      { path: 'reports',      loadComponent: () => import('./features/reports/reports.component').then(m => m.ReportsComponent) },
      {
        path: 'teams',
        loadChildren: () => import('./features/teams/teams.routes').then(m => m.routes)
      },
      {
        path: 'docs',
        loadChildren: () => import('./features/docs/docs.routes')
      },
      { path: 'webhooks',     redirectTo: '/admin' },
      { path: 'profile',      loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent) },
      { path: 'admin',        loadComponent: () => import('./features/admin/admin.component').then(m => m.AdminComponent), canActivate: [adminGuard] },
      { path: 'admin/users',  loadComponent: () => import('./features/admin/admin.component').then(m => m.AdminComponent), canActivate: [adminGuard] },
    ],
  },
  { path: '**', redirectTo: '' },
];
