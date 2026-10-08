import { Component, computed, inject, input, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideSend, lucideTrash2, lucidePencil, lucideLoader2, lucideCircleAlert, lucideX } from '@ng-icons/lucide';
import {
  CommentsService, type Comment, type CommentableEntity,
} from '../../core/comments.service';
import { UsersService } from '../../core/users.service';
import { AuthSignalStore } from '../../core/auth-signal.store';
import { RouterLink } from '@angular/router';
import { UserAvatarComponent } from './user-avatar.component';
import { errorMessage } from '../utils/error-message';
import { MentionsService } from '../../features/docs/mentions.service';
import { MENTION_TYPE_LABELS, type MentionCandidate } from '../../features/docs/extensions/mention';
import {
  commentSegments, mentionLink, mentionPrefix, toDraft, toStored,
  type CommentSegment, type DraftMention,
} from '../utils/comment-mentions';

/** Qué cuadro de texto está escribiendo: el de un comentario nuevo o el de uno que se edita. */
type Draft = 'new' | 'edit';

/**
 * El hilo de comentarios de una tarea, un ticket o un proyecto.
 *
 * **Un solo componente para los tres.** Comentar es la misma operación en los tres sitios, con
 * las mismas reglas; triplicarlo daría tres sitios donde arreglar el mismo fallo. Es la misma
 * decisión que en el backend, donde hay un módulo y no tres.
 *
 * Un comentario se manda y **se pinta cuando el servidor lo devuelve**, no antes. Es la
 * excepción a lo que hace el resto del producto —donde se pinta y se revierte— y tiene motivo:
 * un comentario que aparece y desaparece se lee como un mensaje perdido, y quien lo escribió no
 * sabe si volver a escribirlo. Para un cambio de estado revertir está bien; para algo que
 * alguien redactó, no.
 */
@Component({
  selector: 'app-comments',
  standalone: true,
  imports: [FormsModule, DatePipe, NgTemplateOutlet, NgIconComponent, RouterLink, UserAvatarComponent],
  viewProviders: [provideIcons({ lucideSend, lucideTrash2, lucidePencil, lucideLoader2, lucideCircleAlert, lucideX })],
  templateUrl: './comments.component.html',
})
export class CommentsComponent implements OnInit {
  readonly entityType = input.required<CommentableEntity>();
  readonly entityId = input.required<string>();

  private readonly service = inject(CommentsService);
  private readonly users = inject(UsersService);
  private readonly session = inject(AuthSignalStore);
  private readonly mentionSearch = inject(MentionsService);

  readonly comments = signal<Comment[]>([]);
  readonly loading = signal(false);
  readonly sending = signal(false);
  readonly error = signal('');

  /** El comentario que se está editando, o `null`. */
  readonly editing = signal<string | null>(null);
  readonly deleting = signal<string | null>(null);

  text = '';
  editedText = '';
  /** A qué comentario se responde, si se está respondiendo. */
  readonly replyingTo = signal<string | null>(null);

  // ── Menciones ───────────────────────────────────────────────────────────────────────────
  //
  // Al escribir `@` se buscan personas y equipos, y con `#` tareas, tickets, proyectos y
  // documentos. Lo elegido se ve como `@Ana Pérez` y se guarda como `@[Ana Pérez](Person:id)`:
  // ver `comment-mentions.ts`, que es el contrato con el servidor.

  /** Las menciones elegidas en cada cuadro, para convertirlas al enviar. */
  private newMentions: DraftMention[] = [];
  private editMentions: DraftMention[] = [];

  readonly suggestions = signal<MentionCandidate[]>([]);
  readonly activeSuggestion = signal(0);
  /** En qué cuadro está abierto el desplegable, o `null` si no lo está. */
  readonly suggestingFor = signal<Draft | null>(null);
  readonly typeLabels = MENTION_TYPE_LABELS;
  readonly prefixOf = mentionPrefix;

  /** Dónde está lo que se está escribiendo tras el `@` o el `#`, para sustituirlo al elegir. */
  private trigger: { draft: Draft; start: number; end: number } | null = null;
  /** Para descartar respuestas que llegan tarde: sólo cuenta la última búsqueda. */
  private searchSequence = 0;

  segments(text: string): CommentSegment[] {
    return commentSegments(text);
  }

  linkOf(segment: CommentSegment) {
    return segment.kind === 'mention' ? mentionLink(segment.type, segment.id) : null;
  }

