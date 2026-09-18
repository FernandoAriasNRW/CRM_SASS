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
import { UserAvatarComponent } from './user-avatar.component';
import { mensajeDeError } from '../utils/mensaje-de-error';

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
  imports: [FormsModule, DatePipe, NgTemplateOutlet, NgIconComponent, UserAvatarComponent],
  viewProviders: [provideIcons({ lucideSend, lucideTrash2, lucidePencil, lucideLoader2, lucideCircleAlert, lucideX })],
  templateUrl: './comments.component.html',
})
export class CommentsComponent implements OnInit {
  readonly entityType = input.required<CommentableEntity>();
  readonly entityId = input.required<string>();

  private readonly service = inject(CommentsService);
  private readonly users = inject(UsersService);
  private readonly session = inject(AuthSignalStore);

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
        this.error.set(mensajeDeError(response, $localize`No se pudieron cargar los comentarios`));
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

    this.service.comment(this.entityType(), this.entityId(), trimmed, this.replyingTo() ?? undefined).subscribe({
      next: comment => {
        this.comments.update(current => [...current, comment]);
        this.text = '';
        this.replyingTo.set(null);
        this.sending.set(false);
      },
      error: response => {
        this.sending.set(false);
        // No se borra lo escrito: es lo único que quien lo redactó no puede recuperar.
        this.error.set(mensajeDeError(response, $localize`No se pudo publicar el comentario`));
      },
    });
  }

  startEdit(comment: Comment): void {
    this.editing.set(comment.id);
    this.editedText = comment.text;
    this.error.set('');
  }

  cancelEdit(): void {
    this.editing.set(null);
  }

  saveEdit(comment: Comment): void {
    const trimmed = this.editedText.trim();
    if (!trimmed) return;

    this.service.edit(comment.id, trimmed).subscribe({
      next: () => {
        this.comments.update(current => current.map(c =>
          c.id === comment.id ? { ...c, text: trimmed, editedAtUtc: new Date().toISOString() } : c));
        this.editing.set(null);
      },
      error: response => this.error.set(
        mensajeDeError(response, $localize`No se pudo guardar el comentario`)),
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
        this.error.set(mensajeDeError(response, $localize`No se pudo borrar el comentario`));
      },
    });
  }
}
