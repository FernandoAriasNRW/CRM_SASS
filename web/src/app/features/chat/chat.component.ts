import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
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

interface Channel { id: string; name: string; type: string; }
interface Message { id: string; channelId: string; senderId: string; content: string; sentAtUtc: string; }

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
  private sub?: Subscription;

  readonly channels = signal<Channel[]>([]);
  readonly messages = signal<Message[]>([]);
  readonly activeChannel = signal<Channel | null>(null);
  readonly newMessage = signal('');
  readonly sending = signal(false);

  ngOnInit(): void {
    this.api.get<Channel[]>('/channels').subscribe({
      next: data => {
        this.channels.set(data);
        if (data.length > 0) this.selectChannel(data[0]);
      },
      error: () => {},
    });

    this.sub = this.realtime.chatMessage$.subscribe(msg => {
      if (msg.channelId === this.activeChannel()?.id) {
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
    this.api.get<Message[]>(`/channels/${channel.id}/messages`).subscribe({
      next: data => this.messages.set(data),
      error: () => {},
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
