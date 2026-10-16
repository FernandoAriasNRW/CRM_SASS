import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe, SlicePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { RealtimeService } from '../../core/realtime.service';
import { AvatarComponent } from '../../shared/ui/avatar.component';
import { ButtonComponent } from '../../shared/ui/button.component';
import { InputComponent } from '../../shared/ui/input.component';
import { BadgeComponent } from '../../shared/ui/badge.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucidePlus, lucideHash, lucideSend } from '@ng-icons/lucide';
import { Subscription } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { errorMessage } from '../../shared/utils/error-message';

interface Channel { id: string; name: string; type: string; }
interface Message { id: string; conversationId: string; senderId: string; content: string; sentAt: string; }

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [FormsModule, AvatarComponent, ButtonComponent, InputComponent, BadgeComponent, NgIconComponent, DatePipe, SlicePipe],
  viewProviders: [provideIcons({ lucidePlus, lucideHash, lucideSend })],
  templateUrl: './chat.component.html',
})
export class ChatComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly realtime = inject(RealtimeService);
  private readonly route = inject(ActivatedRoute);
  private sub?: Subscription;

  readonly channels = signal<Channel[]>([]);
  readonly messages = signal<Message[]>([]);
  readonly activeChannel = signal<Channel | null>(null);
  readonly newMessage = signal('');
  readonly sending = signal(false);
  /** Sin él, un fallo al cargar los canales se leía como «Sin canales», y uno de mensajes, como un canal vacío. */
  readonly channelsError = signal('');
  readonly messagesError = signal('');

  ngOnInit(): void {
    // El fallo se explica en la propia lista: el aviso global lo repetiría.
    this.api.get<{items: Channel[]}>('/channels', undefined, { silent: true }).subscribe({
      next: res => {
        const data = res.items || [];
        this.channels.set(data);
        // Un aviso de mensaje nuevo llega con ?channel=: se abre ese canal y no el primero.
        const requested = data.find(c => c.id === this.route.snapshot.queryParamMap.get('channel'));
        if (requested ?? data[0]) this.selectChannel(requested ?? data[0]);
      },
      error: (e: unknown) => this.channelsError.set(errorMessage(e, $localize`No se pudieron cargar los canales.`)),
    });

    this.sub = this.realtime.chatMessage$.subscribe(msg => {
      if (msg.conversationId === this.activeChannel()?.id) {
        this.messages.update(msgs => [...msgs, msg]);
      }
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  selectChannel(channel: Channel): void {
    this.activeChannel.set(channel);
    this.messages.set([]);
    this.messagesError.set('');
    this.api.get<{items: Message[]}>(`/channels/${channel.id}/messages`, undefined, { silent: true }).subscribe({
      next: res => this.messages.set(res.items || []),
      error: (e: unknown) => this.messagesError.set(errorMessage(e, $localize`No se pudieron cargar los mensajes de este canal.`)),
    });
    this.realtime.joinChannel(channel.id);
  }

  send(): void {
    const content = this.newMessage().trim();
    const channel = this.activeChannel();
    if (!content || !channel || this.sending()) return;
    this.sending.set(true);
    this.api.post(`/channels/${channel.id}/messages`, { content }).subscribe({
      next: (msg: any) => {
        this.messages.update(msgs => [...msgs, msg]);
        this.newMessage.set('');
        this.sending.set(false);
      },
      error: () => this.sending.set(false),
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }
}
