import { Component, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter, map } from 'rxjs/operators';
import { AuthSignalStore } from './core/auth-signal.store';
import { RealtimeService } from './core/realtime.service';
import { NotificationsComponent } from './features/notifications/notifications.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { ApiService } from './core/api.service';
import { SessionManagerService } from './core/session-manager.service';
import { ToastContainerComponent } from './shared/ui/toast-container.component';
import { ToastService } from './shared/services/toast.service';
import { UserAvatarComponent } from './shared/ui/user-avatar.component';
import { SidebarCustomizerComponent } from './shared/ui/sidebar-customizer.component';
import { SubmenuCustomizerComponent } from './shared/ui/submenu-customizer.component';
import {
  lucideLayoutDashboard, lucideFolderKanban, lucideCheckSquare,
  lucideTicket, lucideLogOut, lucideMenu, lucideX,
  lucideMessageSquare, lucideCalendar, lucideBarChart2, lucideUser, lucideSettings,
  lucideWebhook, lucideChevronDown, lucideChevronRight, lucideFileText,
  lucideUsers, lucideHome, lucideMoreHorizontal, lucideChartBar
} from '@ng-icons/lucide';
import { HierarchySignalStore } from './core/hierarchy-signal.store';
import { NavigationSignalStore } from './core/navigation-signal.store';
import { UpperCasePipe, LowerCasePipe } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    NgIconComponent,
    NotificationsComponent,
    ToastContainerComponent,
    UserAvatarComponent,
    SidebarCustomizerComponent,
    SubmenuCustomizerComponent,
    UpperCasePipe
  ],
  viewProviders: [provideIcons({
    lucideLayoutDashboard, lucideFolderKanban, lucideCheckSquare,
    lucideTicket, lucideLogOut, lucideMenu, lucideX,
    lucideMessageSquare, lucideCalendar, lucideBarChart2, lucideUser, lucideSettings, lucideWebhook,
    lucideChevronDown, lucideChevronRight, lucideFileText, lucideUsers, lucideHome, lucideMoreHorizontal, lucideChartBar
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
  private readonly toast = inject(ToastService);
  readonly hierarchyStore = inject(HierarchySignalStore);
  readonly navStore = inject(NavigationSignalStore);

  readonly currentRouteName = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map((e: any) => {
        console.log("Event: ", e)
        const path = e.urlAfterRedirects.split('/')[1] || 'Home';
        const item = this.navStore.allItems().find(i => i.route === `/${path}`) || this.navStore.allItems().find(i => i.route === '/');
        return item ? item.label : path;
      })
    ),
    { initialValue: 'Dashboard' }
  );

  // Drawer state for sidebar customizer
  isCustomizerOpen = false;
  activeSubmenuCustomizer: string | null = null;

  openCustomizer(): void {
    this.isCustomizerOpen = true;
  }

  closeCustomizer(): void {
    this.isCustomizerOpen = false;
  }

  openSubmenuCustomizer(menuId: string): void {
    this.activeSubmenuCustomizer = menuId;
  }

  closeSubmenuCustomizer(): void {
    this.activeSubmenuCustomizer = null;
  }

  ngOnInit(): void {
    if (this.authStore.isAuthenticated()) {
      this.realtime.connect();
      this.realtime.connectChat();
      this.hierarchyStore.loadHierarchy();
      this.navStore.loadPreferences();

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
