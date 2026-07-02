import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthSignalStore } from './core/auth-signal.store';
import { RealtimeService } from './core/realtime.service';
import { NotificationsComponent } from './features/notifications/notifications.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { ApiService } from './core/api.service';
import { SessionManagerService } from './core/session-manager.service';
import { ToastContainerComponent } from './shared/ui/toast-container.component';
import {
  lucideLayoutDashboard, lucideFolderKanban, lucideCheckSquare,
  lucideTicket, lucideLogOut, lucideMenu, lucideX,
  lucideMessageSquare, lucideCalendar, lucideBarChart2, lucideUser, lucideSettings,
  lucideWebhook
} from '@ng-icons/lucide';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    NgIconComponent,
    NotificationsComponent,
    ToastContainerComponent
  ],
  viewProviders: [provideIcons({
    lucideLayoutDashboard, lucideFolderKanban, lucideCheckSquare,
    lucideTicket, lucideLogOut, lucideMenu, lucideX,
    lucideMessageSquare, lucideCalendar, lucideBarChart2, lucideUser, lucideSettings, lucideWebhook
  })],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  readonly authStore = inject(AuthSignalStore);
  private readonly router = inject(Router);
  private readonly realtime = inject(RealtimeService);
  private readonly api = inject(ApiService);
  private readonly sessionManager = inject(SessionManagerService);

  readonly navItems = [
    { path: '/',          label: 'Dashboard', icon: 'lucideLayoutDashboard', exact: true },
    { path: '/projects',  label: 'Proyectos', icon: 'lucideFolderKanban' },
    { path: '/tasks',     label: 'Tareas',    icon: 'lucideCheckSquare' },
    { path: '/tickets',   label: 'Tickets',   icon: 'lucideTicket' },
    { path: '/chat',      label: 'Chat',      icon: 'lucideMessageSquare' },
    { path: '/calendar',  label: 'Calendario',icon: 'lucideCalendar' },
    { path: '/reports',   label: 'Reportes',  icon: 'lucideBarChart2' },
    { path: '/webhooks',  label: 'Webhooks',  icon: 'lucideWebhook' },
  ];

  ngOnInit(): void {
    if (this.authStore.isAuthenticated()) {
      this.realtime.connect();
      this.realtime.connectChat();

      // Fetch user info if not loaded
      if (!this.authStore.userInfo()) {
        this.api.get<{ id: string; name: string; email: string; tenantId: string; role: string }>('/auth/users/me')
          .subscribe({
            next: (user) => this.authStore.setUserInfo(user),
            error: () => {}
          });
      }
    }
  }

  logout(): void {
    // Call backend logout - it will clear the refresh token cookie
    // No need to pass refresh token in body - it's sent via cookie automatically
    this.api.post('/auth/logout', {}).subscribe({
      next: () => {
        this.realtime.disconnect();
        this.authStore.logout();
      },
      error: () => {
        this.realtime.disconnect();
        this.authStore.logout();
      }
    });
  }

  navigateToProfile(): void {
    this.router.navigate(['/profile']);
  }

  navigateToAdminUsers(): void {
    this.router.navigate(['/admin/users']);
  }
}
