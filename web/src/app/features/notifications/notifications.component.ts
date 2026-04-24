import { Component, inject, OnInit, signal, HostListener, ElementRef } from '@angular/core';
import { Store } from '@ngrx/store';
import { AsyncPipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideBell, lucideCheck } from '@ng-icons/lucide';
import {
  notificationsLoaded, notificationReceived, notificationMarkedRead,
  selectNotifications, selectUnreadCount, type Notification
} from '../../state/notifications/notifications.state';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [NgIconComponent, AsyncPipe],
  viewProviders: [provideIcons({ lucideBell, lucideCheck })],
  templateUrl: './notifications.component.html',
})
export class NotificationsComponent implements OnInit {
  private readonly store = inject(Store);
  private readonly api = inject(ApiService);
  private readonly realtime = inject(RealtimeService);
  private readonly el = inject(ElementRef);

  readonly notifications$ = this.store.select(selectNotifications);
  readonly unreadCount$ = this.store.select(selectUnreadCount);
  readonly open = signal(false);

  ngOnInit(): void {
    this.api.get<Notification[]>('/notifications').subscribe({
      next: items => this.store.dispatch(notificationsLoaded({ items })),
      error: () => {},
    });

    this.realtime.notification$.subscribe(item =>
      this.store.dispatch(notificationReceived({ item }))
    );
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

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.el.nativeElement.contains(event.target)) {
      this.open.set(false);
    }
  }
}
