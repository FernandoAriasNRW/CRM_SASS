import { Component, inject, input, output, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { AvatarComponent } from '../../shared/ui/avatar.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideX, lucideCheck, lucideUser, lucideTag,
  lucideFlag, lucideMessageSquare, lucidePaperclip,
  lucideSend, lucideChevronDown, lucideMail, lucidePhone, lucideBuilding
} from '@ng-icons/lucide';
import type { Ticket } from './ticket-create-modal.component';
import { TICKET_TAGS, type Tag } from '../../shared/utils/tags';

interface Comment {
  id: string;
  authorId: string;
  authorName: string;
  content: string;
  createdAtUtc: string;
}

const STATUSES = ['New', 'In Progress', 'Resolved', 'Closed'];
const PRIORITIES = [
  { key: 'urgent', label: 'Urgente' },
  { key: 'high',   label: 'Alta'    },
  { key: 'normal', label: 'Normal'  },
  { key: 'low',    label: 'Baja'    },
];
const STATUS_BADGE: Record<string, BadgeVariant> = {
  'New': 'secondary', 'In Progress': 'default', 'Resolved': 'success', 'Closed': 'outline'
};

@Component({
  selector: 'app-ticket-detail-panel',
  standalone: true,
  imports: [FormsModule, DatePipe, BadgeComponent, AvatarComponent, NgIconComponent],
  viewProviders: [provideIcons({
    lucideX, lucideCheck, lucideUser, lucideTag,
    lucideFlag, lucideMessageSquare, lucidePaperclip,
    lucideSend, lucideChevronDown, lucideMail, lucidePhone, lucideBuilding
  })],
  templateUrl: './ticket-detail-panel.component.html',
})
export class TicketDetailPanelComponent implements OnInit {
  readonly ticket = input.required<Ticket>();
  readonly closed = output<void>();
  readonly updated = output<Ticket>();

  private readonly api = inject(ApiService);

  isEditing = false;
  isSaving = signal(false);
  title = '';
  description = '';
  status = '';
  priority = 'normal';
  selectedTags = signal<string[]>([]);
  showTagPicker = signal(false);
  newComment = signal('');
  comments = signal<Comment[]>([]);
  activeTab = signal<'comments' | 'activity'>('comments');

  readonly statuses = STATUSES;
  readonly priorities = PRIORITIES;
  readonly availableTags = TICKET_TAGS;

  statusBadge(s: string): BadgeVariant { return STATUS_BADGE[s] ?? 'outline'; }

  ngOnInit(): void {
    const t = this.ticket();
    this.title = t.title;
    this.description = t.description ?? '';
    this.status = t.status;
    this.priority = t.priority ?? 'normal';
    if ((t as any).tags) {
      this.selectedTags.set(String((t as any).tags).split(',').map((s: string) => s.trim()).filter(Boolean));
    }
    this.loadComments();
  }

  loadComments(): void {
    this.api.get<Comment[]>(`/tickets/${this.ticket().id}/comments`).subscribe({
      next: data => this.comments.set(data),
      error: () => {},
    });
  }

  saveField(field: string, value: unknown): void {
    this.isSaving.set(true);
    this.api.patch(`/tickets/${this.ticket().id}`, {
      title: this.title, description: this.description, priority: this.priority, status: this.status
    }).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.isEditing = false;
        this.updated.emit({ ...this.ticket(), title: this.title, description: this.description, status: this.status, priority: this.priority });
      },
      error: () => this.isSaving.set(false),
    });
  }

  changeStatus(newStatus: string): void {
    this.status = newStatus;
    this.api.patch(`/tickets/${this.ticket().id}`, { status: newStatus }).subscribe({
      next: () => this.updated.emit({ ...this.ticket(), status: newStatus }),
      error: () => {},
    });
  }

  toggleTag(key: string): void {
    this.selectedTags.update(tags =>
      tags.includes(key) ? tags.filter(t => t !== key) : [...tags, key]
    );
    this.saveField('tags', this.selectedTags().join(','));
  }

  isTagSelected(key: string): boolean {
    return this.selectedTags().includes(key);
  }

  getTag(key: string): Tag | undefined {
    return TICKET_TAGS.find(t => t.key === key);
  }

  sendComment(): void {
    const content = this.newComment().trim();
    if (!content) return;
    this.api.post<Comment>(`/tickets/${this.ticket().id}/comments`, { content }).subscribe({
      next: comment => {
        this.comments.update(c => [...c, comment]);
        this.newComment.set('');
      },
      error: () => {},
    });
  }

  onKeydown(e: KeyboardEvent): void {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) this.sendComment();
  }

  close(): void { this.closed.emit(); }
}
