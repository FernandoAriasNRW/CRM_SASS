import { Component, inject, input, output, signal, OnInit, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideX, lucideLoader2, lucideCheck, lucideAlertCircle,
  lucideServer, lucideKey
} from '@ng-icons/lucide';
import { WebhookService } from './webhook.service';
import {
  WEBHOOK_CATEGORY_LABELS, type WebhookEventType, type WebhookRequest, type WebhookSubscription,
} from './webhook.model';

import { DrawerComponent } from '../../shared/ui/drawer.component';

/** Un grupo del catálogo tal como se pinta: su nombre y sus eventos. */
interface EventGroup {
  category: string;
  label: string;
  events: string[];
}

/**
 * Crear o cambiar una suscripción.
 *
 * Los eventos salen del catálogo del servidor, agrupados por área. Había una lista escrita aquí a
 * mano con nombres que el servidor no emitía —«TaskCommentAdded», «UserInvited»—: se podían
 * marcar y no llegaba nunca nada. **Ninguno viene marcado**: se elige uno a uno, o un grupo entero
 * a propósito, pero nunca «todo» por defecto.
 */
@Component({
  selector: 'app-webhook-form-modal',
  standalone: true,
  imports: [FormsModule, NgIconComponent, DrawerComponent],
  viewProviders: [
    provideIcons({
      lucideX, lucideLoader2, lucideCheck, lucideAlertCircle,
      lucideServer, lucideKey
    })
  ],
  templateUrl: './webhook-form-modal.component.html',
})
export class WebhookFormModalComponent implements OnInit {
  private readonly webhookService = inject(WebhookService);

  readonly subscription = input<WebhookSubscription | null>(null);

  readonly closed = output<void>();
  /** La suscripción guardada y, si es nueva, su secreto, que hay que enseñar una vez. */
  readonly saved = output<{ subscription: WebhookSubscription; secret?: string }>();

  isEditing = computed(() => this.subscription() !== null);

  readonly newTitle = $localize`Nuevo webhook`;
  readonly editTitle = $localize`Editar webhook`;
  readonly newSubtitle = $localize`Elige a dónde se mandan los eventos y cuáles`;
  readonly editSubtitle = $localize`Cambia la URL o los eventos que recibe`;

  name = signal('');
  url = signal('');
  selectedEventTypes = signal<string[]>([]);

  saving = signal(false);
  errors = signal<Record<string, string>>({});

  private readonly catalog = signal<WebhookEventType[]>([]);

  readonly groups = computed<EventGroup[]>(() => {
    const byCategory = new Map<string, string[]>();
    for (const event of this.catalog()) {
      byCategory.set(event.category, [...(byCategory.get(event.category) ?? []), event.name]);
    }
    return [...byCategory.entries()].map(([category, events]) => ({
      category,
      label: WEBHOOK_CATEGORY_LABELS[category] ?? category,
      events,
    }));
  });

  ngOnInit(): void {
    const sub = this.subscription();
    if (sub) {
      this.name.set(sub.name);
      this.url.set(sub.url);
      this.selectedEventTypes.set([...sub.eventTypes]);
    }

    this.webhookService.getEventTypes().subscribe({
      next: events => this.catalog.set(Array.isArray(events) ? events : []),
    });
  }

  close(): void {
    this.closed.emit();
  }

  toggleEventType(type: string): void {
    this.selectedEventTypes.update(types =>
      types.includes(type) ? types.filter(t => t !== type) : [...types, type]);
    this.clearError('eventTypes');
  }

  isEventTypeSelected(type: string): boolean {
    return this.selectedEventTypes().includes(type);
  }

  /** Si están marcados todos los eventos del grupo. */
  isGroupSelected(group: EventGroup): boolean {
    return group.events.every(e => this.isEventTypeSelected(e));
  }

  /** Marca o desmarca un grupo entero. Es una decisión explícita, no lo que viene de serie. */
  toggleGroup(group: EventGroup): void {
    const all = this.isGroupSelected(group);
    this.selectedEventTypes.update(types => all
      ? types.filter(t => !group.events.includes(t))
      : [...new Set([...types, ...group.events])]);
    this.clearError('eventTypes');
  }

  validate(): boolean {
    const errors: Record<string, string> = {};

    if (!this.name().trim()) {
      errors['name'] = $localize`El nombre es obligatorio`;
    } else if (this.name().length > 100) {
      errors['name'] = $localize`El nombre puede tener como mucho 100 caracteres`;
    }

    if (!this.url().trim()) {
      errors['url'] = $localize`La URL es obligatoria`;
    } else if (!this.isValidUrl(this.url())) {
      errors['url'] = $localize`Escribe una URL completa que empiece por https://`;
    }

    if (this.selectedEventTypes().length === 0) {
      errors['eventTypes'] = $localize`Elige al menos un evento: un webhook no recibe todo por defecto`;
    }

    this.errors.set(errors);
    return Object.keys(errors).length === 0;
  }

  private isValidUrl(url: string): boolean {
    try {
      const parsed = new URL(url);
      return parsed.protocol === 'https:' || parsed.protocol === 'http:';
    } catch {
      return false;
    }
  }

  clearError(field: string): void {
    this.errors.update(e => {
      const next = { ...e };
      delete next[field];
      return next;
    });
  }

  save(): void {
    if (!this.validate()) return;

    this.saving.set(true);

    const request: WebhookRequest = {
      name: this.name().trim(),
      url: this.url().trim(),
      eventTypes: this.selectedEventTypes(),
      isActive: this.subscription()?.isActive ?? true,
    };

    if (this.isEditing()) {
      this.webhookService.updateSubscription(this.subscription()!.id, request).subscribe({
        next: updated => {
          this.saving.set(false);
          this.saved.emit({ subscription: updated });
        },
        error: () => this.saving.set(false),
      });
    } else {
      this.webhookService.createSubscription(request).subscribe({
        next: created => {
          this.saving.set(false);
          this.saved.emit({ subscription: created.subscription, secret: created.secret });
        },
        error: () => this.saving.set(false),
      });
    }
  }
}
