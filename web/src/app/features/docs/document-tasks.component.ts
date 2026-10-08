import { Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { AttachedDocumentsService, type TaskWithDocument } from '../tasks/attached-documents.service';
import { taskStatusLabel } from '../tasks/task-vocabulary';
import { ToastService } from '../../shared/services/toast.service';
import { errorMessage } from '../../shared/utils/error-message';

interface TaskOption { id: string; title: string; projectId: string; status: string; }

/**
 * Las tareas que tienen adjunto este documento, y adjuntarlo a otra sin salir de él.
 *
 * Es la otra mitad de los adjuntos: desde la tarea se ve qué documentos tiene; desde el documento,
 * a qué trabajo pertenece. Mencionar una tarea en el texto es otra cosa —hablar de ella— y sale en
 * la tarea como «Mencionado en».
 */
@Component({
  selector: 'app-document-tasks',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="space-y-3">
      @if (loading()) {
        <p class="text-xs text-muted-foreground" i18n>Cargando…</p>
      } @else if (tasks().length === 0) {
        <p class="text-xs text-muted-foreground" i18n>Este documento no está adjunto a ninguna tarea.</p>
      } @else {
        <ul class="space-y-1">
          @for (t of tasks(); track t.taskId) {
            <li>
              <a [routerLink]="['/tasks']" [queryParams]="{ task: t.taskId }"
                 class="block rounded-md px-2 py-1.5 hover:bg-secondary transition-colors">
                <span class="text-sm">{{ t.title }}</span>
                <span class="block text-xs text-muted-foreground">{{ statusLabel(t.status) }}</span>
              </a>
            </li>
          }
        </ul>
      }

      <div class="space-y-1.5 border-t border-border pt-3">
        <label for="document-tasks-search" class="text-xs font-medium text-muted-foreground" i18n>Adjuntar a una tarea</label>
        <input id="document-tasks-search" type="text" [ngModel]="search()" (ngModelChange)="onSearch($event)"
               i18n-placeholder placeholder="Buscar una tarea…"
               class="w-full rounded-md border border-border bg-background px-2 py-1 text-sm" />
        @if (options().length > 0) {
          <ul class="max-h-48 overflow-y-auto space-y-0.5">
            @for (option of options(); track option.id) {
              <li>
                <button type="button" (click)="attachTo(option)"
                        class="w-full text-left text-sm rounded px-2 py-1 hover:bg-secondary truncate">
                  {{ option.title }}
                </button>
              </li>
            }
          </ul>
        }
      </div>
    </div>
  `,
})
export class DocumentTasksComponent {
  private readonly api = inject(ApiService);
  private readonly attachments = inject(AttachedDocumentsService);
  private readonly toast = inject(ToastService);

  readonly documentId = input.required<string>();

  readonly tasks = signal<TaskWithDocument[]>([]);
  readonly loading = signal(true);
  readonly search = signal('');
  readonly options = signal<TaskOption[]>([]);
  readonly statusLabel = taskStatusLabel;

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    // Al cambiar de documento se vuelve a preguntar: el panel lateral no se destruye entre uno
    // y otro.
    effect(() => {
      const id = this.documentId();
      this.search.set('');
      this.options.set([]);
      void this.load(id);
    });
  }

  private async load(documentId: string): Promise<void> {
    this.loading.set(true);
    try {
      this.tasks.set(await firstValueFrom(this.attachments.tasksWithDocument(documentId)));
    } catch {
      this.tasks.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  /** Busca en el servidor, con una pausa corta para no pedir una lista por cada tecla. */
  onSearch(text: string): void {
    this.search.set(text);
    clearTimeout(this.searchTimer);

    if (text.trim().length < 2) {
      this.options.set([]);
      return;
    }

    this.searchTimer = setTimeout(async () => {
      try {
        const page = await firstValueFrom(
          this.api.get<{ items?: TaskOption[] }>('/tasks', { search: text.trim(), pageSize: 20 }));
        const taken = new Set(this.tasks().map(t => t.taskId));
        this.options.set((Array.isArray(page?.items) ? page.items : []).filter(t => !taken.has(t.id)));
      } catch {
        this.options.set([]);
      }
    }, 250);
  }

  async attachTo(task: TaskOption): Promise<void> {
    try {
      await firstValueFrom(this.attachments.attach(task.id, this.documentId()));
      this.tasks.update(list => [
        { taskId: task.id, projectId: task.projectId, title: task.title, status: task.status },
        ...list,
      ]);
      this.options.update(list => list.filter(o => o.id !== task.id));
      this.search.set('');
      this.options.set([]);
    } catch (err) {
      this.toast.error(errorMessage(err, $localize`No se pudo adjuntar el documento`));
    }
  }
}
