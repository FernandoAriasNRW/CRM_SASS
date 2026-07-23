import { Injectable, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthSignalStore } from './auth-signal.store';

export interface RealtimeNotification {
  id: string;
  userId: string;
  title: string;
  body: string;
  type: 'info' | 'success' | 'warning' | 'error';
  createdAtUtc: string;
  isRead: boolean;
}

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private hub: signalR.HubConnection | null = null;
  private readonly authStore = inject(AuthSignalStore);

  readonly notification$ = new Subject<RealtimeNotification>();
  readonly taskMoved$ = new Subject<{ taskId: string; status: string }>();
  readonly chatMessage$ = new Subject<any>();
  readonly ticketMoved$ = new Subject<{ ticketId: string; status: number }>();
  private chatHub: signalR.HubConnection | null = null;
  private ticketsHub: signalR.HubConnection | null = null;

  connect(): void {
    if (this.hub?.state === signalR.HubConnectionState.Connected) return;

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:8080/hubs/notifications', {
        accessTokenFactory: () => this.authStore.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    this.hub.on('notification_received', (n: RealtimeNotification) => this.notification$.next(n));

    this.hub.start().catch(err => console.warn('SignalR connection failed:', err));
  }

  connectBoard(projectId: string): void {
    const boardHub = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:8080/hubs/board', {
        accessTokenFactory: () => this.authStore.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    boardHub.on('task_moved', (task: any) => this.taskMoved$.next(task));
    boardHub.start()
      .then(() => boardHub.invoke('JoinBoard', projectId))
      .catch(err => console.warn('Board hub failed:', err));
  }

  connectTickets(tenantId: string): void {
    if (this.ticketsHub?.state === signalR.HubConnectionState.Connected) return;

    this.ticketsHub = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:8080/hubs/tickets', {
        accessTokenFactory: () => this.authStore.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    this.ticketsHub.on('ticket_moved', (ticket: any) => this.ticketMoved$.next(ticket));
    this.ticketsHub.start()
      .then(() => this.ticketsHub?.invoke('JoinTickets', tenantId))
      .catch(err => console.warn('Tickets hub failed:', err));
  }

  connectChat(): void {
    if (this.chatHub?.state === signalR.HubConnectionState.Connected) return;
    this.chatHub = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:8080/hubs/chat', {
        accessTokenFactory: () => this.authStore.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .build();
    this.chatHub.on('message_received', (msg: any) => this.chatMessage$.next(msg));
    this.chatHub.start().catch(err => console.warn('Chat hub failed:', err));
  }

  joinChannel(channelId: string): void {
    this.chatHub?.invoke('JoinChannel', channelId).catch(() => {});
  }

  disconnect(): void {
    this.hub?.stop();
    this.chatHub?.stop();
    this.ticketsHub?.stop();
  }
}
