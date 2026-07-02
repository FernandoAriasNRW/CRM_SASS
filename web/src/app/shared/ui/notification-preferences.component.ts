import { Component, inject, OnInit, signal, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import { lucideX, lucideBell, lucideMail, lucideSmartphone, lucideClock } from '@ng-icons/lucide';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import { WebPushService, NotificationPreferences } from '../../shared/services/web-push.service';

@Component({
  selector: 'app-notification-preferences',
  standalone: true,
  imports: [FormsModule, NgIconComponent],
  viewProviders: [
    provideIcons({ lucideX, lucideBell, lucideMail, lucideSmartphone, lucideClock })
  ],
  template: `
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4">
      <div class="w-full max-w-lg bg-card rounded-xl border border-border shadow-2xl overflow-hidden">
        <!-- Header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-border">
          <div class="flex items-center gap-3">
            <div class="p-2 rounded-lg bg-primary/10">
              <ng-icon name="lucideBell" size="20" class="text-primary" />
            </div>
            <h2 class="text-lg font-semibold">Preferencias de Notificaciones</h2>
          </div>
          <button
            (click)="close()"
            class="p-1.5 rounded-md hover:bg-accent text-muted-foreground hover:text-foreground transition-colors"
          >
            <ng-icon name="lucideX" size="18" />
          </button>
        </div>

        <!-- Content -->
        <div class="px-6 py-4 space-y-6 max-h-[70vh] overflow-y-auto">
          @if (loading()) {
            <div class="flex items-center justify-center py-8">
              <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
            </div>
          } @else {
            <!-- Push Notifications Section -->
            <div class="space-y-4">
              <h3 class="text-sm font-medium text-muted-foreground uppercase tracking-wide flex items-center gap-2">
                <ng-icon name="lucideSmartphone" size="14" />
                Notificaciones Push
              </h3>

              <div class="space-y-3">
                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.pushEnabled"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Notificaciones push</span>
                      <p class="text-xs text-muted-foreground">Recibe alertas en tu navegador</p>
                    </div>
                  </div>
                </label>

                @if (preferences.pushEnabled) {
                  <div class="ml-7 flex gap-2">
                    @if (!webPush.isSubscribed()) {
                      <button
                        (click)="subscribePush()"
                        class="text-xs px-3 py-1.5 rounded-md bg-primary text-primary-foreground hover:bg-primary/90 transition-colors"
                      >
                        Activar push
                      </button>
                    } @else {
                      <button
                        (click)="testPush()"
                        class="text-xs px-3 py-1.5 rounded-md border border-border hover:bg-accent transition-colors"
                      >
                        Enviar prueba
                      </button>
                      <button
                        (click)="unsubscribePush()"
                        class="text-xs px-3 py-1.5 rounded-md border border-destructive/50 text-destructive hover:bg-destructive/10 transition-colors"
                      >
                        Desactivar
                      </button>
                    }
                  </div>
                }
              </div>
            </div>

            <!-- Email Section -->
            <div class="space-y-4">
              <h3 class="text-sm font-medium text-muted-foreground uppercase tracking-wide flex items-center gap-2">
                <ng-icon name="lucideMail" size="14" />
                Notificaciones por Email
              </h3>

              <div class="space-y-3">
                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.emailEnabled"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Notificaciones por email</span>
                      <p class="text-xs text-muted-foreground">Recibe resúmenes diarios</p>
                    </div>
                  </div>
                </label>
              </div>
            </div>

            <!-- Event Types Section -->
            <div class="space-y-4">
              <h3 class="text-sm font-medium text-muted-foreground uppercase tracking-wide flex items-center gap-2">
                <ng-icon name="lucideBell" size="14" />
                Tipos de Eventos
              </h3>

              <div class="space-y-3">
                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.taskAssigned"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Tarea asignada</span>
                      <p class="text-xs text-muted-foreground">Cuando te asignan una tarea</p>
                    </div>
                  </div>
                </label>

                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.taskCompleted"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Tarea completada</span>
                      <p class="text-xs text-muted-foreground">Cuando se completa una tarea</p>
                    </div>
                  </div>
                </label>

                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.taskDueSoon"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Recordatorio de fecha límite</span>
                      <p class="text-xs text-muted-foreground">24h antes del vencimiento</p>
                    </div>
                  </div>
                </label>

                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.ticketCreated"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Ticket creado</span>
                      <p class="text-xs text-muted-foreground">Nuevo ticket creado</p>
                    </div>
                  </div>
                </label>

                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.ticketUpdated"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Ticket actualizado</span>
                      <p class="text-xs text-muted-foreground">Cambios en tickets</p>
                    </div>
                  </div>
                </label>

                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.projectUpdated"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Proyecto actualizado</span>
                      <p class="text-xs text-muted-foreground">Cambios en proyectos</p>
                    </div>
                  </div>
                </label>

                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.mentionEnabled"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Menciones</span>
                      <p class="text-xs text-muted-foreground">Cuando te mencionan</p>
                    </div>
                  </div>
                </label>
              </div>
            </div>

            <!-- Quiet Hours Section -->
            <div class="space-y-4">
              <h3 class="text-sm font-medium text-muted-foreground uppercase tracking-wide flex items-center gap-2">
                <ng-icon name="lucideClock" size="14" />
                Horas de Silencio
              </h3>

              <div class="space-y-3">
                <label class="flex items-center justify-between p-3 rounded-lg border border-border hover:bg-accent/50 transition-colors cursor-pointer">
                  <div class="flex items-center gap-3">
                    <input
                      type="checkbox"
                      [(ngModel)]="preferences.quietHoursEnabled"
                      (ngModelChange)="savePreferences()"
                      class="w-4 h-4 rounded border-input bg-background text-primary focus:ring-2 focus:ring-primary"
                    />
                    <div>
                      <span class="font-medium">Activar horas de silencio</span>
                      <p class="text-xs text-muted-foreground">No molestar durante estas horas</p>
                    </div>
                  </div>
                </label>

                @if (preferences.quietHoursEnabled) {
                  <div class="ml-7 grid grid-cols-2 gap-4">
                    <div class="space-y-1">
                      <label class="text-xs text-muted-foreground">Desde</label>
                      <input
                        type="time"
                        [(ngModel)]="preferences.quietHoursStart"
                        (ngModelChange)="savePreferences()"
                        class="w-full px-3 py-2 border border-input rounded-md bg-background text-sm focus:ring-2 focus:ring-primary"
                      />
                    </div>
                    <div class="space-y-1">
                      <label class="text-xs text-muted-foreground">Hasta</label>
                      <input
                        type="time"
                        [(ngModel)]="preferences.quietHoursEnd"
                        (ngModelChange)="savePreferences()"
                        class="w-full px-3 py-2 border border-input rounded-md bg-background text-sm focus:ring-2 focus:ring-primary"
                      />
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-border bg-muted/30 flex justify-end">
          <button
            (click)="close()"
            class="px-4 py-2 rounded-md bg-primary text-primary-foreground hover:bg-primary/90 transition-colors font-medium"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  `
})
export class NotificationPreferencesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  readonly webPush = inject(WebPushService);

  readonly closed = output<void>();
  readonly loading = signal(true);
  readonly saving = signal(false);

  preferences: NotificationPreferences = {
    emailEnabled: true,
    pushEnabled: false,
    taskAssigned: true,
    taskCompleted: false,
    taskDueSoon: true,
    ticketCreated: true,
    ticketUpdated: false,
    projectUpdated: true,
    mentionEnabled: true,
    quietHoursEnabled: false,
    quietHoursStart: '22:00',
    quietHoursEnd: '08:00'
  };

  ngOnInit(): void {
    this.loadPreferences();
  }

  close(): void {
    this.closed.emit();
  }

  private loadPreferences(): void {
    this.api.get<NotificationPreferences>('/notifications/preferences').subscribe({
      next: (prefs) => {
        this.preferences = { ...this.preferences, ...prefs };
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  savePreferences(): void {
    this.saving.set(true);
    this.api.put('/notifications/preferences', this.preferences).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Preferencias guardadas', 'Tus preferencias de notificación han sido actualizadas.');
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Error', 'No se pudieron guardar las preferencias.');
      }
    });
  }

  async subscribePush(): Promise<void> {
    const permission = await this.webPush.requestPermission();
    if (permission === 'granted') {
      await this.webPush.subscribe();
      this.preferences.pushEnabled = true;
      this.savePreferences();
    } else {
      this.toast.warning('Permiso denegado', 'No se pudieron activar las notificaciones push.');
    }
  }

  async unsubscribePush(): Promise<void> {
    await this.webPush.unsubscribe();
    this.preferences.pushEnabled = false;
    this.savePreferences();
  }

  testPush(): void {
    this.webPush.testLocalNotification('CRM SaaS', '¡Las notificaciones push están funcionando!');
  }
}