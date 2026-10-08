import { Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX, lucideBell, lucideClock, lucideShieldCheck } from '@ng-icons/lucide';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../services/toast.service';
import { errorMessage } from '../utils/error-message';

/** Un tipo de aviso tal como lo devuelve el servidor. */
interface TypePreference {
  kind: string;
  category: string;
  enabled: boolean;
  adminOnly: boolean;
}

interface Preferences {
  emailEnabled: boolean;
  pushEnabled: boolean;
  quietHoursEnabled: boolean;
  quietHoursStart: string;
  quietHoursEnd: string;
  types: TypePreference[];
}

/**
 * Qué avisos recibe cada persona, y cuándo no.
 *
 * <b>La lista sale del servidor</b>, con lo que puede recibir quien pregunta: quien no administra
 * no ve los avisos de administración. Antes eran once interruptores escritos aquí, algunos de
 * avisos que nadie mandaba; y había un apartado de avisos push contra una API que no existe.
 * Push y correo se quitan de la pantalla hasta que haya algo que los mande: un interruptor que no
 * hace nada es prometer y no cumplir.
 *
 * Cada cambio se guarda al momento, sólo con el tipo que se tocó.
 */
@Component({
  selector: 'app-notification-preferences',
  standalone: true,
  imports: [FormsModule, NgIconComponent, NgTemplateOutlet],
  viewProviders: [provideIcons({ lucideX, lucideBell, lucideClock, lucideShieldCheck })],
  template: `
    @if (inline()) {
      <ng-container *ngTemplateOutlet="body" />
    } @else {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4">
        <div class="w-full max-w-lg bg-card rounded-xl border border-border shadow-2xl overflow-hidden"
             role="dialog" aria-modal="true" aria-labelledby="notification-preferences-title">
          <div class="flex items-center justify-between px-6 py-4 border-b border-border">
            <div class="flex items-center gap-3">
              <div class="p-2 rounded-lg bg-primary/10">
                <ng-icon name="lucideBell" size="20" class="text-primary" />
              </div>
              <h2 id="notification-preferences-title" class="text-lg font-semibold" i18n>Preferencias de avisos</h2>
            </div>
            <button (click)="close()" i18n-aria-label aria-label="Cerrar"
                    class="p-1.5 rounded-md hover:bg-accent text-muted-foreground hover:text-foreground transition-colors">
              <ng-icon name="lucideX" size="18" />
            </button>
          </div>
          <div class="px-6 py-4 max-h-[70vh] overflow-y-auto">
            <ng-container *ngTemplateOutlet="body" />
          </div>
        </div>
      </div>
    }

    <ng-template #body>
      @if (loading()) {
        <p class="text-sm text-muted-foreground py-6 text-center" i18n>Cargando…</p>
      } @else if (preferences(); as p) {
        <div class="space-y-6">
          <p class="text-xs text-muted-foreground" i18n>
            Elige de qué quieres enterarte. Nunca recibes aviso de lo que haces tú.
          </p>

          @for (group of groups(); track group.category) {
            <fieldset class="space-y-1">
              <legend class="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2">{{ group.label }}</legend>
              @for (type of group.types; track type.kind) {
                <label class="flex items-center justify-between gap-3 rounded-lg px-3 py-2 hover:bg-accent/50 cursor-pointer">
                  <span class="text-sm flex items-center gap-2">
                    {{ kindLabel(type.kind) }}
                    @if (type.adminOnly) {
                      <ng-icon name="lucideShieldCheck" size="12" class="text-muted-foreground" i18n-title title="Sólo administración" />
                    }
                  </span>
                  <input type="checkbox" [checked]="type.enabled" (change)="toggle(type)"
                         class="w-4 h-4 rounded border-input text-primary focus:ring-2 focus:ring-primary" />
                </label>
              }
            </fieldset>
          }

          <fieldset class="space-y-3 border-t border-border pt-4">
            <legend class="text-xs font-semibold uppercase tracking-wide text-muted-foreground flex items-center gap-2">
              <ng-icon name="lucideClock" size="12" />
              <span i18n>Horas de silencio</span>
            </legend>
            <label class="flex items-center justify-between gap-3 px-3 cursor-pointer">
              <span class="text-sm" i18n>No recibir avisos en este tramo</span>
              <input type="checkbox" [ngModel]="p.quietHoursEnabled" (ngModelChange)="setQuiet({ quietHoursEnabled: $event })"
                     class="w-4 h-4 rounded border-input text-primary focus:ring-2 focus:ring-primary" />
            </label>
            @if (p.quietHoursEnabled) {
              <div class="flex items-center gap-3 px-3">
                <label for="quiet-start" class="text-xs text-muted-foreground" i18n>Desde</label>
                <input id="quiet-start" type="time" [ngModel]="p.quietHoursStart" (ngModelChange)="setQuiet({ quietHoursStart: $event })"
                       class="rounded-md border border-border bg-background px-2 py-1 text-sm" />
                <label for="quiet-end" class="text-xs text-muted-foreground" i18n>hasta</label>
                <input id="quiet-end" type="time" [ngModel]="p.quietHoursEnd" (ngModelChange)="setQuiet({ quietHoursEnd: $event })"
                       class="rounded-md border border-border bg-background px-2 py-1 text-sm" />
              </div>
            }
          </fieldset>
        </div>
      }
    </ng-template>
  `,
})
export class NotificationPreferencesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  /** Dentro de otra pantalla —el perfil— y no como ventana encima. */
  readonly inline = input(false);
  readonly closed = output<void>();

  readonly preferences = signal<Preferences | null>(null);
  readonly loading = signal(true);

  private static readonly CATEGORY_LABELS: Record<string, string> = {
    tasks: $localize`Tareas`,
    tickets: $localize`Tickets`,
    projects: $localize`Proyectos`,
    mentions: $localize`Menciones`,
    reports: $localize`Informes`,
    chat: $localize`Chat`,
    teams: $localize`Equipos`,
    users: $localize`Cuentas de la organización`,
    webhooks: $localize`Webhooks`,
  };

  /**
   * Cómo se llama cada aviso en pantalla. La clave es la del servidor
   * (`NotificationCatalog`); uno nuevo sin nombre aquí se enseña con su clave.
   */
  private static readonly KIND_LABELS: Record<string, string> = {
    'task.assigned': $localize`Me asignan una tarea`,
    'task.status_changed': $localize`Una tarea mía cambia de estado`,
    'task.completed': $localize`Se completa una tarea mía`,
    'task.updated': $localize`Cualquier cambio en una tarea mía`,
    'task.deleted': $localize`Se borra una tarea mía`,
    'task.commented': $localize`Comentan en una tarea mía`,
    'task.due_soon': $localize`Una tarea mía se acerca a su vencimiento`,
    'ticket.assigned': $localize`Me asignan un ticket`,
    'ticket.status_changed': $localize`Un ticket mío cambia de estado`,
    'ticket.updated': $localize`Cualquier cambio en un ticket mío`,
    'ticket.commented': $localize`Comentan en un ticket mío`,
    'project.updated': $localize`Cambia un proyecto mío`,
    'project.deleted': $localize`Se borra un proyecto mío`,
    'project.commented': $localize`Comentan en un proyecto mío`,
    'mention': $localize`Me mencionan`,
    'report.export_ready': $localize`Mi exportación está lista`,
    'report.export_failed': $localize`Mi exportación no salió`,
    'chat.message': $localize`Escriben en una conversación en la que participo`,
    'team.member_added': $localize`Me añaden a un equipo`,
    'team.member_removed': $localize`Me quitan de un equipo`,
    'user.created': $localize`Se crea una cuenta`,
    'user.updated': $localize`Cambia una cuenta`,
    'user.deleted': $localize`Se borra una cuenta`,
    'webhook.delivery_failed': $localize`Un webhook deja de entregarse`,
  };

  readonly groups = computed(() => {
    const byCategory = new Map<string, TypePreference[]>();
    for (const type of this.preferences()?.types ?? []) {
      byCategory.set(type.category, [...(byCategory.get(type.category) ?? []), type]);
    }
    return [...byCategory.entries()].map(([category, types]) => ({
      category,
      label: NotificationPreferencesComponent.CATEGORY_LABELS[category] ?? category,
      types,
    }));
  });

  kindLabel(kind: string): string {
    return NotificationPreferencesComponent.KIND_LABELS[kind] ?? kind;
  }

  ngOnInit(): void {
    this.api.get<Preferences>('/notifications/preferences').subscribe({
      next: p => {
        this.preferences.set({ ...p, types: Array.isArray(p?.types) ? p.types : [] });
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  toggle(type: TypePreference): void {
    this.save({ types: [{ kind: type.kind, enabled: !type.enabled }] });
  }

  setQuiet(change: Partial<Pick<Preferences, 'quietHoursEnabled' | 'quietHoursStart' | 'quietHoursEnd'>>): void {
    const current = this.preferences();
    if (!current) return;
    this.preferences.set({ ...current, ...change });
    this.save({ types: [] });
  }

  /**
   * Guarda y pinta lo que devuelve el servidor, no lo que se mandó: si algo se rechaza, el
   * interruptor vuelve a donde estaba en vez de quedarse diciendo lo que no se guardó.
   */
  private save(change: { types: { kind: string; enabled: boolean }[] }): void {
    const current = this.preferences();
    if (!current) return;

    this.api.put<Preferences>('/notifications/preferences', {
      emailEnabled: current.emailEnabled,
      pushEnabled: current.pushEnabled,
      quietHoursEnabled: current.quietHoursEnabled,
      quietHoursStart: current.quietHoursStart,
      quietHoursEnd: current.quietHoursEnd,
      types: change.types,
    }, { silent: true }).subscribe({
      next: saved => this.preferences.set({ ...saved, types: Array.isArray(saved?.types) ? saved.types : [] }),
      error: err => {
        this.toast.error($localize`No se pudieron guardar tus preferencias`, errorMessage(err, ''));
        this.preferences.set({ ...current });
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
