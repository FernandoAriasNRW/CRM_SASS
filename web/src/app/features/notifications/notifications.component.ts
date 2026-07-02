import { Component, inject, OnInit, signal, HostListener, ElementRef, OnDestroy } from '@angular/core';
import { Store } from '@ngrx/store';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideBell, lucideCheck, lucideX, lucideSettings, lucideTrash2,
  lucideAlertCircle, lucideCheckCircle, lucideInfo, lucideClock
} from '@ng-icons/lucide';
import {
  notificationsLoaded, notificationReceived, notificationMarkedRead,
  selectNotifications, selectUnreadCount, type Notification
} from '../../state/notifications/notifications.state';
import { WebPushService } from '../../shared/services/web-push.service';
import { NotificationPreferencesComponent } from '../../shared/ui/notification-preferences.component';
import { AuthSignalStore } from '../../core/auth-signal.store';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [NgIconComponent, NotificationPreferencesComponent],
  viewProviders: [provideIcons({
    lucideBell, lucideCheck, lucideX, lucideSettings, lucideTrash2,
    lucideAlertCircle, lucideCheckCircle, lucideInfo, lucideClock
  })],
  template: `
    <div class="relative">
      <!-- Bell Button -->
      <button
        (click)="toggle()"
        class="relative p-2 rounded-md hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
        [attr.aria-label]="'Notificaciones' + (unreadCount() > 0 ? ' (' + unreadCount() + ' no leídas)' : '')"
      >
        <ng-icon name="lucideBell" size="18" />
        @if (unreadCount() > 0) {
          <span class="absolute -top-0.5 -right-0.5 flex h-4 w-4 items-center justify-center rounded-full bg-destructive text-[10px] font-medium text-destructive-foreground">
            {{ unreadCount() > 99 ? '99+' : unreadCount() }}
          </span>
        }
      </button>

      <!-- Dropdown Panel -->
      @if (open()) {
        <div class="absolute right-0 mt-2 w-96 bg-card rounded-xl border border-border shadow-2xl z-50">
          <!-- Header -->
          <div class="flex items-center justify-between px-4 py-3 border-b border-border">
            <h3 class="font-semibold text-sm">Notificaciones</h3>
            <div class="flex items-center gap-1">
              <button
                (click)="showPreferences.set(true)"
                class="p-1.5 rounded-md hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
                title="Configuración de notificaciones"
              >
                <ng-icon name="lucideSettings" size="16" />
              </button>
              <button
                (click)="toggle()"
                class="p-1.5 rounded-md hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
              >
                <ng-icon name="lucideX" size="16" />
              </button>
            </div>
          </div>

          <!-- Notification List -->
          <div class="max-h-96 overflow-y-auto">
            @if (loading()) {
              <div class="flex items-center justify-center py-8">
                <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
              </div>
            } @else {
              @if (notifications().length === 0) {
                <div class="flex flex-col items-center justify-center py-8 text-center">
                  <ng-icon name="lucideBell" size="32" class="text-muted-foreground/30 mb-2" />
                  <p class="text-sm text-muted-foreground">No hay notificaciones</p>
                </div>
              } @else {
                @for (notification of notifications(); track notification.id) {
                  <div
                    class="flex items-start gap-3 px-4 py-3 hover:bg-accent/50 transition-colors border-b border-border last:border-0"
                    [class.bg-accent/30]="!notification.isRead"
                  >
                    <!-- Icon based on type -->
                    <div class="shrink-0 mt-0.5">
                      @switch (notification.type) {
                        @case ('success') {
                          <ng-icon name="lucideCheckCircle" size="18" class="text-green-500" />
                        }
                        @case ('warning') {
                          <ng-icon name="lucideAlertCircle" size="18" class="text-yellow-500" />
                        }
                        @case ('error') {
                          <ng-icon name="lucideAlertCircle" size="18" class="text-red-500" />
                        }
                        @default {
                          <ng-icon name="lucideInfo" size="18" class="text-blue-500" />
                        }
                      }
                    </div>

                    <!-- Content -->
                    <div class="flex-1 min-w-0">
                      <p class="text-sm font-medium line-clamp-2">{{ notification.title }}</p>
                      @if (notification.body) {
                        <p class="text-xs text-muted-foreground mt-0.5 line-clamp-2">{{ notification.body }}</p>
                      }
                      <div class="flex items-center gap-1 mt-1">
                        <ng-icon name="lucideClock" size="10" class="text-muted-foreground" />
                        <span class="text-[10px] text-muted-foreground">{{ formatTime(notification.createdAtUtc) }}</span>
                      </div>
                    </div>

                    <!-- Actions -->
                    <div class="shrink-0 flex items-center gap-1">
                      @if (!notification.isRead) {
                        <button
                          (click)="markRead(notification.id)"
                          class="p-1 rounded-md hover:bg-primary/10 text-muted-foreground hover:text-primary transition-colors"
                          title="Marcar como leída"
                        >
                          <ng-icon name="lucideCheck" size="14" />
                        </button>
                      }
                      <button
                        (click)="deleteNotification(notification.id)"
                        class="p-1 rounded-md hover:bg-destructive/10 text-muted-foreground hover:text-destructive transition-colors"
                        title="Eliminar"
                      >
                        <ng-icon name="lucideTrash2" size="14" />
                      </button>
                    </div>
                  </div>
                }
              }
            }
          </div>

          <!-- Footer -->
          <div class="px-4 py-2 border-t border-border bg-muted/30">
            <button
              (click)="markAllRead()"
              class="w-full text-xs text-center text-muted-foreground hover:text-primary transition-colors py-1"
            >
              Marcar todas como leídas
            </button>
          </div>
        </div>
      }

      <!-- Preferences Modal -->
      @if (showPreferences()) {
        <app-notification-preferences (closed)="showPreferences.set(false)" />
      }
    </div>
  `,
  styles: [`
    :host {
      display: inline-block;
    }
  `]
})
export class NotificationsComponent implements OnInit, OnDestroy {
  private readonly store = inject(Store);
  private readonly api = inject(ApiService);
  private readonly realtime = inject(RealtimeService);
  private readonly el = inject(ElementRef);
  private readonly webPush = inject(WebPushService);
  private readonly authStore = inject(AuthSignalStore);

