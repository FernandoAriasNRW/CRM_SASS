import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../shared/services/toast.service';
import { errorMessage } from '../../shared/utils/error-message';
import type {
  WebhookDelivery, WebhookEventType, WebhookRequest, WebhookSubscription, WebhookWithSecret,
} from './webhook.model';

/**
 * La API de webhooks. Sólo la puede usar quien administra.
 *
 * Mandaba `?tenantId=` en cada petición —el inquilino sale del token, no de la URL— y los eventos
 * como una cadena separada por comas que la API no sabía leer.
 */
@Injectable({ providedIn: 'root' })
export class WebhookService {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  getSubscriptions(): Observable<WebhookSubscription[]> {
    return this.api.get<WebhookSubscription[]>('/webhooks');
  }

  getEventTypes(): Observable<WebhookEventType[]> {
    return this.api.get<WebhookEventType[]>('/webhooks/events');
  }

  createSubscription(request: WebhookRequest): Observable<WebhookWithSecret> {
    return this.api.post<WebhookWithSecret>('/webhooks', request, { silent: true }).pipe(
      tap({
        next: created => this.toast.success($localize`Webhook creado`, created.subscription.name),
        error: err => this.toast.error($localize`No se pudo crear el webhook`, errorMessage(err, '')),
      })
    );
  }

  updateSubscription(id: string, request: WebhookRequest): Observable<WebhookSubscription> {
    return this.api.put<WebhookSubscription>(`/webhooks/${id}`, request, { silent: true }).pipe(
      tap({
        next: () => this.toast.success($localize`Webhook actualizado`),
        error: err => this.toast.error($localize`No se pudo actualizar el webhook`, errorMessage(err, '')),
      })
    );
  }

  deleteSubscription(id: string): Observable<void> {
    return this.api.delete<void>(`/webhooks/${id}`, { silent: true }).pipe(
      tap({
        next: () => this.toast.success($localize`Webhook eliminado`),
        error: err => this.toast.error($localize`No se pudo eliminar el webhook`, errorMessage(err, '')),
      })
    );
  }

  getSecret(id: string): Observable<{ secret: string }> {
    return this.api.get<{ secret: string }>(`/webhooks/${id}/secret`);
  }

  regenerateSecret(id: string): Observable<WebhookWithSecret> {
    return this.api.post<WebhookWithSecret>(`/webhooks/${id}/regenerate-secret`, {}, { silent: true }).pipe(
      tap({ error: err => this.toast.error($localize`No se pudo cambiar el secreto`, errorMessage(err, '')) })
    );
  }

  sendTest(id: string): Observable<WebhookDelivery> {
    return this.api.post<WebhookDelivery>(`/webhooks/${id}/test`, {}, { silent: true }).pipe(
      tap({
        next: () => this.toast.success($localize`Prueba enviada`, $localize`Mira el registro de envíos en unos segundos`),
        error: err => this.toast.error($localize`No se pudo enviar la prueba`, errorMessage(err, '')),
      })
    );
  }

  getDeliveries(id: string): Observable<WebhookDelivery[]> {
    return this.api.get<WebhookDelivery[]>(`/webhooks/${id}/deliveries`);
  }
}
