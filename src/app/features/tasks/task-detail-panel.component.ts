import { Component, inject, input, output, signal, OnInit, computed, effect } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import { BadgeComponent, type BadgeVariant } from '../../shared/ui/badge.component';
import { AvatarComponent } from '../../shared/ui/avatar.component';
import { SkeletonComponent } from '../../shared/ui/skeleton.component';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideX, lucideCheck, lucideCalendar, lucideClock, lucideUser,
  lucideTag, lucideFlag, lucideMessageSquare, lucidePaperclip,
  lucideSmile, lucideSend, lucideChevronDown, lucideAlertCircle,
  lucideArrowUp, lucideMinus, lucideArrowDown, lucideLoader2
} from '@ng-icons/lucide';
import { DrawerComponent } from '../../shared/ui/drawer.component';
import type { TaskItem } from './task-create-modal.component';
import { TASK_TAGS, type Tag } from '../../shared/utils/tags';

interface Comment {
  id: string;
  authorId: string;
  authorName: string;
  content: string;
  createdAtUtc: string;
}

const PRIORITIES = [
  { key: 'urgent',  label: 'Urgente',  icon: 'lucideAlertCircle', color: 'text-red-500' },
  { key: 'high',    label: 'Alta',     icon: 'lucideArrowUp',     color: 'text-orange-500' },
  { key: 'normal',  label: 'Normal',   icon: 'lucideMinus',       color: 'text-blue-500' },
  { key: 'low',     label: 'Baja',     icon: 'lucideArrowDown',   color: 'text-gray-400' },
];

const STATUSES = ['To Do', 'In Progress', 'In Review', 'Done'];

const STATUS_BADGE: Record<string, BadgeVariant> = {
  'To Do': 'secondary', 'In Progress': 'default', 'In Review': 'warning', 'Done': 'success'
};

@Component({
  selector: 'app-task-detail-panel',
  standalone: true,
  imports: [FormsModule, DatePipe, BadgeComponent, AvatarComponent, NgIconComponent, SkeletonComponent, DrawerComponent],
  viewProviders: [provideIcons({
    lucideX, lucideCheck, lucideCalendar, lucideClock, lucideUser,
    lucideTag, lucideFlag, lucideMessageSquare, lucidePaperclip,
    lucideSmile, lucideSend, lucideChevronDown, lucideAlertCircle,
    lucideArrowUp, lucideMinus, lucideArrowDown, lucideLoader2
  })],
  templateUrl: './task-detail-panel.component.html',
})
export class TaskDetailPanelComponent implements OnInit {
  readonly task = input.required<TaskItem>();
  readonly closed = output<void>();
  readonly updated = output<TaskItem>();

  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  // Estado editable local
  title = '';
  description = '';
  status = '';
  priority = 'normal';
  dueDate = '';
  estimatedHours = 0;
  selectedTags = signal<string[]>([]);
  showTagPicker = signal(false);
  newComment = signal('');
  comments = signal<Comment[]>([]);
  saving = signal(false);
  loadingComments = signal(false);
  sendingComment = signal(false);
  activeTab = signal<'comments' | 'activity'>('comments');

  readonly priorities = PRIORITIES;
  readonly statuses = STATUSES;
  readonly availableTags = TASK_TAGS;

  readonly currentPriority = computed(() =>
    PRIORITIES.find(p => p.key === this.priority) ?? PRIORITIES[2]
  );

  statusBadge(s: string): BadgeVariant { return STATUS_BADGE[s] ?? 'outline'; }

  ngOnInit(): void {
    const t = this.task();
    this.title = t.title;
    this.description = t.description ?? '';
    this.status = t.status;
    this.dueDate = t.dueDate ?? '';
    this.estimatedHours = t.estimatedHours ?? 0;
    // Parsear etiquetas guardadas como string separado por comas
    if ((t as any).tags) {
      this.selectedTags.set(String((t as any).tags).split(',').map((s: string) => s.trim()).filter(Boolean));
    }
    this.loadComments();
  }

  loadComments(): void {
    this.loadingComments.set(true);
    this.api.get<Comment[]>(`/tasks/${this.task().id}/comments`).subscribe({
      next: data => {
        this.comments.set(data);
        this.loadingComments.set(false);
      },
      error: () => {
        this.loadingComments.set(false);
        this.toast.error('Error', 'No se pudieron cargar los comentarios');
      },
    });
  }

  saveField(field: string, value: unknown): void {
    this.saving.set(true);
    const payload: Record<string, unknown> = {};
    payload[field] = value;
    this.api.patch(`/tasks/${this.task().id}`, payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.updated.emit({ ...this.task(), title: this.title, description: this.description, status: this.status, dueDate: this.dueDate, estimatedHours: this.estimatedHours });
        if (field === 'title') {
          this.toast.success('Guardado', 'Título actualizado');
        }
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error('Error', 'No se pudo guardar el cambio');
      },
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
    return TASK_TAGS.find(t => t.key === key);
  }

  changeStatus(newStatus: string): void {
    this.status = newStatus;
    this.api.post(`/tasks/${this.task().id}/move`, { newStatus }).subscribe({
      next: () => {
        this.updated.emit({ ...this.task(), status: newStatus });
        this.toast.success('Estado actualizado', `La tarea ahora está en ${newStatus}`);
      },
      error: () => {
        this.toast.error('Error', 'No se pudo cambiar el estado');
      },
    });
  }

  sendComment(): void {
    const content = this.newComment().trim();
    if (!content) return;
    this.sendingComment.set(true);
    this.api.post<Comment>(`/tasks/${this.task().id}/comments`, { content }).subscribe({
      next: comment => {
        this.comments.update(c => [...c, comment]);
        this.newComment.set('');
        this.sendingComment.set(false);
        this.toast.success('Comentario agregado', 'Tu comentario fue publicado');
      },
      error: () => {
        this.sendingComment.set(false);
        this.toast.error('Error', 'No se pudo enviar el comentario');
      },
    });
  }

  onKeydown(e: KeyboardEvent): void {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) this.sendComment();
  }

  close(): void { this.closed.emit(); }
}
