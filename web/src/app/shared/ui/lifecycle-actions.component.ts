import { Component, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideArchive, lucideArchiveRestore, lucideShare2, lucideTrash2, lucideUndo2, lucideX } from '@ng-icons/lucide';
import { firstValueFrom, map } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { AuthSignalStore } from '../../core/auth-signal.store';
import { UsersService } from '../../core/users.service';
import { errorMessage } from '../utils/error-message';

/** Lo que se puede archivar, mandar a la papelera y compartir. */
export type LifecycleKind = 'Task' | 'Ticket' | 'Project';

/** Lo que acaba de pasarle al elemento, para que la lista se recargue y la ficha se cierre. */
export type LifecycleChange = 'archived' | 'unarchived' | 'trashed' | 'restored';

const PATHS: Record<LifecycleKind, string> = { Task: '/tasks', Ticket: '/tickets', Project: '/projects' };

/**
 * Archivar, mandar a la papelera, restaurar y compartir una tarea, un ticket o un proyecto.
 *
 * La API existía y estaba probada, y el menú enseñaba «Archivado» y «Papelera», pero ningún
 * botón archivaba, borraba ni compartía: esas listas solo se llenaban desde la API.
 *
 * **Lo que ofrece depende de dónde se abrió la ficha**, que es el filtro de la URL: en la
 * papelera solo se restaura, en el archivo se desarchiva, y en el resto se archiva, se borra o se
 * comparte. Leerlo de la URL evita que cada lista tenga que pasarlo.
 *
 * Mandar a la papelera se confirma en el propio botón, no con un `confirm()` del navegador, como
 * en el resto de la aplicación.
 */
@Component({
  selector: 'app-lifecycle-actions',
  standalone: true,
  imports: [FormsModule, NgIconComponent],
  viewProviders: [provideIcons({ lucideArchive, lucideArchiveRestore, lucideShare2, lucideTrash2, lucideUndo2, lucideX })],
  template: `
    <div class="relative flex items-center gap-1.5">
      @switch (context()) {
        @case ('trash') {
          <button type="button" [class]="buttonClass" [disabled]="busy()" (click)="run('restore', 'restored')">
            <ng-icon name="lucideUndo2" size="14" /><span i18n>Restaurar</span>
          </button>
        }
        @case ('archived') {
          <button type="button" [class]="buttonClass" [disabled]="busy()" (click)="run('unarchive', 'unarchived')">
            <ng-icon name="lucideArchiveRestore" size="14" /><span i18n>Desarchivar</span>
          </button>
        }
        @default {
          <button type="button" [class]="buttonClass" [disabled]="busy()" (click)="toggleSharing()"
                  [attr.aria-expanded]="sharing()">
            <ng-icon name="lucideShare2" size="14" /><span i18n>Compartir</span>
          </button>
          <button type="button" [class]="buttonClass" [disabled]="busy()" (click)="run('archive', 'archived')">
            <ng-icon name="lucideArchive" size="14" /><span i18n>Archivar</span>
          </button>
        }
      }

      @if (context() !== 'trash') {
        @if (confirmingTrash()) {
          <span class="text-xs text-muted-foreground" i18n>¿Enviar a la papelera?</span>
          <button type="button" [class]="dangerClass" [disabled]="busy()" (click)="trash()" i18n>Sí, enviar</button>
          <button type="button" [class]="buttonClass" (click)="confirmingTrash.set(false)" i18n>Cancelar</button>
        } @else {
          <button type="button" [class]="dangerClass" [disabled]="busy()" (click)="confirmingTrash.set(true)">
            <ng-icon name="lucideTrash2" size="14" /><span i18n>Papelera</span>
          </button>
        }
      }

      @if (sharing()) {
        <div class="absolute right-0 top-full mt-2 w-80 z-20 rounded-lg border border-border bg-card shadow-xl p-3 space-y-3"
             role="dialog" i18n-aria-label aria-label="Compartir" data-testid="share-panel">
          <div class="flex items-center justify-between">
            <p class="text-sm font-medium" i18n>Compartir con</p>
            <button type="button" class="p-1 rounded hover:bg-accent text-muted-foreground" (click)="sharing.set(false)"
                    i18n-aria-label aria-label="Cerrar">
              <ng-icon name="lucideX" size="14" />
            </button>
          </div>

          @if (sharedWith().length === 0) {
            <p class="text-xs text-muted-foreground" i18n>Todavía no está compartido con nadie.</p>
          } @else {
            <ul class="space-y-1">
              @for (id of sharedWith(); track id) {
                <li class="flex items-center justify-between text-sm">
                  <span>{{ nameOf(id) }}</span>
                  <button type="button" class="text-xs text-destructive hover:underline" [disabled]="busy()"
                          (click)="unshare(id)" [attr.aria-label]="unshareLabel(id)" i18n>Quitar</button>
                </li>
              }
            </ul>
          }

          <div class="flex items-center gap-2">
            <select class="flex-1 min-w-0 border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                    [(ngModel)]="shareWith" name="shareWith" i18n-aria-label aria-label="Persona">
              <option value="" disabled i18n>Elige a alguien</option>
              @for (u of candidates(); track u.id) {
                <option [value]="u.id">{{ u.name }}</option>
              }
            </select>
            <select class="border border-border rounded-md px-2 py-1.5 text-sm bg-background"
                    [(ngModel)]="shareLevel" name="shareLevel" i18n-aria-label aria-label="Nivel">
              <option value="View" i18n>Ver</option>
              <option value="Edit" i18n>Editar</option>
              <option value="Full" i18n>Total</option>
            </select>
          </div>
          <button type="button" class="w-full rounded-md bg-primary text-primary-foreground text-sm py-1.5 disabled:opacity-50"
                  [disabled]="busy() || !shareWith" (click)="share()" i18n>Compartir</button>
        </div>
      }
    </div>

    @if (error()) {
      <p class="text-xs text-destructive mt-1" role="alert">{{ error() }}</p>
    }
  `,
})
export class LifecycleActionsComponent {
  readonly kind = input.required<LifecycleKind>();
  readonly entityId = input.required<string>();
  readonly changed = output<LifecycleChange>();

