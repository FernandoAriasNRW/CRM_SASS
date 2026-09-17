import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideKeyRound, lucidePlus, lucideCopy, lucideCheck, lucideLoader2, lucideCircleAlert, lucideBan,
} from '@ng-icons/lucide';
import { ApiService } from '../../../core/api.service';
import { mensajeDeError } from '../../../shared/utils/mensaje-de-error';
import {
  REQUIRED_FIELDS, OPTIONAL_FIELDS, SAMPLE_KEY, curlExample, curlWithAttachmentsExample, formExample,
} from './intake-examples';

export interface IntakeKey {
  id: string;
  name: string;
  prefix: string;
  createdAtUtc: string;
  lastUsedAtUtc: string | null;
  revokedAtUtc: string | null;
}

interface CreatedIntakeKey {
  id: string;
  name: string;
  prefix: string;
  key: string;
}

/** La ruta pública por la que entran los tickets. Coincide con `TicketingEndpoints`. */
const INTAKE_ROUTE = '/ticket-intake';

/**
 * Las claves con las que la web o el backend de un cliente abren tickets en la organización.
 *
 * **La clave se enseña una sola vez**, justo al crearla, con los ejemplos ya rellenos. El servidor
 * sólo guarda su hash; si se pierde, se revoca y se crea otra. Por eso el aviso está junto a la
 * clave y no en un texto de ayuda aparte que nadie lee a tiempo.
 *
 * Revocar se confirma en la propia fila, como en campos personalizados: el `confirm()` del
 * navegador no se traduce.
 */
@Component({
  selector: 'app-admin-ticket-intake',
  standalone: true,
  imports: [FormsModule, NgIconComponent, DatePipe],
  viewProviders: [provideIcons({
    lucideKeyRound, lucidePlus, lucideCopy, lucideCheck, lucideLoader2, lucideCircleAlert, lucideBan,
  })],
  templateUrl: './admin-ticket-intake.component.html',
})
export class AdminTicketIntakeComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly keys = signal<IntakeKey[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');

  name = '';
  readonly creating = signal(false);
  readonly justCreated = signal<CreatedIntakeKey | null>(null);
  readonly toRevoke = signal<string | null>(null);
  readonly copied = signal<string | null>(null);

  readonly url = this.api.urlDeLaApi(INTAKE_ROUTE);

  /** Los ejemplos llevan la clave recién creada si la hay; si no, un marcador evidente. */
  private readonly keyForExamples = computed(() => this.justCreated()?.key ?? SAMPLE_KEY);
  readonly form = computed(() => formExample(this.url, this.keyForExamples()));
  readonly curl = computed(() => curlExample(this.url, this.keyForExamples()));
  readonly curlWithAttachments = computed(() => curlWithAttachmentsExample(this.url, this.keyForExamples()));
  readonly requiredFields = REQUIRED_FIELDS.join(', ');
  readonly optionalFields = OPTIONAL_FIELDS.join(', ');

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<IntakeKey[]>('/tickets/intake-keys').subscribe({
      next: keys => { this.keys.set(keys); this.loading.set(false); },
      error: err => {
        this.error.set(mensajeDeError(err, $localize`No se pudieron cargar las claves`));
        this.loading.set(false);
      },
    });
  }

  create(): void {
    const name = this.name.trim();
    if (!name) return;

    this.creating.set(true);
    this.error.set('');
    this.api.post<CreatedIntakeKey>('/tickets/intake-keys', { name }).subscribe({
      next: created => {
        this.justCreated.set(created);
        this.name = '';
        this.creating.set(false);
        this.load();
      },
      error: err => {
        this.error.set(mensajeDeError(err, $localize`No se pudo crear la clave`));
        this.creating.set(false);
      },
    });
  }

  revoke(id: string): void {
    this.api.delete(`/tickets/intake-keys/${id}`).subscribe({
      next: () => {
        this.toRevoke.set(null);
        if (this.justCreated()?.id === id) this.justCreated.set(null);
        this.load();
      },
      error: err => this.error.set(mensajeDeError(err, $localize`No se pudo revocar la clave`)),
    });
  }

  async copy(what: string, text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copied.set(what);
      setTimeout(() => this.copied.set(null), 1500);
    } catch {
      this.error.set($localize`No se pudo copiar; selecciona el texto y cópialo a mano`);
    }
  }
}
