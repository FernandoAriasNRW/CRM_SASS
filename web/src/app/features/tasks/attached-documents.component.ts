import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideFileText, lucidePaperclip, lucideX } from '@ng-icons/lucide';
import { firstValueFrom } from 'rxjs';

import { AttachedDocumentsService, type AttachedDocument } from './attached-documents.service';
import { DocsService, type DocumentDto } from '../docs/docs.service';
import { ToastService } from '../../shared/services/toast.service';
import { errorMessage } from '../../shared/utils/error-message';

/**
 * Los documentos adjuntos a una tarea: el acta, la especificación, la guía.
 *
 * Ocupa el hueco que había en la ficha con un botón «Adjuntar archivo» que no hacía nada. Se
 * adjuntan documentos del módulo de documentos; cada uno se abre en su sitio y se puede quitar
 * de la tarea sin borrarlo.
 */
@Component({
  selector: 'app-attached-documents',
  standalone: true,
  imports: [FormsModule, RouterLink, NgIconComponent],
  viewProviders: [provideIcons({ lucideFileText, lucidePaperclip, lucideX })],
  template: `
    <div class="space-y-2">
      <p class="text-xs font-medium text-muted-foreground uppercase tracking-wide" i18n>Documentos adjuntos</p>

      @if (loading()) {
        <p class="text-sm text-muted-foreground" i18n>Cargando…</p>
      } @else {
        @if (attached().length === 0) {
          <p class="text-sm text-muted-foreground" i18n>Esta tarea no tiene documentos adjuntos.</p>
        } @else {
          <ul class="space-y-1">
            @for (d of attached(); track d.documentId) {
              <li class="flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-secondary transition-colors">
                <ng-icon name="lucideFileText" size="14" class="text-muted-foreground shrink-0" />
                <a [routerLink]="['/docs']" [queryParams]="{ doc: d.documentId }" class="text-sm flex-1 truncate hover:underline">
                  {{ d.title }}
                </a>
                <button type="button" (click)="detach(d)"
                        class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-muted"
                        i18n-title title="Quitar de la tarea" i18n-aria-label aria-label="Quitar de la tarea">
                  <ng-icon name="lucideX" size="12" />
                </button>
              </li>
            }
          </ul>
        }

        @if (picking()) {
          <div class="space-y-1.5 rounded-lg border border-border p-2">
            <label for="attach-document-search" class="sr-only" i18n>Buscar un documento</label>
            <input id="attach-document-search" type="text" [ngModel]="search()" (ngModelChange)="search.set($event)"
                   i18n-placeholder placeholder="Buscar un documento…"
                   class="w-full rounded-md border border-border bg-background px-2 py-1 text-sm" />
            <ul class="max-h-48 overflow-y-auto space-y-0.5">
              @for (doc of candidates(); track doc.id) {
                <li>
                  <button type="button" (click)="attach(doc)"
                          class="w-full text-left text-sm rounded px-2 py-1 hover:bg-secondary truncate">
                    {{ doc.title }}
                  </button>
                </li>
              } @empty {
                <li class="text-sm text-muted-foreground px-2 py-1" i18n>No hay documentos que adjuntar.</li>
              }
            </ul>
            <button type="button" (click)="picking.set(false)" class="text-xs text-muted-foreground hover:text-foreground" i18n>Cancelar</button>
          </div>
        } @else {
          <button type="button" (click)="openPicker()"
                  class="flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground border border-dashed border-border rounded-lg px-4 py-3 w-full transition-colors hover:bg-accent/30">
            <ng-icon name="lucidePaperclip" size="14" />
            <span i18n>Adjuntar documento</span>
          </button>
        }
      }
    </div>
  `,
})
export class AttachedDocumentsComponent {
  private readonly attachments = inject(AttachedDocumentsService);
  private readonly docs = inject(DocsService);
  private readonly toast = inject(ToastService);

  readonly taskId = input.required<string>();

  readonly attached = signal<AttachedDocument[]>([]);
  readonly loading = signal(true);
  readonly picking = signal(false);
  readonly search = signal('');
  private readonly allDocuments = signal<DocumentDto[]>([]);

  /** Los documentos que se pueden adjuntar: los que no lo están ya, filtrados por el título. */
  readonly candidates = computed(() => {
    const taken = new Set(this.attached().map(a => a.documentId));
    const text = this.search().trim().toLowerCase();
    return this.allDocuments()
      .filter(d => !taken.has(d.id) && (!text || d.title.toLowerCase().includes(text)))
      .slice(0, 50);
  });

  constructor() {
    queueMicrotask(() => void this.load());
  }

  private async load(): Promise<void> {
    try {
      this.attached.set(await firstValueFrom(this.attachments.forTask(this.taskId())));
    } catch {
      this.attached.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  async openPicker(): Promise<void> {
    this.search.set('');
    this.picking.set(true);
    if (this.allDocuments().length === 0) {
      try {
        const documents = await firstValueFrom(this.docs.getDocuments());
        this.allDocuments.set(Array.isArray(documents) ? documents : []);
      } catch {
        this.allDocuments.set([]);
      }
    }
  }

  async attach(doc: DocumentDto): Promise<void> {
    try {
      const added = await firstValueFrom(this.attachments.attach(this.taskId(), doc.id));
      this.attached.update(list => [added, ...list.filter(a => a.documentId !== added.documentId)]);
      this.picking.set(false);
    } catch (err) {
      this.toast.error(errorMessage(err, $localize`No se pudo adjuntar el documento`));
    }
  }

  async detach(doc: AttachedDocument): Promise<void> {
    try {
      await firstValueFrom(this.attachments.detach(this.taskId(), doc.documentId));
      this.attached.update(list => list.filter(a => a.documentId !== doc.documentId));
    } catch (err) {
      this.toast.error(errorMessage(err, $localize`No se pudo quitar el documento`));
    }
  }
}
