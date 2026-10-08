import { Component, inject, OnInit, signal, HostListener, ElementRef, OnDestroy } from '@angular/core';
import { Store } from '@ngrx/store';
import { NgTemplateOutlet } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideBell, lucideCheck, lucideX, lucideSettings, lucideTrash2,
  lucideAlertCircle, lucideCheckCircle, lucideInfo, lucideClock
} from '@ng-icons/lucide';
import {
  notificationsLoaded, notificationReceived, notificationMarkedRead,
  selectNotifications, selectUnreadCount, fromApi, type Notification, type NotificationDto
} from '../../state/notifications/notifications.state';
import { Router } from '@angular/router';
import { mentionLink } from '../../shared/utils/comment-mentions';
import { WebPushService } from '../../shared/services/web-push.service';
import { NotificationPreferencesComponent } from '../../shared/ui/notification-preferences.component';
import { AuthSignalStore } from '../../core/auth-signal.store';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [NgIconComponent, NgTemplateOutlet, NotificationPreferencesComponent],
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
        [attr.aria-label]="unreadCount() > 0 ? bellLabelUnread(unreadCount()) : bellLabel"
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
            <h3 class="font-semibold text-sm" i18n>Notificaciones</h3>
            <div class="flex items-center gap-1">
              <button
                (click)="showPreferences.set(true)"
                class="p-1.5 rounded-md hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
                i18n-title title="Preferencias de avisos" i18n-aria-label aria-label="Preferencias de avisos"
              >
                <ng-icon name="lucideSettings" size="16" />
              </button>
              <button
                (click)="toggle()"
                i18n-aria-label aria-label="Cerrar"
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
                  <p class="text-sm text-muted-foreground" i18n>No hay notificaciones</p>
                </div>
              } @else {
                @for (notification of notifications(); track notification.id) {
                  <div
                    class="flex items-start gap-3 px-4 py-3 hover:bg-accent/50 transition-colors border-b border-border last:border-0"
                    [class.bg-accent/30]="!notification.isRead"
                  >
                    <div class="shrink-0 mt-0.5">
                      <ng-icon name="lucideInfo" size="18" class="text-primary" />
                    </div>

                    <!-- El aviso lleva a lo que avisa: la tarea, el ticket, el proyecto. -->
                    @if (hasLink(notification)) {
                      <button type="button" (click)="openNotification(notification)" class="flex-1 min-w-0 text-left">
                        <ng-container *ngTemplateOutlet="notificationText; context: { $implicit: notification }" />
                      </button>
                    } @else {
                      <div class="flex-1 min-w-0">
                        <ng-container *ngTemplateOutlet="notificationText; context: { $implicit: notification }" />
                      </div>
                    }

                    <!-- Actions -->
                    <div class="shrink-0 flex items-center gap-1">
                      @if (!notification.isRead) {
                        <button
                          (click)="markRead(notification.id)"
                          class="p-1 rounded-md hover:bg-primary-subtle text-muted-foreground hover:text-primary-subtle-fg transition-colors"
                          i18n-title title="Marcar como leída" i18n-aria-label aria-label="Marcar como leída"
                        >
                          <ng-icon name="lucideCheck" size="14" />
                        </button>
                      }
                      <button
                        (click)="deleteNotification(notification.id)"
                        class="p-1 rounded-md hover:bg-destructive-subtle text-muted-foreground hover:text-destructive-subtle-fg transition-colors"
                        i18n-title title="Eliminar" i18n-aria-label aria-label="Eliminar"
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
             i18n>
              Marcar todas como leídas
            </button>
          </div>
        </div>
      }

      <ng-template #notificationText let-notification>
        <p class="text-sm font-medium line-clamp-2">{{ notification.title }}</p>
        @if (notification.body) {
          <p class="text-xs text-muted-foreground mt-0.5 line-clamp-2">{{ notification.body }}</p>
        }
        <div class="flex items-center gap-1 mt-1">
          <ng-icon name="lucideClock" size="10" class="text-muted-foreground" />
          <span class="text-[10px] text-muted-foreground">{{ formatTime(notification.createdAtUtc) }}</span>
        </div>
      </ng-template>

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
  private readonly router = inject(Router);

  readonly notifications$ = this.store.select(selectNotifications);
  readonly notifications = signal<Notification[]>([]);
  readonly unreadCount = signal(0);
  readonly open = signal(false);
  readonly loading = signal(true);
  readonly showPreferences = signal(false);

  readonly bellLabel = $localize`Notificaciones`;
  bellLabelUnread(count: number): string {
    return $localize`Notificaciones (${count} sin leer)`;
  }

  ngOnInit(): void {
    this.loadNotifications();

    // Subscribe to real-time notifications
    this.realtime.notification$.subscribe(dto => {
      this.store.dispatch(notificationReceived({ item: fromApi(dto) }));
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
    this.api.get<{ items?: NotificationDto[] }>('/notifications', { pageSize: 50 }).subscribe({
      next: res => {
        const items: Notification[] = (Array.isArray(res?.items) ? res.items : []).map(fromApi);
        this.store.dispatch(notificationsLoaded({ items }));
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

  /** Si el aviso lleva a algún sitio: una tarea, un ticket, un proyecto. */
  hasLink(notification: Notification): boolean {
    return !!notification.entityType && !!notification.entityId
      && mentionLink(notification.entityType, notification.entityId) !== null;
  }

  /** Lleva a lo que avisa y lo da por leído: abrirlo es haberlo visto. */
  openNotification(notification: Notification): void {
    if (!notification.entityType || !notification.entityId) return;
    const link = mentionLink(notification.entityType, notification.entityId);
    if (!link) return;

    if (!notification.isRead) this.markRead(notification.id);
    this.open.set(false);
    void this.router.navigate([link.route], { queryParams: link.queryParams });
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
    if (!dateStr) return $localize`Hace un momento`;
    const date = new Date(dateStr);
    if (isNaN(date.getTime())) return $localize`Hace un momento`;
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);

    if (minutes < 1) return $localize`Hace un momento`;
    if (minutes < 60) return $localize`Hace ${minutes} min`;
    if (hours < 24) return $localize`Hace ${hours} h`;
    if (days < 7) return $localize`Hace ${days} días`;
    return date.toLocaleDateString('es-ES', { day: '2-digit', month: 'short' });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.el.nativeElement.contains(event.target)) {
      this.open.set(false);
    }
  }
}