  /** Mira si lo que hay justo antes del cursor es una mención a medio escribir, y busca. */
  onDraftInput(event: Event, draft: Draft): void {
    const box = event.target as HTMLTextAreaElement;
    const caret = box.selectionStart ?? box.value.length;
    const match = /(^|\s)([@#])([^\s@#]{1,40})$/.exec(box.value.slice(0, caret));

    if (!match) {
      this.closeSuggestions();
      return;
    }

    this.trigger = { draft, start: caret - match[2].length - match[3].length, end: caret };
    const sequence = ++this.searchSequence;

    void this.mentionSearch.search(match[2], match[3]).then(candidates => {
      if (sequence !== this.searchSequence) return;
      this.suggestions.set(candidates.slice(0, 8));
      this.activeSuggestion.set(0);
      this.suggestingFor.set(candidates.length ? draft : null);
    });
  }

  /** Con el desplegable abierto, las flechas lo recorren, Intro o Tab eligen y Escape lo cierra. */
  onDraftKeydown(event: KeyboardEvent, draft: Draft, box: HTMLTextAreaElement): void {
    const candidates = this.suggestions();
    if (this.suggestingFor() !== draft || !candidates.length) return;

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.activeSuggestion.update(i => (i + 1) % candidates.length);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.activeSuggestion.update(i => (i - 1 + candidates.length) % candidates.length);
        break;
      case 'Enter':
      case 'Tab':
        if (event.ctrlKey) return;
        event.preventDefault();
        this.pick(candidates[this.activeSuggestion()], box);
        break;
      case 'Escape':
        event.preventDefault();
        this.closeSuggestions();
        break;
    }
  }

  /** Sustituye lo escrito tras el `@` o el `#` por el nombre elegido, y lo recuerda para enviarlo. */
  pick(candidate: MentionCandidate, box: HTMLTextAreaElement): void {
    const trigger = this.trigger;
    if (!trigger) return;

    const inserted = mentionPrefix(candidate.type) + candidate.label + ' ';
    const current = trigger.draft === 'new' ? this.text : this.editedText;
    const next = current.slice(0, trigger.start) + inserted + current.slice(trigger.end);
    const mention: DraftMention = { type: candidate.type, id: candidate.id, label: candidate.label };

    if (trigger.draft === 'new') {
      this.text = next;
      this.newMentions = [...this.newMentions, mention];
    } else {
      this.editedText = next;
      this.editMentions = [...this.editMentions, mention];
    }

    this.closeSuggestions();

    // El cursor, justo detrás de lo insertado, cuando el cuadro ya tiene el texto nuevo.
    const caret = trigger.start + inserted.length;
    setTimeout(() => {
      box.focus();
      box.setSelectionRange(caret, caret);
    });
  }

  closeSuggestions(): void {
    this.searchSequence++;
    this.trigger = null;
    this.suggestions.set([]);
    this.suggestingFor.set(null);
  }

  /** Los de primer nivel, en orden. Las respuestas se pintan colgando del suyo. */
  readonly thread = computed(() => this.comments().filter(c => !c.replyToId));

  ngOnInit(): void {
    this.load();
    if (!this.users.users().length) this.users.loadTenantUsers().subscribe();
  }

  repliesOf(id: string): Comment[] {
    return this.comments().filter(c => c.replyToId === id);
  }

  nameOf(authorId: string): string {
    return this.users.getUser(authorId)?.name ?? $localize`Alguien del equipo`;
  }

  /** Quién puede editar: sólo su autor. Lo mismo que exige el dominio. */
  isMine(comment: Comment): boolean {
    return comment.authorId === this.session.userInfo()?.id;
  }

  /** Quién puede borrar: su autor o quien administra. También igual que el dominio. */
  canDelete(comment: Comment): boolean {
    return this.isMine(comment) || this.session.isAdmin();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');

    this.service.thread(this.entityType(), this.entityId()).subscribe({
      next: comments => {
        this.comments.set(comments ?? []);
        this.loading.set(false);
      },
      error: response => {
        this.error.set(errorMessage(response, $localize`No se pudieron cargar los comentarios`));
        this.loading.set(false);
      },
    });
  }

  replyTo(id: string | null): void {
    this.replyingTo.set(id);
    this.error.set('');
  }

  send(): void {
    const trimmed = this.text.trim();
    if (!trimmed || this.sending()) return;

    this.sending.set(true);
    this.error.set('');

    const stored = toStored(trimmed, this.newMentions);

    this.service.comment(this.entityType(), this.entityId(), stored, this.replyingTo() ?? undefined).subscribe({
      next: comment => {
        this.comments.update(current => [...current, comment]);
        this.text = '';
        this.newMentions = [];
        this.replyingTo.set(null);
        this.sending.set(false);
      },
      error: response => {
        this.sending.set(false);
        // No se borra lo escrito: es lo único que quien lo redactó no puede recuperar.
        this.error.set(errorMessage(response, $localize`No se pudo publicar el comentario`));
      },
    });
  }

  startEdit(comment: Comment): void {
    this.editing.set(comment.id);
    // Se edita como se escribió: con los nombres, no con los identificadores.
    const draft = toDraft(comment.text);
    this.editedText = draft.text;
    this.editMentions = draft.mentions;
    this.error.set('');
  }

  cancelEdit(): void {
    this.editing.set(null);
  }

  saveEdit(comment: Comment): void {
    const trimmed = this.editedText.trim();
    if (!trimmed) return;

    const stored = toStored(trimmed, this.editMentions);

    this.service.edit(comment.id, stored).subscribe({
      next: () => {
        this.comments.update(current => current.map(c =>
          c.id === comment.id ? { ...c, text: stored, editedAtUtc: new Date().toISOString() } : c));
        this.editing.set(null);
      },
      error: response => this.error.set(
        errorMessage(response, $localize`No se pudo guardar el comentario`)),
    });
  }

  delete(comment: Comment): void {
    this.service.delete(comment.id).subscribe({
      next: () => {
        // Se van también sus respuestas: sin el comentario del que colgaban no se entienden.
        this.comments.update(current =>
          current.filter(c => c.id !== comment.id && c.replyToId !== comment.id));
        this.deleting.set(null);
      },
      error: response => {
        this.deleting.set(null);
        this.error.set(errorMessage(response, $localize`No se pudo borrar el comentario`));
      },
    });
  }
}
