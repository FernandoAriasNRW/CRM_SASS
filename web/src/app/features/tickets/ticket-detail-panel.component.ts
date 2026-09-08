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

import { DrawerComponent } from '../../shared/ui/drawer.component';
import { ClickableDirective } from '../../shared/directives/clickable.directive';
import { ComentariosComponent } from '../../shared/ui/comentarios.component';

@Component({
  selector: 'app-ticket-detail-panel',
  standalone: true,
  imports: [ComentariosComponent, ClickableDirective, FormsModule, DatePipe, BadgeComponent, AvatarComponent, NgIconComponent, DrawerComponent],
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



  close(): void { this.closed.emit(); }
}
