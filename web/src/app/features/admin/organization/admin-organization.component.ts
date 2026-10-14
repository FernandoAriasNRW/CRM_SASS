import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiService } from '../../../core/api.service';
import { errorMessage } from '../../../shared/utils/error-message';

interface OrganizationSettings { timeZone: string; }

/** Las zonas que conoce el navegador, más UTC, que no siempre viene en la lista. */
function availableTimeZones(): string[] {
  const intl = Intl as unknown as { supportedValuesOf?: (key: string) => string[] };
  const zones = intl.supportedValuesOf?.('timeZone') ?? [];
  return ['UTC', ...zones.filter(z => z !== 'UTC')];
}

/**
 * La configuración de la organización. Por ahora, su zona horaria.
 *
 * La usan los informes programados —«cada lunes a las 8» es a las 8 de la organización, no del
 * servidor— y las fechas de los informes. El calendario sigue con la hora del navegador de cada
 * persona. Mientras nadie la cambie es UTC, que es lo que se usaba hasta ahora.
 */
@Component({
  selector: 'app-admin-organization',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="max-w-xl space-y-6">
      <div>
        <h2 class="text-lg font-semibold" i18n>Organización</h2>
        <p class="text-sm text-muted-foreground" i18n>Lo que vale para toda la organización.</p>
      </div>

      <div class="space-y-2">
        <label for="organization-time-zone" class="text-sm font-medium block" i18n>Zona horaria</label>
        <p class="text-xs text-muted-foreground" i18n>
          Los informes programados se generan a su hora en esta zona, y las fechas de los informes se
          escriben en ella. El calendario sigue usando la hora del navegador de cada persona.
        </p>
        <div class="flex items-center gap-2">
          <select id="organization-time-zone" name="timeZone" [(ngModel)]="timeZone" [disabled]="loading() || saving()"
                  class="flex-1 min-w-0 border border-border rounded-md px-3 py-2 text-sm bg-background">
            @for (zone of zones(); track zone) {
              <option [value]="zone">{{ zone }}</option>
            }
          </select>
          <button type="button" (click)="save()" [disabled]="loading() || saving() || timeZone === saved()"
                  class="rounded-md bg-primary text-primary-foreground text-sm px-4 py-2 disabled:opacity-50" i18n>
            Guardar
          </button>
        </div>
        @if (savedNow()) {
          <p class="text-xs text-success-subtle-fg" role="status" i18n>Guardado.</p>
        }
        @if (error()) {
          <p class="text-xs text-destructive" role="alert">{{ error() }}</p>
        }
      </div>
    </div>
  `,
})
export class AdminOrganizationComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly saved = signal('UTC');
  readonly savedNow = signal(false);

  timeZone = 'UTC';

  /** La guardada va siempre en la lista, aunque el navegador no la conozca: si no, el desplegable la enseñaría vacía. */
  readonly zones = computed(() => {
    const zones = availableTimeZones();
    return zones.includes(this.saved()) ? zones : [this.saved(), ...zones];
  });

  async ngOnInit(): Promise<void> {
    try {
      const settings = await firstValueFrom(this.api.get<OrganizationSettings>('/organization/settings'));
      this.saved.set(settings.timeZone);
      this.timeZone = settings.timeZone;
    } catch (e) {
      this.error.set(errorMessage(e, $localize`No se pudo cargar la configuración.`));
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.savedNow.set(false);
    try {
      const settings = await firstValueFrom(this.api.put<OrganizationSettings>(
        '/organization/settings', { timeZone: this.timeZone }, { silent: true }));
      this.saved.set(settings.timeZone);
      this.savedNow.set(true);
    } catch (e) {
      this.error.set(errorMessage(e, $localize`No se pudo guardar la zona horaria.`));
    } finally {
      this.saving.set(false);
    }
  }
}
