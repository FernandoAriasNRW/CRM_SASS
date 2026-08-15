import { Component, computed, inject, input, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideSend, lucideTrash2, lucidePencil, lucideLoader2, lucideCircleAlert, lucideX } from '@ng-icons/lucide';
import {
  CommentsService, type Comentario, type EntidadComentable,
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
  selector: 'app-comentarios',
  standalone: true,
  imports: [FormsModule, DatePipe, NgTemplateOutlet, NgIconComponent, UserAvatarComponent],
  viewProviders: [provideIcons({ lucideSend, lucideTrash2, lucidePencil, lucideLoader2, lucideCircleAlert, lucideX })],
  templateUrl: './comentarios.component.html',
})
export class ComentariosComponent implements OnInit {
  readonly entidad = input.required<EntidadComentable>();
  readonly entityId = input.required<string>();

  private readonly servicio = inject(CommentsService);
  private readonly usuarios = inject(UsersService);
  private readonly sesion = inject(AuthSignalStore);

  readonly comentarios = signal<Comentario[]>([]);
  readonly cargando = signal(false);
  readonly enviando = signal(false);
  readonly error = signal('');

  /** El comentario que se está editando, o `null`. */
  readonly editando = signal<string | null>(null);
  readonly borrando = signal<string | null>(null);

  texto = '';
  textoEditado = '';
  /** A qué comentario se responde, si se está respondiendo. */
  readonly respondiendoA = signal<string | null>(null);

  /** Los de primer nivel, en orden. Las respuestas se pintan colgando del suyo. */
  readonly hilo = computed(() => this.comentarios().filter(c => !c.respondeAId));

  ngOnInit(): void {
    this.cargar();
    if (!this.usuarios.users().length) this.usuarios.loadTenantUsers().subscribe();
  }

  respuestasDe(id: string): Comentario[] {
    return this.comentarios().filter(c => c.respondeAId === id);
  }

  nombreDe(autorId: string): string {
    return this.usuarios.getUser(autorId)?.name ?? $localize`Alguien del equipo`;
  }

  /** Quién puede editar: sólo su autor. Lo mismo que exige el dominio. */
  esMio(comentario: Comentario): boolean {
    return comentario.autorId === this.sesion.userInfo()?.id;
  }

  /** Quién puede borrar: su autor o quien administra. También igual que el dominio. */
  loPuedoBorrar(comentario: Comentario): boolean {
    return this.esMio(comentario) || this.sesion.isAdmin();
  }

  cargar(): void {
    this.cargando.set(true);
    this.error.set('');

    this.servicio.hilo(this.entidad(), this.entityId()).subscribe({
      next: comentarios => {
        this.comentarios.set(comentarios ?? []);
        this.cargando.set(false);
      },
      error: respuesta => {
        this.error.set(mensajeDeError(respuesta, $localize`No se pudieron cargar los comentarios`));
        this.cargando.set(false);
      },
    });
  }

  responderA(id: string | null): void {
    this.respondiendoA.set(id);
    this.error.set('');
  }

  enviar(): void {
    const limpio = this.texto.trim();
    if (!limpio || this.enviando()) return;

    this.enviando.set(true);
    this.error.set('');

    this.servicio.comentar(this.entidad(), this.entityId(), limpio, this.respondiendoA() ?? undefined).subscribe({
      next: comentario => {
        this.comentarios.update(actuales => [...actuales, comentario]);
        this.texto = '';
        this.respondiendoA.set(null);
        this.enviando.set(false);
      },
      error: respuesta => {
        this.enviando.set(false);
        // No se borra lo escrito: es lo único que quien lo redactó no puede recuperar.
        this.error.set(mensajeDeError(respuesta, $localize`No se pudo publicar el comentario`));
      },
    });
  }

  empezarEdicion(comentario: Comentario): void {
    this.editando.set(comentario.id);
    this.textoEditado = comentario.texto;
    this.error.set('');
  }

  cancelarEdicion(): void {
    this.editando.set(null);
  }

  guardarEdicion(comentario: Comentario): void {
    const limpio = this.textoEditado.trim();
    if (!limpio) return;

    this.servicio.editar(comentario.id, limpio).subscribe({
      next: () => {
        this.comentarios.update(actuales => actuales.map(c =>
          c.id === comentario.id ? { ...c, texto: limpio, editadoUtc: new Date().toISOString() } : c));
        this.editando.set(null);
      },
      error: respuesta => this.error.set(
        mensajeDeError(respuesta, $localize`No se pudo guardar el comentario`)),
    });
  }

  borrar(comentario: Comentario): void {
    this.servicio.borrar(comentario.id).subscribe({
      next: () => {
        // Se van también sus respuestas: sin el comentario del que colgaban no se entienden.
        this.comentarios.update(actuales =>
          actuales.filter(c => c.id !== comentario.id && c.respondeAId !== comentario.id));
        this.borrando.set(null);
      },
      error: respuesta => {
        this.borrando.set(null);
        this.error.set(mensajeDeError(respuesta, $localize`No se pudo borrar el comentario`));
      },
    });
  }
}