  private readonly api = inject(ApiService);
  private readonly users = inject(UsersService);
  private readonly auth = inject(AuthSignalStore);
  private readonly route = inject(ActivatedRoute);

  /** El filtro de la lista desde la que se abrió la ficha: `trash`, `archived` u otro. */
  readonly context = toSignal(this.route.queryParamMap.pipe(map(p => p.get('filter') ?? '')), { initialValue: '' });

  readonly busy = signal(false);
  readonly error = signal('');
  readonly confirmingTrash = signal(false);
  readonly sharing = signal(false);
  readonly sharedWith = signal<string[]>([]);

  shareWith = '';
  shareLevel: 'View' | 'Edit' | 'Full' = 'View';

  /** Con quién se puede compartir todavía: ni conmigo ni con quien ya lo tiene. */
  readonly candidates = computed(() => {
    const me = this.auth.userInfo()?.id;
    const already = new Set(this.sharedWith());
    return this.users.users().filter(u => u.id !== me && !already.has(u.id));
  });

  readonly buttonClass = 'px-3 py-1.5 rounded-lg border border-border text-xs font-medium hover:bg-accent transition-colors flex items-center gap-1.5 disabled:opacity-50';
  readonly dangerClass = 'px-3 py-1.5 rounded-lg border border-destructive/30 text-xs font-medium text-destructive-subtle-fg hover:bg-destructive-subtle transition-colors flex items-center gap-1.5 disabled:opacity-50';

  private get path(): string { return `${PATHS[this.kind()]}/${this.entityId()}`; }
  private get sharingPath(): string { return `/sharing/${this.kind()}/${this.entityId()}`; }

  nameOf(id: string): string {
    return this.users.getUser(id)?.name ?? $localize`Persona que ya no está`;
  }

  unshareLabel(id: string): string {
    return $localize`Dejar de compartir con ${this.nameOf(id)}:name:`;
  }

  async run(action: 'archive' | 'unarchive' | 'restore', change: LifecycleChange): Promise<void> {
    await this.attempt(() => firstValueFrom(this.api.post<void>(`${this.path}/${action}`, {}, { silent: true })),
      $localize`No se pudo completar la acción.`);
    if (!this.error()) this.changed.emit(change);
  }

  /** Borrar es mandar a la papelera: `DELETE` ya no borra de verdad, y se puede restaurar. */
  async trash(): Promise<void> {
    await this.attempt(() => firstValueFrom(this.api.delete<void>(this.path, { silent: true })),
      $localize`No se pudo enviar a la papelera.`);
    this.confirmingTrash.set(false);
    if (!this.error()) this.changed.emit('trashed');
  }

  async toggleSharing(): Promise<void> {
    if (this.sharing()) { this.sharing.set(false); return; }
    this.sharing.set(true);
    if (this.users.users().length === 0) await firstValueFrom(this.users.loadTenantUsers());
    await this.loadShares();
  }

  async share(): Promise<void> {
    const withUser = this.shareWith;
    await this.attempt(() => firstValueFrom(this.api.put<void>(
      `${this.sharingPath}/${withUser}`, { level: this.shareLevel }, { silent: true })),
      $localize`No se pudo compartir.`);
    if (!this.error()) {
      this.shareWith = '';
      await this.loadShares();
    }
  }

  async unshare(id: string): Promise<void> {
    await this.attempt(() => firstValueFrom(this.api.delete<void>(`${this.sharingPath}/${id}`, { silent: true })),
      $localize`No se pudo dejar de compartir.`);
    if (!this.error()) await this.loadShares();
  }

  private async loadShares(): Promise<void> {
    await this.attempt(async () => this.sharedWith.set(await firstValueFrom(this.api.get<string[]>(this.sharingPath))),
      $localize`No se pudo saber con quién está compartido.`);
  }

  /** Corre una llamada y deja el motivo del fallo a la vista, junto a los botones. */
  private async attempt(call: () => Promise<unknown>, fallback: string): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await call();
    } catch (e) {
      this.error.set(errorMessage(e, fallback));
    } finally {
      this.busy.set(false);
    }
  }
}
