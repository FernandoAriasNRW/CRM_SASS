import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import {
  WebhookSubscription,
  CreateWebhookRequest,
  UpdateWebhookRequest,
  WebhookEventType,
} from './webhook.model';
import { AuthSignalStore } from '../../core/auth-signal.store';

@Injectable({ providedIn: 'root' })
export class WebhookService {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly authStore = inject(AuthSignalStore);

  /**
   * Gets all webhook subscriptions for the current tenant.
   */
  getSubscriptions(): Observable<WebhookSubscription[]> {
    const tenantId = this.getTenantId();
    return this.api.get<WebhookSubscription[]>(`/webhooks?tenantId=${tenantId}`);
  }

  /**
   * Gets a single webhook subscription by ID.
   */
  getSubscription(subscriptionId: string): Observable<WebhookSubscription> {
    const tenantId = this.getTenantId();
    return this.api.get<WebhookSubscription>(
      `/webhooks/${subscriptionId}?tenantId=${tenantId}`
    );
  }

  /**
   * Creates a new webhook subscription.
   */
  createSubscription(request: CreateWebhookRequest): Observable<WebhookSubscription> {
    return this.api.post<WebhookSubscription>('/webhooks', request).pipe(
      tap({
        next: (subscription) => {
          this.toast.success(
            'Webhook creado',
            `"${subscription.name}" ha sido creado exitosamente. Guarda el secret de forma segura.`
          );
        },
        error: () => {
          this.toast.error('Error', 'No se pudo crear el webhook');
        },
      })
    );
  }

  /**
   * Updates an existing webhook subscription.
   */
  updateSubscription(
    subscriptionId: string,
    request: UpdateWebhookRequest
  ): Observable<WebhookSubscription> {
    const tenantId = this.getTenantId();
    return this.api
      .put<WebhookSubscription>(`/webhooks/${subscriptionId}?tenantId=${tenantId}`, request)
      .pipe(
        tap({
          next: () => {
            this.toast.success('Webhook actualizado', 'Los cambios han sido guardados');
          },
          error: () => {
            this.toast.error('Error', 'No se pudo actualizar el webhook');
          },
        })
      );
  }

  /**
   * Deletes a webhook subscription.
   */
  deleteSubscription(subscriptionId: string): Observable<void> {
    const tenantId = this.getTenantId();
    return this.api.delete<void>(`/webhooks/${subscriptionId}?tenantId=${tenantId}`).pipe(
      tap({
        next: () => {
          this.toast.success('Webhook eliminado', 'La suscripción ha sido eliminada');
        },
        error: () => {
          this.toast.error('Error', 'No se pudo eliminar el webhook');
        },
      })
    );
  }

  /**
   * Regenerates the webhook secret.
   */
  regenerateSecret(subscriptionId: string): Observable<{ secret: string }> {
    const tenantId = this.getTenantId();
    return this.api
      .post<{ secret: string }>(
        `/webhooks/${subscriptionId}/regenerate-secret?tenantId=${tenantId}`,
        {}
      )
      .pipe(
        tap({
          next: () => {
            this.toast.warning(
              'Secret regenerado',
              'El secret anterior ha dejado de funcionar. Usa el nuevo secret.'
            );
          },
          error: () => {
            this.toast.error('Error', 'No se pudo regenerar el secret');
          },
        })
      );
  }

  /**
   * Gets the list of available webhook event types.
   */
  getEventTypes(): Observable<WebhookEventType[]> {
    return this.api.get<WebhookEventType[]>('/webhooks/events');
  }

  /**
   * Toggles the active status of a webhook subscription.
   */
  toggleActive(subscriptionId: string, isActive: boolean): Observable<WebhookSubscription> {
    const tenantId = this.getTenantId();
    return this.updateSubscription(subscriptionId, { isActive });
  }

  /**
   * Gets the current tenant ID from the auth store.
   */
  private getTenantId(): string {
    const userInfo = this.authStore.userInfo();
    return userInfo?.tenantId ?? '';
  }

  /**
   * Parses event types string to array.
   */
  parseEventTypes(eventTypes: string): string[] {
    if (!eventTypes) return [];
    return eventTypes.split(',').map((s) => s.trim()).filter(Boolean);
  }

  /**
   * Formats event types array to comma-separated string.
   */
  formatEventTypes(eventTypes: string[]): string {
    return eventTypes.join(',');
  }
}