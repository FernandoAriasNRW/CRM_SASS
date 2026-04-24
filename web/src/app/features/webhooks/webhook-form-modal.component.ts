import { Component, inject, input, output, signal, OnInit, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIconComponent, provideIcons } from '@ng-icons/core';
import {
  lucideX, lucideLoader2, lucideCheck, lucideAlertCircle,
  lucideServer, lucideKey
} from '@ng-icons/lucide';
import { WebhookService } from './webhook.service';
import {
  WebhookSubscription,
  CreateWebhookRequest,
  UpdateWebhookRequest,
  WEBHOOK_EVENT_TYPES,
  WebhookEventType,
} from './webhook.model';

@Component({
  selector: 'app-webhook-form-modal',
  standalone: true,
  imports: [FormsModule, NgIconComponent],
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

  // Inputs
  readonly subscription = input<WebhookSubscription | null>(null);
  readonly tenantId = input.required<string>();

  // Outputs
  readonly closed = output<void>();
  readonly saved = output<WebhookSubscription>();

  // State
  isEditing = computed(() => this.subscription() !== null);

  // Form fields
  name = signal('');
  url = signal('');
  selectedEventTypes = signal<string[]>([]);
  maxRetries = signal(3);
  timeoutSeconds = signal(30);

  // UI state
  saving = signal(false);
  errors = signal<Record<string, string>>({});

  // Available event types
  readonly eventTypes = WEBHOOK_EVENT_TYPES;

  ngOnInit(): void {
    const sub = this.subscription();
    if (sub) {
      this.name.set(sub.name);
      this.url.set(sub.url);
      this.selectedEventTypes.set(
        this.webhookService.parseEventTypes(sub.eventTypes)
      );
      this.maxRetries.set(3); // Default, could be from sub if stored
      this.timeoutSeconds.set(30);
    }
  }

  close(): void {
    this.closed.emit();
  }

  toggleEventType(type: string): void {
    this.selectedEventTypes.update((types) =>
      types.includes(type)
        ? types.filter((t) => t !== type)
        : [...types, type]
    );
    this.clearError('eventTypes');
  }

  isEventTypeSelected(type: string): boolean {
    return this.selectedEventTypes().includes(type);
  }

  validate(): boolean {
    const errors: Record<string, string> = {};

    // Name validation
    if (!this.name().trim()) {
      errors['name'] = 'El nombre es requerido';
    } else if (this.name().length > 200) {
      errors['name'] = 'El nombre debe tener máximo 200 caracteres';
    }

    // URL validation
    if (!this.url().trim()) {
      errors['url'] = 'La URL es requerida';
    } else if (!this.isValidUrl(this.url())) {
      errors['url'] = 'Ingresa una URL válida (https://...)';
    }

    // Event types validation
    if (this.selectedEventTypes().length === 0) {
      errors['eventTypes'] = 'Selecciona al menos un tipo de evento';
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
    this.errors.update((e) => {
      const newErrors = { ...e };
      delete newErrors[field];
      return newErrors;
    });
  }

  save(): void {
    if (!this.validate()) return;

    this.saving.set(true);

    const eventTypesStr = this.webhookService.formatEventTypes(this.selectedEventTypes());

    if (this.isEditing()) {
      const request: UpdateWebhookRequest = {
        name: this.name(),
        url: this.url(),
        eventTypes: eventTypesStr,
        maxRetries: this.maxRetries(),
        timeoutSeconds: this.timeoutSeconds(),
      };

      this.webhookService
        .updateSubscription(this.subscription()!.id, request)
        .subscribe({
          next: (updated) => {
            this.saving.set(false);
            this.saved.emit(updated);
          },
          error: () => {
            this.saving.set(false);
          },
        });
    } else {
      const request: CreateWebhookRequest = {
        tenantId: this.tenantId(),
        name: this.name(),
        url: this.url(),
        eventTypes: eventTypesStr,
        maxRetries: this.maxRetries(),
        timeoutSeconds: this.timeoutSeconds(),
      };

      this.webhookService.createSubscription(request).subscribe({
        next: (created) => {
          this.saving.set(false);
          this.saved.emit(created);
        },
        error: () => {
          this.saving.set(false);
        },
      });
    }
  }
}