  readonly notifications$ = this.store.select(selectNotifications);
  readonly notifications = signal<Notification[]>([]);
  readonly unreadCount = signal(0);
  readonly open = signal(false);
  readonly loading = signal(true);
  readonly showPreferences = signal(false);

  ngOnInit(): void {
    this.loadNotifications();

    // Subscribe to real-time notifications
    this.realtime.notification$.subscribe(item => {
      this.store.dispatch(notificationReceived({ item }));
      // Update badge
      this.webPush.updateBadgeCount(this.unreadCount() + 1);
    });

    // Sync NgRx state to signals
    this.notifications$.subscribe(notifications => {
      this.notifications.set(notifications);
    });

    this.store.select(selectUnreadCount).subscribe(count => {
      this.unreadCount.set(count);
      // Update browser badge
      this.webPush.updateBadgeCount(count);
    });
  }

  ngOnDestroy(): void {
    // Clear badge when component is destroyed
    this.webPush.clearBadge();
  }

  loadNotifications(): void {
    this.api.get<{items: Notification[]}>('/notifications').subscribe({
      next: res => {
        this.store.dispatch(notificationsLoaded({ items: res.items || [] }));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  toggle(): void {
    this.open.set(!this.open());
  }

  markRead(id: string): void {
    this.api.post(`/notifications/${id}/read`, {}).subscribe({
      next: () => this.store.dispatch(notificationMarkedRead({ id })),
      error: () => {},
    });
  }

  markAllRead(): void {
    this.api.post('/notifications/read-all', {}).subscribe({
      next: () => {
        this.loadNotifications();
      },
      error: () => {},
    });
  }

  deleteNotification(id: string): void {
    this.api.delete(`/notifications/${id}`).subscribe({
      next: () => {
        this.loadNotifications();
      },
      error: () => {},
    });
  }

  formatTime(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);

    if (minutes < 1) return 'Hace un momento';
    if (minutes < 60) return `Hace ${minutes} min`;
    if (hours < 24) return `Hace ${hours} h`;
    if (days < 7) return `Hace ${days} días`;
    return date.toLocaleDateString('es-ES', { day: '2-digit', month: 'short' });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.el.nativeElement.contains(event.target)) {
      this.open.set(false);
    }
  }
}